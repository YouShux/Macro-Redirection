using System;
using Dalamud.Game.Command;
using Dalamud.Plugin;

namespace MacroRedirection;

public class Plugin : IDalamudPlugin, IDisposable
{
    public string Name => "Macro Redirection";
    private const string CommandName = "/macroredirect";

    public Configuration Configuration { get; private set; }
    private PluginUI PluginUI { get; }
    private Actions Actions { get; }
    private MacroRedirectionCore Core { get; }

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        if (pluginInterface.IsDev) throw new InvalidOperationException("本插件仅支持在线安装，请从插件库安装。");
        Services.Initialize(pluginInterface);

        try
        {
            Configuration = Services.Interface.GetPluginConfig() as Configuration ?? new Configuration();
        }
        catch (Exception ex)
        {
            Services.PluginLog.Error(ex, "加载配置失败，将使用默认配置");
            Configuration = new Configuration();
        }

        Actions = new Actions();
        Core = new MacroRedirectionCore(this, Configuration, Actions);
        PluginUI = new PluginUI(Configuration, Actions);

        // Register IPC
        IPCProvider.RegisterIPC(this, Services.Interface);

        // Register commands
        Services.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "打开 Macro Redirection 配置窗口"
        });

        Services.CommandManager.AddHandler("/mr", new CommandInfo(OnCommand)
        {
            HelpMessage = "打开 Macro Redirection 配置窗口 (简写)"
        });

        Services.PluginLog.Info("Macro Redirection 插件已加载");
    }

    public void Dispose()
    {
        Core.Dispose();
        PluginUI.Dispose();
        IPCProvider.Dispose();

        Services.CommandManager.RemoveHandler(CommandName);
        Services.CommandManager.RemoveHandler("/mr");

        Configuration.Save();

        Services.PluginLog.Info("Macro Redirection 插件已卸载");
    }

    private void OnCommand(string command, string args)
    {
        PluginUI.MainWindowVisible = true;
    }

    public void OpenConfig() => PluginUI.MainWindowVisible = true;
}
