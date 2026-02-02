using System.Collections.Generic;
using System.Linq;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace MacroRedirection;

public class Actions
{
    private static readonly uint[] 占星额外技能 = [37023, 37024, 37025, 37026, 37027, 37028];
    private static readonly uint[] 召唤额外技能 = [16514u, 16515u, 25830u];

    private ExcelSheet<Action>? Sheet;
    private ExcelSheet<RawRow>? CJC;
    private readonly List<Action> RoleActions = new();
    private readonly List<uint> JobInfo = new();
    private readonly Dictionary<uint, List<Action>> Jobs = new();

    public List<uint> GetJobInfo() => JobInfo;
    public IEnumerable<Action> GetJobActions(uint job) => Jobs.TryGetValue(job, out var actions) ? actions : [];
    public IEnumerable<Action> GetRoleActions() => RoleActions;
    public Action? GetRow(uint id) => Sheet?.TryGetRow(id, out var action) == true ? action : null;

    public Actions()
    {
        try
        {
            Initialize();
        }
        catch (System.Exception ex)
        {
            Jobs.Clear();
            JobInfo.Clear();
            RoleActions.Clear();
            Services.PluginLog.Error(ex, "技能表初始化失败");
        }
    }

    private void Initialize()
    {
        Sheet = Services.DataManager.GetExcelSheet<Action>();
        if (Sheet == null) return;

        RoleActions.AddRange(Sheet.Where(a =>
            a.IsRoleAction &&
            a.ClassJobLevel != 0 &&
            !a.IsPvP &&
            HasOptionalTargeting(a)
        ));

        var jobSheet = Services.DataManager.GetExcelSheet<ClassJob>();
        if (jobSheet == null) return;
        JobInfo.AddRange(jobSheet.Where(j => j.Role > 0 && j.ItemSoulCrystal.Value.RowId > 0).Select(j => j.RowId));

        CJC = Services.DataManager.GetExcelSheet<RawRow>(name: "ClassJobCategory");
        if (CJC == null) return;

        foreach (var job in JobInfo)
        {
            var list = Sheet.Where(a =>
            {
                if (a.ClassJob.RowId == 0 || !a.IsPlayerAction || a.IsRoleAction || a.IsPvP) return false;

                var id = a.ClassJobCategory.RowId;
                return CJC.TryGetRow(id, out var cjc) && cjc.ReadBoolColumn((int)job + 1) && HasOptionalTargeting(a);
            }).ToList();

            if (job == 33)
                foreach (var id in 占星额外技能)
                    if (!list.Any(a => a.RowId == id) && Sheet.TryGetRow(id, out var a))
                        list.Add(a);

            if (job == 27)
                foreach (var id in 召唤额外技能)
                    if (!list.Any(a => a.RowId == id) && Sheet.TryGetRow(id, out var a))
                        list.Add(a);

            Jobs[job] = list;
        }
    }

    private static bool HasOptionalTargeting(Action a)
    {
        return a.CanTargetAlly || a.CanTargetHostile || a.CanTargetParty || a.TargetArea;
    }
}
