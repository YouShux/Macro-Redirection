using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;

namespace MacroRedirection;

public static class IPCProvider
{
    private static Plugin? Plugin;
    internal static bool ACR重定向禁用;
    internal static bool ACR施法中;
    private static ICallGateProvider<uint[]>? RetargetedActionsProvider;
    private static ICallGateProvider<uint, bool>? IsActionRetargetedProvider;
    private static ICallGateProvider<uint, string[]>? GetActionTargetsProvider;
    private static ICallGateProvider<string>? PingProvider;
    private static ICallGateProvider<object?>? OpenConfigProvider;
    private static ICallGateProvider<bool, bool>? SetACRRedirectionDisabledProvider;
    private static ICallGateProvider<bool, bool>? SetACRExecutingProvider;

    public static void RegisterIPC(Plugin plugin, IDalamudPluginInterface pluginInterface)
    {
        Plugin = plugin;

        // 注册 IPC 接口：获取所有重定向的技能ID列表
        RetargetedActionsProvider = pluginInterface.GetIpcProvider<uint[]>("MacroRedirection.RetargetedActions");
        RetargetedActionsProvider.RegisterFunc(GetRetargetedActions);

        // 注册 IPC 接口：检查某个技能是否被重定向
        IsActionRetargetedProvider = pluginInterface.GetIpcProvider<uint, bool>("MacroRedirection.IsActionRetargeted");
        IsActionRetargetedProvider.RegisterFunc(IsActionRetargeted);

        // 注册 IPC 接口：获取某个技能的目标优先级列表
        GetActionTargetsProvider = pluginInterface.GetIpcProvider<uint, string[]>("MacroRedirection.GetActionTargets");
        GetActionTargetsProvider.RegisterFunc(GetActionTargets);

        PingProvider = pluginInterface.GetIpcProvider<string>("MacroRedirection.Ping");
        PingProvider.RegisterFunc(Ping);

        OpenConfigProvider = pluginInterface.GetIpcProvider<object?>("MacroRedirection.OpenConfig");
        OpenConfigProvider.RegisterFunc(OpenConfig);

        SetACRRedirectionDisabledProvider = pluginInterface.GetIpcProvider<bool, bool>("MacroRedirection.SetACRRedirectionDisabled");
        SetACRRedirectionDisabledProvider.RegisterFunc(SetACRRedirectionDisabled);

        SetACRExecutingProvider = pluginInterface.GetIpcProvider<bool, bool>("MacroRedirection.SetACRExecuting");
        SetACRExecutingProvider.RegisterFunc(SetACRExecuting);
    }

    private static string Ping() => typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "";

    private static object? OpenConfig()
    {
        Plugin?.OpenConfig();
        return null;
    }

    private static uint[] GetRetargetedActions()
    {
        if (Plugin == null)
            return Array.Empty<uint>();

        HashSet<uint> actionIds = new();
        foreach (var entry in Plugin.Configuration.Redirections)
        {
            actionIds.Add(entry.ActionId);
        }

        return actionIds.ToArray();
    }

    private static bool IsActionRetargeted(uint actionId)
    {
        if (Plugin == null)
            return false;

        return Plugin.Configuration.Redirections.Any(e => e.ActionId == actionId);
    }

    private static string[] GetActionTargets(uint actionId)
    {
        if (Plugin == null)
            return Array.Empty<string>();

        var entry = Plugin.Configuration.Redirections.FirstOrDefault(e => e.ActionId == actionId);
        return entry?.TargetPriority.ToArray() ?? Array.Empty<string>();
    }

    private static bool SetACRRedirectionDisabled(bool disabled)
    {
        ACR重定向禁用 = disabled;
        return true;
    }

    private static bool SetACRExecuting(bool executing)
    {
        ACR施法中 = executing;
        return true;
    }

    public static void Dispose()
    {
        RetargetedActionsProvider?.UnregisterFunc();
        IsActionRetargetedProvider?.UnregisterFunc();
        GetActionTargetsProvider?.UnregisterFunc();
        PingProvider?.UnregisterFunc();
        OpenConfigProvider?.UnregisterFunc();
        SetACRRedirectionDisabledProvider?.UnregisterFunc();
        SetACRExecutingProvider?.UnregisterFunc();
        Plugin = null;
    }
}
