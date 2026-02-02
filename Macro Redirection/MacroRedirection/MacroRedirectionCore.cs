using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace MacroRedirection;

public unsafe class MacroRedirectionCore : IDisposable
{
    private const uint DefaultTarget = 0xE0000000;
    private readonly Plugin Plugin;
    private readonly Configuration Configuration;
    private readonly Actions Actions;

    private delegate bool UseActionDelegate(ActionManager* thisPtr, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted);
    private delegate bool UseActionLocationDelegate(ActionManager* thisPtr, ActionType actionType, uint actionId, ulong targetId, Vector3* location, uint extraParam);

    private Hook<UseActionDelegate>? UseActionHook;
    private readonly UseActionLocationDelegate UseActionLocation;

    public MacroRedirectionCore(Plugin plugin, Configuration config, Actions actions)
    {
        Plugin = plugin;
        Configuration = config;
        Actions = actions;

        UseActionHook = Services.InteropProvider.HookFromAddress<UseActionDelegate>(
            (nint)ActionManager.MemberFunctionPointers.UseAction,
            UseActionCallback
        );
        UseActionLocation = Marshal.GetDelegateForFunctionPointer<UseActionLocationDelegate>((IntPtr)ActionManager.MemberFunctionPointers.UseActionLocation);

        UseActionHook.Enable();
    }

    private bool UseActionCallback(ActionManager* thisPtr, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, bool* outOptAreaTargeted)
    {
        if (UseActionHook == null) return false;

        // Only handle player actions
        if (actionType != ActionType.Action)
            return UseActionHook.Original(thisPtr, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);

        if (IPCProvider.ACR重定向禁用 && IPCProvider.ACR施法中)
            return UseActionHook.Original(thisPtr, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);

        var originalAction = Actions.GetRow(actionId);
        if (originalAction == null || originalAction.Value.IsPvP)
            return UseActionHook.Original(thisPtr, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);

        // Handle macro queueing
        if ((uint)mode == 2 && Configuration.EnableMacroQueueing)
            mode = ActionManager.UseActionMode.None;

        // Get adjusted action ID
        var adjustedId = ActionManager.MemberFunctionPointers.GetAdjustedActionId(thisPtr, actionId);
        var adjustedAction = Actions.GetRow(adjustedId);
        if (adjustedAction == null)
            return UseActionHook.Original(thisPtr, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);

        var adjustedActionValue = adjustedAction.Value;

        // Find matching redirection entry
        var player = Services.ObjectTable.LocalPlayer;
        if (player == null)
            return UseActionHook.Original(thisPtr, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);

        var matchingEntry = FindMatchingRedirection(originalAction.Value.RowId, adjustedId, player.ClassJob.RowId);

        if (matchingEntry != null)
        {
            // Process redirection
            foreach (var targetOption in matchingEntry.TargetPriority)
            {
                var 目标类型 = 规范化目标类型(targetOption);
                if ((目标类型 == "Mouse Location" || 目标类型 == "Cursor") && adjustedActionValue.TargetArea)
                {
                    Vector3 location;
                    var success = ActionManager.MemberFunctionPointers.GetGroundPositionForCursor(thisPtr, &location);
                    if (success)
                    {
                        return GroundActionAtCursor(thisPtr, actionType, actionId, targetId, extraParam, mode, comboRouteId, &location);
                    }
                }
                else
                {
                    var 目标 = ResolveTarget(目标类型);
                    if (目标 != null && ValidateTarget(adjustedActionValue, 目标) && 可用(adjustedActionValue, 目标))
                    {
                        if (adjustedActionValue.TargetArea)
                            return GroundActionAtTarget(thisPtr, actionType, actionId, 目标, extraParam, mode, comboRouteId);
                        return UseActionHook.Original(thisPtr, actionType, actionId, 目标.GameObjectId, extraParam, mode, comboRouteId, outOptAreaTargeted);
                    }

                    if (目标类型 == "Field Mouseover")
                    {
                        var 当前 = ResolveTarget("Target");
                        if (当前 != null && ValidateTarget(adjustedActionValue, 当前) && 可用(adjustedActionValue, 当前))
                        {
                            if (adjustedActionValue.TargetArea)
                                return GroundActionAtTarget(thisPtr, actionType, actionId, 当前, extraParam, mode, comboRouteId);
                            return UseActionHook.Original(thisPtr, actionType, actionId, 当前.GameObjectId, extraParam, mode, comboRouteId, outOptAreaTargeted);
                        }
                    }
                }
            }
        }

        if (adjustedActionValue.TargetArea && Configuration.DefaultCursorMouseover)
        {
            Vector3 location;
            var success = ActionManager.MemberFunctionPointers.GetGroundPositionForCursor(thisPtr, &location);
            if (success)
                return GroundActionAtCursor(thisPtr, actionType, actionId, targetId, extraParam, mode, comboRouteId, &location);
        }

        return UseActionHook.Original(thisPtr, actionType, actionId, targetId, extraParam, mode, comboRouteId, outOptAreaTargeted);
    }

    private RedirectionEntry? FindMatchingRedirection(uint originalId, uint adjustedId, uint jobId)
    {
        RedirectionEntry? 默认 = null;
        RedirectionEntry? 修饰 = null;
        var 存在修饰项 = false;

        foreach (var e in Configuration.Redirections)
        {
            if (e.ActionId != originalId && e.ActionId != adjustedId) continue;
            if (e.JobId != 0 && e.JobId != jobId) continue;
            if (e.Modifier != VirtualKey.NO_KEY) 存在修饰项 = true;
            if (e.Modifier == VirtualKey.NO_KEY) 默认 = e;
            else if (Services.KeyState[e.Modifier]) { 修饰 = e; break; }
        }

        if (修饰 != null) return 修饰;
        return 存在修饰项 ? null : 默认;
    }

    private IGameObject? ResolveTarget(string targetName)
    {
        var pronoun = PronounModule.Instance();
        if (pronoun == null) return null;

        return targetName switch
        {
            "UI Mouseover" => Services.ObjectTable.CreateObjectReference((nint)pronoun->UiMouseOverTarget),
            "Field Mouseover" => Services.TargetManager.MouseOverTarget,
            "Crosshair" => 取准星目标(),
            "Mouse Location" => null,
            "Target" => Services.ObjectTable.CreateObjectReference((nint)pronoun->ResolvePlaceholder("<t>", 1, 0)),
            "Focus Target" => Services.ObjectTable.CreateObjectReference((nint)pronoun->ResolvePlaceholder("<f>", 1, 0)),
            "Target of Target" => Services.ObjectTable.CreateObjectReference((nint)pronoun->ResolvePlaceholder("<tt>", 1, 0)),
            "Self" => Services.ObjectTable.CreateObjectReference((nint)pronoun->ResolvePlaceholder("<me>", 1, 0)),
            "<2>" => Services.ObjectTable.CreateObjectReference((nint)pronoun->ResolvePlaceholder("<2>", 1, 0)),
            "<3>" => Services.ObjectTable.CreateObjectReference((nint)pronoun->ResolvePlaceholder("<3>", 1, 0)),
            "<4>" => Services.ObjectTable.CreateObjectReference((nint)pronoun->ResolvePlaceholder("<4>", 1, 0)),
            "<5>" => Services.ObjectTable.CreateObjectReference((nint)pronoun->ResolvePlaceholder("<5>", 1, 0)),
            "<6>" => Services.ObjectTable.CreateObjectReference((nint)pronoun->ResolvePlaceholder("<6>", 1, 0)),
            "<7>" => Services.ObjectTable.CreateObjectReference((nint)pronoun->ResolvePlaceholder("<7>", 1, 0)),
            "<8>" => Services.ObjectTable.CreateObjectReference((nint)pronoun->ResolvePlaceholder("<8>", 1, 0)),
            _ => null
        };
    }

    private static string 规范化目标类型(string? v)
    {
        return v switch
        {
            "Model Mouseover" => "Field Mouseover",
            "Focus" => "Focus Target",
            "Cursor" => "Mouse Location",
            _ => v ?? string.Empty
        };
    }

    private IGameObject? 取准星目标()
    {
        var targetSystem = TargetSystem.Instance();
        if (targetSystem == null) return null;
        return Services.ObjectTable.CreateObjectReference((nint)targetSystem->GetMouseOverObject(Configuration.CrosshairWidth, Configuration.CrosshairHeight));
    }

    private bool ValidateTarget(Lumina.Excel.Sheets.Action action, IGameObject target)
    {
        if (!Configuration.RangeCheck)
            return true;

        var player = Services.ObjectTable.LocalPlayer;
        if (player == null) return false;

        var playerPtr = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)player.Address;
        var targetPtr = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address;

        var err = ActionManager.GetActionInRangeOrLoS(action.RowId, playerPtr, targetPtr);

        if (Configuration.IgnoreErrors)
            return err == 0 || err == 565; // 忽略面向

        if (err != 0 && err != 565)
        {
            if (!Configuration.IgnoreErrors)
            {
                if (err == 566)
                    Services.ToastGui.ShowError("目标不在视线范围内");
                else if (err == 562)
                    Services.ToastGui.ShowError("目标不在范围内");
                else
                    Services.ToastGui.ShowError("无效目标");
            }
            return false;
        }

        return true;
    }

    private bool 可用(Lumina.Excel.Sheets.Action a, IGameObject t)
    {
        var tp = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)t.Address;
        return ActionManager.CanUseActionOnTarget(a.RowId, tp);
    }

    private bool GroundActionAtCursor(ActionManager* thisPtr, ActionType actionType, uint actionId, ulong targetId, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId, Vector3* location)
    {
        if (UseActionHook == null) return false;

        var status = ActionManager.MemberFunctionPointers.GetActionStatus(thisPtr, actionType, actionId, (uint)targetId, true, true, null);

        if (status != 0 && status != 0x244)
            return UseActionHook.Original(thisPtr, actionType, actionId, targetId, extraParam, mode, comboRouteId, null);

        return UseActionLocation(thisPtr, actionType, actionId, targetId, location, extraParam);
    }

    private bool GroundActionAtTarget(ActionManager* thisPtr, ActionType actionType, uint actionId, IGameObject target, uint extraParam, ActionManager.UseActionMode mode, uint comboRouteId)
    {
        if (UseActionHook == null) return false;

        var location = target.Position;
        return UseActionLocation(thisPtr, actionType, actionId, target.GameObjectId, &location, extraParam);
    }

    public void Dispose()
    {
        UseActionHook?.Dispose();
        UseActionHook = null;
    }
}
