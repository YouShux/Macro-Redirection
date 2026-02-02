using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;

namespace MacroRedirection;

public class PluginUI : IDisposable
{
    private const uint ICON_SIZE = 48;
    private const uint MAX_REDIRECTS = 12;

    private readonly Configuration Configuration;
    private readonly Actions Actions;

    private readonly Dictionary<ulong, VirtualKey> 修饰键选择 = new();

    private IDalamudTextureWrap? 主动技能边框;
    private bool 主动技能边框加载失败;

    internal bool MainWindowVisible = false;
    private bool SelectedRoleActions = false;
    private uint SelectedJob;
    private string searchText = string.Empty;
    private string 预设提示 = string.Empty;
    private string 剪贴板提示 = string.Empty;

    private readonly string[] TargetOptions =
    {
        "UI Mouseover", "Field Mouseover", "Crosshair", "Mouse Location", "Target", "Focus Target",
        "Target of Target", "Self",
        "<2>", "<3>", "<4>", "<5>", "<6>", "<7>", "<8>",
    };

    public PluginUI(Configuration config, Actions actions)
    {
        Configuration = config;
        Actions = actions;

        Services.Interface.UiBuilder.Draw += Draw;
        Services.Interface.UiBuilder.OpenMainUi += OnOpenConfig;
        Services.Interface.UiBuilder.OpenConfigUi += OnOpenConfig;
    }

    private void OnOpenConfig()
    {
        MainWindowVisible = true;
    }

    public void Dispose()
    {
        Services.Interface.UiBuilder.OpenMainUi -= OnOpenConfig;
        Services.Interface.UiBuilder.OpenConfigUi -= OnOpenConfig;
        Services.Interface.UiBuilder.Draw -= Draw;
        主动技能边框?.Dispose();
    }

    public void Draw()
    {
        绘制准星();
        if (!MainWindowVisible) return;

        ImGui.SetNextWindowSize(new Vector2(800, 600), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin("Macro Redirection 配置", ref MainWindowVisible))
        {
            ImGui.End();
            return;
        }

        if (ImGui.BeginTabBar("##主标签"))
        {
            if (ImGui.BeginTabItem("主页"))
            {
                绘制主页();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("设置"))
            {
                绘制设置();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("预设"))
            {
                绘制预设();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("关于"))
            {
                绘制关于();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.End();
    }

    private void 绘制设置()
    {
        var ignoreErrors = Configuration.IgnoreErrors;
        if (ImGui.Checkbox("忽略施法报错", ref ignoreErrors))
        {
            Configuration.IgnoreErrors = ignoreErrors;
            Configuration.Save();
        }

        var rangeCheck = Configuration.RangeCheck;
        if (ImGui.Checkbox("启用范围检查，避免超出最大技能距离导致无法使用", ref rangeCheck))
        {
            Configuration.RangeCheck = rangeCheck;
            Configuration.Save();
        }

        var cursorMo = Configuration.DefaultCursorMouseover;
        if (ImGui.Checkbox("地面技能和位移技能，鼠标悬停改成鼠标位置直接施法", ref cursorMo))
        {
            Configuration.DefaultCursorMouseover = cursorMo;
            Configuration.Save();
        }

        var queueGround = Configuration.QueueGroundActions;
        if (ImGui.Checkbox("允许地面技能排队", ref queueGround))
        {
            Configuration.QueueGroundActions = queueGround;
            Configuration.Save();
        }

        var macroQueue = Configuration.EnableMacroQueueing;
        if (ImGui.Checkbox("允许宏技能排队", ref macroQueue))
        {
            Configuration.EnableMacroQueueing = macroQueue;
            Configuration.Save();
        }

        var showIcons = Configuration.ShowActionIcons;
        if (ImGui.Checkbox("显示技能图标", ref showIcons))
        {
            Configuration.ShowActionIcons = showIcons;
            Configuration.Save();
        }

        var iconScale = Configuration.IconScale;
        if (ImGui.SliderFloat("图标缩放", ref iconScale, 0.5f, 2.0f))
        {
            Configuration.IconScale = iconScale;
            Configuration.Save();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("准星位置（需要你用覆盖层自己画出来）");
        ImGui.SetNextItemWidth(100);
        if (ImGui.InputInt("X 坐标", ref Configuration.CrosshairWidth))
            Configuration.Save();
        ImGui.SetNextItemWidth(100);
        if (ImGui.InputInt("Y 坐标", ref Configuration.CrosshairHeight))
            Configuration.Save();

        var drawCrosshair = Configuration.DrawCrosshair;
        if (ImGui.Checkbox("显示准星", ref drawCrosshair))
        {
            Configuration.DrawCrosshair = drawCrosshair;
            Configuration.Save();
        }

        if (Configuration.DrawCrosshair)
        {
            ImGui.Indent(10);
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputFloat("大小", ref Configuration.CrosshairSize))
                Configuration.Save();
            ImGui.SetNextItemWidth(100);
            if (ImGui.InputFloat("线宽", ref Configuration.CrosshairThickness))
                Configuration.Save();

            绘制颜色重置("无目标", ref Configuration.CrosshairInvalidColor, ImGuiColors.DalamudRed);
            绘制颜色重置("已捕获目标", ref Configuration.CrosshairValidColor, ImGuiColors.DalamudOrange);
            绘制颜色重置("施法锁定", ref Configuration.CrosshairCastColor, ImGuiColors.ParsedGreen);
            ImGui.Unindent(10);
        }
        
        ImGui.Spacing();
        ImGui.TextWrapped("说明：");
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextWrapped("列表 鼠标悬停：顾名思义，鼠标指向列表、小队列表的后，可以对该目标施法，理论上也支持团队列表");
        ImGui.TextWrapped("场景 鼠标悬停：视野范围内鼠标指向哪个目标就是哪个目标，类似于游戏的mo宏");
        ImGui.TextWrapped("准心：设置里开启准心开关，对着准心目标施法");
        ImGui.TextWrapped("鼠标位置：地面技能、位移技能都可以用，比如地星、庇护所、缩地、调停等，会直接在鼠标位置直接施法，\n游戏内是需要二次使用技能才能在鼠标位置释放");
        
        ImGui.Spacing();
        ImGui.TextWrapped("特殊说明：");
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.TextWrapped("鼠标悬停就是各种需要二次施法的技能，保持和游戏一致，设置里可以勾选鼠标悬停直接施法，这样子就变成了游戏里的gtoff宏！");
        ImGui.TextWrapped("随等级变化的技能默认是自动适配其余等级的技能，也就是说，像白魔的崩石建了一个预设，崩石这个系列的技能都会生效，不需要每个都改一次！" +
                          "另外本插件的初衷是为了我的奶妈ACR服务的，所以我联动了我的ACR！");
        ImGui.TextWrapped("特别注意：如果选择开启Macro Redirection，AE等自动输出插件的输出目标会跟随插件设置一起变化。" +
                          "\n并不是只有你手动释放的技能才会被重定向，自动释放的也会！我的ACR里内置了开关控制MR是否影响ACR自动输出时的目标重定向，" +
                          "\n或者你可以在MR插件内绑定一个按键用于其余ACR是否要启用重定向的功能！");
    }

    private void 绘制主页()
    {
        ImGui.Columns(2, "main_columns", true);
        ImGui.SetColumnWidth(0, 200);
        DrawJobSelection();
        ImGui.NextColumn();
        DrawActionList();
        ImGui.Columns(1);
    }

    private void 绘制预设()
    {
        ImGui.TextUnformatted("这里是一些对新手友好的预设，可以一键导入。");
        ImGui.Spacing();

        if (ImGui.Button("导入：位移技能")) 导入预设("位移技能", "位移技能预设.json");
        if (ImGui.Button("导入：奶妈基础技能")) 导入预设("奶妈基础技能", "奶妈基础技能.json");

        if (!string.IsNullOrEmpty(预设提示))
        {
            ImGui.Spacing();
            ImGui.TextUnformatted(预设提示);
        }
    }

    private void 绘制关于()
    {
        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DPSRed);
        ImGui.TextWrapped("本插件完全开源免费，从未委托任何人在任何渠道售卖。\n如果你是付费购买的本插件，请立即退款并差评举报。");
        ImGui.PopStyleColor();

        ImGui.Spacing();
        ImGui.TextUnformatted("插件主页：");
        ImGui.SameLine();
        ImGui.TextUnformatted("https://github.com/YouShux/DalamudPlugins");

        ImGui.Spacing();
        ImGui.TextUnformatted("反馈频道：");
        ImGui.SameLine();
        ImGui.TextUnformatted("https://discord.gg/gf7zz84q73");

        ImGui.Spacing();
        if (ImGui.Button("打开插件主页"))
            Util.OpenLink("https://github.com/YouShux/DalamudPlugins");

        ImGui.SameLine();
        if (ImGui.Button("问题反馈"))
            Util.OpenLink("https://discord.gg/gf7zz84q73");

        ImGui.SameLine();
        if (ImGui.Button("爱发电"))
            Util.OpenLink("https://www.ifdian.net/a/youshu");
    }

    private static void 绘制颜色重置(string label, ref Vector4 color, Vector4 默认)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.SameLine();
        ImGui.ColorEdit4($"##{label}", ref color, ImGuiColorEditFlags.NoInputs);
        ImGui.SameLine();
        if (ImGui.Button($"重置##{label}")) color = 默认;
    }

    private unsafe void 绘制准星()
    {
        if (!Configuration.DrawCrosshair) return;

        var player = Services.ObjectTable.LocalPlayer;
        if (player == null) return;

        var drawlist = ImGui.GetBackgroundDrawList();
        var center = new Vector2(Configuration.CrosshairWidth, Configuration.CrosshairHeight);

        var upper = center with { Y = center.Y - Configuration.CrosshairSize };
        var lower = center with { Y = center.Y + Configuration.CrosshairSize };
        var left = center with { X = center.X - Configuration.CrosshairSize };
        var right = center with { X = center.X + Configuration.CrosshairSize };

        var targetSystem = TargetSystem.Instance();
        var acquired = targetSystem != null && Services.ObjectTable.CreateObjectReference((nint)targetSystem->GetMouseOverObject(Configuration.CrosshairWidth, Configuration.CrosshairHeight))?.IsValid() == true;

        var c = player.IsCasting
            ? Configuration.CrosshairCastColor
            : acquired ? Configuration.CrosshairValidColor : Configuration.CrosshairInvalidColor;

        var color = ImGui.ColorConvertFloat4ToU32(c);
        drawlist.AddLine(upper, lower, color, Configuration.CrosshairThickness);
        drawlist.AddLine(left, right, color, Configuration.CrosshairThickness);
    }

    private void 导入预设(string 名字, string 文件名)
    {
        try
        {
            var asm = typeof(PluginUI).Assembly;
            var 资源名 = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(文件名, StringComparison.OrdinalIgnoreCase));
            string json;
            if (资源名 != null)
            {
                using var s = asm.GetManifestResourceStream(资源名);
                if (s == null) throw new FileNotFoundException("预设文件不存在", 文件名);
                using var sr = new StreamReader(s);
                json = sr.ReadToEnd();
            }
            else
            {
                var dir = Services.Interface.AssemblyLocation.DirectoryName;
                var path1 = dir == null ? null : Path.Combine(dir, "Preset", 文件名);
                if (path1 == null || !File.Exists(path1)) throw new FileNotFoundException("预设文件不存在", 文件名);
                json = File.ReadAllText(path1);
            }
            var list = JsonConvert.DeserializeObject<List<MOAction预设项>>(json) ?? new List<MOAction预设项>();
            foreach (var item in list)
            {
                var targets = item.Stack.Select(s => s.Item1).ToList();
                var entry = Configuration.Redirections.FirstOrDefault(e => e.ActionId == item.BaseId && e.JobId == item.JobIdx && e.Modifier == item.Modifier);
                if (entry == null)
                {
                    entry = new RedirectionEntry { ActionId = item.BaseId, JobId = item.JobIdx, Modifier = item.Modifier, TargetPriority = targets };
                    Configuration.Redirections.Add(entry);
                }
                else
                {
                    entry.TargetPriority = targets;
                }
            }

            Configuration.Save();
            预设提示 = $"已导入：{名字}";
        }
        catch (Exception e)
        {
            Services.PluginLog.Error(e, "导入预设失败");
            预设提示 = "导入失败";
        }
    }

    private class MOAction预设项
    {
        public uint BaseId { get; set; }
        public List<(string, uint)> Stack { get; set; } = new();
        public VirtualKey Modifier { get; set; }
        public uint JobIdx { get; set; }
    }

    private void DrawJobSelection()
    {
        if (ImGui.BeginChild("job_selection", new Vector2(0, 0), true))
        {
            if (ImGui.Selectable("职能技能", SelectedRoleActions))
            {
                SelectedRoleActions = true;
                SelectedJob = 0;
            }

            ImGui.Separator();

            var jobs = Actions.GetJobInfo();
            var jobSheet = Services.DataManager.GetExcelSheet<ClassJob>()!;

            var 顺序 = new uint[]
            {
                19, 21, 32, 37,
                24, 28, 33, 40,
                20, 22, 30, 34, 39, 41,
                23, 31, 38,
                25, 27, 35, 42, 36,
            };
            var 顺序表 = new Dictionary<uint, int>(顺序.Length);
            for (int i = 0; i < 顺序.Length; i++) 顺序表[顺序[i]] = i;
            var 排好序 = jobs.OrderBy(id => 顺序表.TryGetValue(id, out var idx) ? idx : 999 + (int)id);

            foreach (var jobId in 排好序)
            {
                if (!jobSheet.TryGetRow(jobId, out var job))
                    continue;

                if (ImGui.Selectable(job.Name.ExtractText(), SelectedJob == jobId))
                {
                    SelectedJob = jobId;
                    SelectedRoleActions = false;
                }
            }

            ImGui.EndChild();
        }
    }

    private void DrawActionList()
    {
        if (!SelectedRoleActions && SelectedJob == 0)
        {
            const string text = "请选择一个职业进行配置";
            var size = ImGui.CalcTextSize(text);

            ImGui.Columns(1);
            var min = ImGui.GetWindowContentRegionMin();
            var max = ImGui.GetWindowContentRegionMax();
            var x = (min.X + max.X - size.X) * 0.5f;
            var y = (min.Y + max.Y - size.Y) * 0.5f;
            ImGui.SetCursorPos(new Vector2(x < min.X ? min.X : x, y < min.Y ? min.Y : y));
            ImGui.TextUnformatted(text);
            return;
        }

        ImGui.PushItemWidth(-1);
        ImGui.InputTextWithHint("##search", "搜索技能...", ref searchText, 250);
        ImGui.PopItemWidth();

        if (ImGui.Button("导出预设到剪贴板")) 导出预设到剪贴板();
        ImGui.SameLine();
        if (ImGui.Button("从剪贴板导入预设")) 从剪贴板导入预设();
        ImGui.SameLine();
        if (CtrlShift删除按钮("一键清除所有配置", "按住 Ctrl+Shift 才能清除所有配置。")) 清除所有配置();
        if (!string.IsNullOrEmpty(剪贴板提示)) ImGui.TextUnformatted(剪贴板提示);

        ImGui.Spacing();

        if (ImGui.BeginChild("action_list", new Vector2(0, 0), true))
        {
            var 奶妈 = !SelectedRoleActions && (SelectedJob == 24 || SelectedJob == 28 || SelectedJob == 33 || SelectedJob == 40);
            var actions = SelectedRoleActions ? Actions.GetRoleActions() : Actions.GetJobActions(SelectedJob);

            var filtered = actions.Where(a =>
                (string.IsNullOrEmpty(searchText) ||
                 a.Name.ExtractText().Contains(searchText, StringComparison.OrdinalIgnoreCase)) &&
                !(奶妈 && a.Name.ExtractText() == "净化") &&
                !a.IsPvP
            );

            foreach (var action in filtered)
            {
                DrawActionEntry(action);
            }

            ImGui.EndChild();
        }
    }

    private void 导出预设到剪贴板()
    {
        try
        {
            var list = Configuration.Redirections
                .OrderBy(e => e.JobId)
                .ThenBy(e => e.ActionId)
                .ThenBy(e => (int)e.Modifier)
                .Select(e => new MOAction预设项
                {
                    BaseId = e.ActionId,
                    JobIdx = e.JobId,
                    Modifier = e.Modifier,
                    Stack = e.TargetPriority.Select(t => (t, 0u)).ToList(),
                })
                .ToList();

            var json = JsonConvert.SerializeObject(list, Formatting.None);
            ImGui.SetClipboardText(json);
            剪贴板提示 = $"已导出到剪贴板：{list.Count} 条";
        }
        catch (Exception e)
        {
            Services.PluginLog.Error(e, "导出预设到剪贴板失败");
            剪贴板提示 = "导出失败";
        }
    }

    private void 从剪贴板导入预设()
    {
        try
        {
            var json = ImGui.GetClipboardText();
            if (string.IsNullOrWhiteSpace(json))
            {
                剪贴板提示 = "剪贴板为空";
                return;
            }

            var list = JsonConvert.DeserializeObject<List<MOAction预设项>>(json) ?? new List<MOAction预设项>();
            foreach (var item in list)
            {
                if (item.BaseId == 0) continue;
                var targets = (item.Stack ?? new List<(string, uint)>()).Select(s => s.Item1).Where(s => !string.IsNullOrEmpty(s)).ToList();
                var entry = Configuration.Redirections.FirstOrDefault(e => e.ActionId == item.BaseId && e.JobId == item.JobIdx && e.Modifier == item.Modifier);
                if (entry == null)
                {
                    entry = new RedirectionEntry { ActionId = item.BaseId, JobId = item.JobIdx, Modifier = item.Modifier, TargetPriority = targets };
                    Configuration.Redirections.Add(entry);
                }
                else
                {
                    entry.TargetPriority = targets;
                }
            }

            Configuration.Save();
            剪贴板提示 = $"已从剪贴板导入：{list.Count} 条";
        }
        catch (Exception e)
        {
            Services.PluginLog.Error(e, "从剪贴板导入预设失败");
            剪贴板提示 = "导入失败";
        }
    }

    private void 清除所有配置()
    {
        var n = Configuration.Redirections.Count;
        if (n <= 0)
        {
            剪贴板提示 = "没有可清除的配置";
            return;
        }

        Configuration.Redirections.Clear();
        修饰键选择.Clear();
        Configuration.Save();
        剪贴板提示 = $"已清除：{n} 条";
    }

    private void DrawActionEntry(Lumina.Excel.Sheets.Action action)
    {
        if (Configuration.ShowActionIcons)
        {
            DrawIcon(action.Icon, new Vector2(ICON_SIZE * Configuration.IconScale));
            ImGui.SameLine();
        }

        ImGui.BeginGroup();
        var jobId = SelectedRoleActions ? 0 : SelectedJob;
        var key = ((ulong)jobId << 32) | action.RowId;
        if (!修饰键选择.TryGetValue(key, out var 当前修饰键))
        {
            var 所有 = Configuration.Redirections.Where(e => e.ActionId == action.RowId && e.JobId == jobId).ToList();
            当前修饰键 = 所有.Any(e => e.Modifier == VirtualKey.NO_KEY) ? VirtualKey.NO_KEY : (所有.FirstOrDefault()?.Modifier ?? VirtualKey.NO_KEY);
            修饰键选择[key] = 当前修饰键;
        }

        ImGui.Text(action.Name.ExtractText());
        var entry = Configuration.Redirections.FirstOrDefault(e => e.ActionId == action.RowId && e.JobId == jobId && e.Modifier == 当前修饰键);
        if (entry == null)
        {
            entry = Configuration.Redirections.FirstOrDefault(e => e.ActionId == action.RowId && e.JobId == jobId);
            if (entry != null)
            {
                当前修饰键 = entry.Modifier;
                修饰键选择[key] = 当前修饰键;
            }
        }
        var hasRedirection = entry != null;

        if (!hasRedirection && ImGui.Button($"添加重定向##{action.RowId}"))
        {
            entry = new RedirectionEntry
            {
                ActionId = action.RowId,
                JobId = jobId,
                Modifier = 当前修饰键
            };
            Configuration.Redirections.Add(entry);

            if (entry != null && entry.TargetPriority.Count == 0)
            {
                entry.TargetPriority.Add("UI Mouseover");
            }
            Configuration.Save();
        }

        if (hasRedirection && entry != null)
        {
            DrawRedirectionSettings(action, entry, key);
        }

        ImGui.EndGroup();
        ImGui.Separator();
    }

    private static bool 绘制修饰键按钮(uint actionId, uint jobId, ref VirtualKey modifier)
    {
        var popup = $"##修饰键_{jobId}_{actionId}";
        var changed = false;
        if (ImGui.Button($"按住的控制键：{取修饰键名(modifier)}{popup}"))
            ImGui.OpenPopup(popup);

        if (ImGui.BeginPopup(popup))
        {
            changed |= 选修饰键("不需要", VirtualKey.NO_KEY, ref modifier);
            changed |= 选修饰键("Shift", VirtualKey.SHIFT, ref modifier);
            changed |= 选修饰键("Alt", VirtualKey.MENU, ref modifier);
            changed |= 选修饰键("Ctrl", VirtualKey.CONTROL, ref modifier);
            ImGui.EndPopup();
        }

        return changed;
    }

    private static bool 选修饰键(string label, VirtualKey key, ref VirtualKey modifier)
    {
        if (!ImGui.Selectable(label, modifier == key)) return false;
        modifier = key;
        return true;
    }

    private static string 取修饰键名(VirtualKey key)
    {
        return key switch
        {
            VirtualKey.NO_KEY => "无",
            VirtualKey.MENU => "Alt",
            VirtualKey.CONTROL => "Ctrl",
            _ => key.ToString()
        };
    }

    private void DrawRedirectionSettings(Lumina.Excel.Sheets.Action action, RedirectionEntry entry, ulong key)
    {
        ImGui.TextUnformatted("目标优先级:");

        var 按钮边长 = ImGui.GetFrameHeight() + 2;
        var 按钮尺寸 = new Vector2(按钮边长, 按钮边长);

        int toRemove = -1;
        var swapA = -1;
        var swapB = -1;
        for (int i = 0; i < entry.TargetPriority.Count; i++)
        {
            ImGui.PushID($"tp_{action.RowId}_{i}");
            ImGui.PushItemWidth(200);
            var current = entry.TargetPriority[i];
            var currentKey = 规范化目标类型(current);

            if (ImGui.BeginCombo($"##target_{action.RowId}_{i}", 文本.取(currentKey, currentKey)))
            {
                foreach (var option in TargetOptions)
                {
                    var 禁用 = option == "Mouse Location" && !action.TargetArea;
                    if (禁用) ImGui.BeginDisabled();
                    if (ImGui.Selectable($"{文本.取(option, option)}##{option}", current == option) && !禁用)
                    {
                        entry.TargetPriority[i] = option;
                        Configuration.Save();
                    }
                    if (禁用) ImGui.EndDisabled();
                }
                ImGui.EndCombo();
            }
            ImGui.PopItemWidth();

            ImGui.SameLine();
            if (ImGui.Button($"X##{action.RowId}_{i}", 按钮尺寸))
            {
                toRemove = i;
            }

            ImGui.SameLine(0, 2);
            if (i == 0) ImGui.BeginDisabled();
            if (ImGui.Button($"↑##{action.RowId}_{i}", 按钮尺寸))
            {
                swapA = i;
                swapB = i - 1;
            }
            if (i == 0) ImGui.EndDisabled();

            ImGui.SameLine(0, 2);
            if (i + 1 >= entry.TargetPriority.Count) ImGui.BeginDisabled();
            if (ImGui.Button($"↓##{action.RowId}_{i}", 按钮尺寸))
            {
                swapA = i;
                swapB = i + 1;
            }
            if (i + 1 >= entry.TargetPriority.Count) ImGui.EndDisabled();

            ImGui.PopID();
        }

        if (toRemove >= 0)
        {
            entry.TargetPriority.RemoveAt(toRemove);
            Configuration.Save();
        }

        if (toRemove < 0 && swapA >= 0 && swapB >= 0 && swapA < entry.TargetPriority.Count && swapB < entry.TargetPriority.Count)
        {
            (entry.TargetPriority[swapA], entry.TargetPriority[swapB]) = (entry.TargetPriority[swapB], entry.TargetPriority[swapA]);
            Configuration.Save();
        }

        var 修饰键 = entry.Modifier;
        if (绘制修饰键按钮(action.RowId, entry.JobId, ref 修饰键))
        {
            修饰键选择[key] = 修饰键;
            var 切换 = Configuration.Redirections.FirstOrDefault(e => e.ActionId == entry.ActionId && e.JobId == entry.JobId && e.Modifier == 修饰键);
            if (切换 == null)
            {
                切换 = new RedirectionEntry
                {
                    ActionId = entry.ActionId,
                    JobId = entry.JobId,
                    Modifier = 修饰键,
                    TargetPriority = entry.TargetPriority.ToList()
                };
                Configuration.Redirections.Add(切换);
            }
            entry = 切换;
            Configuration.Save();
        }

        var 可加 = entry.TargetPriority.Count < MAX_REDIRECTS;
        ImGui.SameLine();
        if (!可加) ImGui.BeginDisabled();
        if (ImGui.Button($"添加目标##{action.RowId}"))
        {
            if (可加)
            {
                entry.TargetPriority.Add("UI Mouseover");
                Configuration.Save();
            }
        }
        if (!可加) ImGui.EndDisabled();

        ImGui.SameLine();
        if (CtrlShift删除按钮($"删除##{action.RowId}", "按住 Ctrl+Shift 才能删除。"))
        {
            Configuration.Redirections.Remove(entry);
            修饰键选择.Remove(key);
            Configuration.Save();
        }
    }

    private static bool CtrlShift删除按钮(string label, string tip)
    {
        var io = ImGui.GetIO();
        var ok = io.KeyCtrl && io.KeyShift;

        if (!ok) ImGui.BeginDisabled();
        var clicked = ImGui.Button(label);
        if (!ok) ImGui.EndDisabled();

        if (!string.IsNullOrEmpty(tip) && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(tip);
            ImGui.EndTooltip();
        }

        return clicked && ok;
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

    private void DrawIcon(ushort iconId, Vector2 size)
    {
        size = new Vector2(MathF.Max(1f, MathF.Abs(size.X)), MathF.Max(1f, MathF.Abs(size.Y)));
        var icon = new GameIconLookup(iconId);
        var texture = Services.TextureProvider.GetFromGameIcon(icon);
        var wrap = texture.GetWrapOrDefault();

        var pos = ImGui.GetCursorScreenPos();
        if (wrap != null) ImGui.Image(wrap.Handle, size);
        else ImGui.Dummy(size);

        var 框 = 取主动技能边框();
        if (框 != null)
        {
            var 边距 = MathF.Max(1f, size.X * 0.072f);
            var p = new Vector2(边距, 边距);
            ImGui.GetWindowDrawList().AddImage(框.Handle, pos - p, pos + size + p);
        }
    }

    private IDalamudTextureWrap? 取主动技能边框()
    {
        if (主动技能边框 != null || 主动技能边框加载失败) return 主动技能边框;

        try
        {
            var dir = Services.Interface.AssemblyLocation.DirectoryName;
            var path0 = dir == null ? null : Path.Combine(dir, "images", "主动技能边框.png");
            var path1 = dir == null ? null : Path.Combine(dir, "主动技能边框.png");
            var path2 = dir == null ? null : Path.Combine(dir, "Resources", "主动技能边框.png");
            const string path3 = "d:/ACR/自创插件/Macro Redirection/Resources/主动技能边框.png";
            var path = (path0 != null && File.Exists(path0)) ? path0 : (path1 != null && File.Exists(path1)) ? path1 : (path2 != null && File.Exists(path2)) ? path2 : File.Exists(path3) ? path3 : null;
            if (path == null)
            {
                主动技能边框加载失败 = true;
                return null;
            }

            using var stream = File.OpenRead(path);
            主动技能边框 = Services.TextureProvider.CreateFromImageAsync(stream).Result;
            return 主动技能边框;
        }
        catch
        {
            主动技能边框加载失败 = true;
            return null;
        }
    }
}
