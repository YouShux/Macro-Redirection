using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace MacroRedirection;

public class Services
{
    [PluginService] public static IDalamudPluginInterface Interface { get; private set; } = null!;
    [PluginService] public static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IDataManager DataManager { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static IToastGui ToastGui { get; private set; } = null!;
    [PluginService] public static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] public static IGameInteropProvider InteropProvider { get; private set; } = null!;
    [PluginService] public static IKeyState KeyState { get; private set; } = null!;
    [PluginService] public static ISigScanner SigScanner { get; private set; } = null!;

    public static void Initialize(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<Services>();
    }
}

public static class 文本
{
    private static readonly Dictionary<string, string> 表 = new()
    {
        ["UI Mouseover"] = "列表 鼠标悬停",
        ["Field Mouseover"] = "场景 鼠标悬停",
        ["Mouse Location"] = "鼠标位置",
        ["Crosshair"] = "准星",
        ["Target"] = "当前目标",
        ["Focus Target"] = "焦点目标",
        ["Target of Target"] = "目标的目标",
        ["Self"] = "自己",
        ["<2>"] = "队友2",
        ["<3>"] = "队友3",
        ["<4>"] = "队友4",
        ["<5>"] = "队友5",
        ["<6>"] = "队友6",
        ["<7>"] = "队友7",
        ["<8>"] = "队友8",
    };

    public static string 取(string? key, string? fallback = null)
    {
        if (string.IsNullOrEmpty(key)) return fallback ?? string.Empty;
        return 表.TryGetValue(key, out var v) ? v : (fallback ?? key);
    }
}
