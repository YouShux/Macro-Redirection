using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface.Colors;

namespace MacroRedirection;

[Serializable]
public class RedirectionEntry
{
    public uint ActionId { get; set; }
    public List<string> TargetPriority { get; set; } = new();
    public VirtualKey Modifier { get; set; } = VirtualKey.NO_KEY;
    public uint JobId { get; set; } = 0;
}

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 2;

    public bool IgnoreErrors { get; set; } = true;
    public bool DefaultCursorMouseover { get; set; } = false;
    public bool EnableMacroQueueing { get; set; } = false;
    public bool QueueGroundActions { get; set; } = false;
    public bool RangeCheck { get; set; } = true;

    public bool ShowActionIcons { get; set; } = true;
    public float IconScale { get; set; } = 1.0f;
    public int IconsPerRow { get; set; } = 6;

    public int CrosshairWidth;
    public int CrosshairHeight;
    public bool DrawCrosshair;
    public float CrosshairThickness = 5.0f;
    public float CrosshairSize = 15.0f;
    public Vector4 CrosshairInvalidColor = ImGuiColors.DalamudRed;
    public Vector4 CrosshairValidColor = ImGuiColors.DalamudOrange;
    public Vector4 CrosshairCastColor = ImGuiColors.ParsedGreen;

    public List<RedirectionEntry> Redirections { get; set; } = new();

    public Configuration()
    {
        初始化准星位置();
    }

    private unsafe void 初始化准星位置()
    {
        if (CrosshairWidth != 0 || CrosshairHeight != 0) return;
        try
        {
            var dev = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance();
            CrosshairWidth = (int)dev->Width / 2;
            CrosshairHeight = (int)dev->Height / 2;
        }
        catch
        {
            CrosshairWidth = 960;
            CrosshairHeight = 540;
        }
    }

    public void Save()
    {
        Services.Interface.SavePluginConfig(this);
    }
}
