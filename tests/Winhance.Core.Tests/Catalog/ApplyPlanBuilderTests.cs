using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class ApplyPlanBuilderTests
{
    private static RegTarget Reg(string key, string valueName, params string[] paths) =>
        new(key, paths.Length == 0 ? new[] { @"HKEY_LOCAL_MACHINE\TEST" } : paths, valueName, RegistryValueKind.DWord);

    private static Setting Make(IReadOnlyList<Target> targets, params SettingState[] states) =>
        new() { Id = "t", Name = "t", Description = "t", Targets = targets, States = states };

    [Fact]
    public void Writes_concrete_value_to_each_mirror_path()
    {
        var setting = Make(
            new[] { Reg("Hide", "HideSCAMeetNow", @"HKCU\A", @"HKLM\B") },
            new SettingState { Label = "On", Set = new Dictionary<string, StateValue> { ["Hide"] = StateValue.Of(1) } });

        var plan = ApplyPlanBuilder.Build(setting, "On");

        var writes = plan.OfType<RegistryWriteOp>().ToList();
        Assert.Equal(2, writes.Count);
        Assert.Contains(writes, w => w.Path == @"HKCU\A" && (int)w.Value == 1);
        Assert.Contains(writes, w => w.Path == @"HKLM\B" && (int)w.Value == 1);
    }

    [Fact]
    public void Absent_state_emits_a_delete_per_path()
    {
        var setting = Make(
            new[] { Reg("Hide", "HideSCAMeetNow", @"HKCU\A", @"HKLM\B") },
            new SettingState { Label = "Off", Set = new Dictionary<string, StateValue> { ["Hide"] = StateValue.Absent } });

        var plan = ApplyPlanBuilder.Build(setting, "Off");

        Assert.Equal(2, plan.OfType<RegistryDeleteOp>().Count());
        Assert.Empty(plan.OfType<RegistryWriteOp>());
    }

    [Fact]
    public void OrAbsent_writes_the_concrete_value()
    {
        var setting = Make(
            new[] { Reg("Start", "Start") },
            new SettingState { Label = "Manual", Set = new Dictionary<string, StateValue> { ["Start"] = StateValue.Of(3).OrAbsent() } });

        var op = Assert.Single(ApplyPlanBuilder.Build(setting, "Manual").OfType<RegistryWriteOp>());
        Assert.Equal(3, (int)op.Value);
    }

    [Fact]
    public void Exists_state_emits_ensure_key()
    {
        var setting = Make(
            new[] { Reg("Flag", "Flag") },
            new SettingState { Label = "On", Set = new Dictionary<string, StateValue> { ["Flag"] = StateValue.Exists } });

        Assert.Single(ApplyPlanBuilder.Build(setting, "On").OfType<RegistryEnsureKeyOp>());
    }

    [Fact]
    public void Effects_are_emitted_after_registry_ops()
    {
        var setting = Make(
            new[] { Reg("Start", "Start") },
            new SettingState
            {
                Label = "Disabled",
                Set = new Dictionary<string, StateValue> { ["Start"] = StateValue.Of(4) },
                Effects = new Effect[] { new ScriptEffect("rename.ps1", RunContext.System) },
            });

        var plan = ApplyPlanBuilder.Build(setting, "Disabled");
        Assert.True(plan.Last() is EffectOp);
        Assert.Equal("rename.ps1", ((ScriptEffect)plan.OfType<EffectOp>().Single().Effect).Script);
    }

    [Fact]
    public void Task_state_emits_enable_or_disable()
    {
        var setting = Make(
            new[] { (Target)new TaskTarget("Task", @"\MS\Win\Task") },
            new SettingState { Label = "On",  Set = new Dictionary<string, StateValue> { ["Task"] = StateValue.Of(true) } },
            new SettingState { Label = "Off", Set = new Dictionary<string, StateValue> { ["Task"] = StateValue.Of(false) } });

        Assert.True(ApplyPlanBuilder.Build(setting, "On").OfType<TaskSetOp>().Single().Enabled);
        Assert.False(ApplyPlanBuilder.Build(setting, "Off").OfType<TaskSetOp>().Single().Enabled);
    }

    [Fact]
    public void Unknown_state_label_throws()
    {
        var setting = Make(new[] { Reg("K", "V") },
            new SettingState { Label = "On", Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(1) } });
        Assert.Throws<System.ArgumentException>(() => ApplyPlanBuilder.Build(setting, "Nope"));
    }

    [Fact]
    public void Fallback_partial_set_skips_uncovered_targets()
    {
        var setting = Make(
            new[] { Reg("A", "A"), Reg("B", "B") },
            new SettingState { Label = "Default", IsFallback = true, Set = new Dictionary<string, StateValue> { ["A"] = StateValue.Of(1) } });

        var plan = ApplyPlanBuilder.Build(setting, "Default");
        Assert.Single(plan.OfType<RegistryWriteOp>());
        Assert.Equal(@"HKEY_LOCAL_MACHINE\TEST", plan.OfType<RegistryWriteOp>().Single().Path);
    }
}
