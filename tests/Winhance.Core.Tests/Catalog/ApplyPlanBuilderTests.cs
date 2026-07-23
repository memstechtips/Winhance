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
        new() { Id = "t", Display = new() { Name = "t", Description = "t" }, Targets = targets, States = states };

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
    public void Lockable_target_unlocks_before_and_locks_after_only_the_protective_value()
    {
        var setting = Make(
            new Target[] { new RegTarget("Start", new[] { @"HKLM\Svc" }, "Start", RegistryValueKind.DWord) { LockWhenValue = 4 } },
            new SettingState { Label = "Disabled", Set = new Dictionary<string, StateValue> { ["Start"] = StateValue.Of(4) } },
            new SettingState { Label = "Manual",   Set = new Dictionary<string, StateValue> { ["Start"] = StateValue.Of(3) } });

        // The protective value (4): unlock, write, lock — in order.
        Assert.Collection(ApplyPlanBuilder.Build(setting, "Disabled"),
            op => Assert.IsType<RegistryUnlockKeyOp>(op),
            op => Assert.IsType<RegistryWriteOp>(op),
            op => Assert.IsType<RegistryLockKeyOp>(op));

        // A non-protective value (3): unlock + write, but NO lock.
        Assert.Collection(ApplyPlanBuilder.Build(setting, "Manual"),
            op => Assert.IsType<RegistryUnlockKeyOp>(op),
            op => Assert.IsType<RegistryWriteOp>(op));
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
    public void Bit_target_emits_a_bit_set_op_keyed_off_the_payload()
    {
        var reg = Reg("Settings", "Settings", @"HKCU\CabinetState") with { ByteIndex = 4, BitMask = 0x20 };
        var setting = Make(
            new[] { (Target)reg },
            new SettingState { Label = "On",  Set = new Dictionary<string, StateValue> { ["Settings"] = StateValue.Of(1) } },
            new SettingState { Label = "Off", Set = new Dictionary<string, StateValue> { ["Settings"] = StateValue.Of(0) } });

        var on = Assert.Single(ApplyPlanBuilder.Build(setting, "On").OfType<RegistryBitSetOp>());
        Assert.Equal(4, on.ByteIndex);
        Assert.Equal((byte)0x20, on.BitMask);
        Assert.True(on.Set);
        Assert.Empty(ApplyPlanBuilder.Build(setting, "On").OfType<RegistryWriteOp>());

        Assert.False(Assert.Single(ApplyPlanBuilder.Build(setting, "Off").OfType<RegistryBitSetOp>()).Set);
    }

    [Fact]
    public void String_flag_target_emits_a_flag_set_op_keyed_off_the_payload()
    {
        var reg = Reg("Flags", "Flags", @"HKCU\MouseKeys") with { StringFlagMask = 0x04, StringFlagAbsentBase = 62 };
        var setting = Make(
            new[] { (Target)reg },
            new SettingState { Label = "On",  Set = new Dictionary<string, StateValue> { ["Flags"] = StateValue.Of(true) } },
            new SettingState { Label = "Off", Set = new Dictionary<string, StateValue> { ["Flags"] = StateValue.Of(false) } });

        var on = Assert.Single(ApplyPlanBuilder.Build(setting, "On").OfType<RegistryStringFlagSetOp>());
        Assert.Equal(0x04, on.FlagMask);
        Assert.Equal(62, on.AbsentBase);
        Assert.True(on.Set);
        Assert.Empty(ApplyPlanBuilder.Build(setting, "On").OfType<RegistryWriteOp>());

        Assert.False(Assert.Single(ApplyPlanBuilder.Build(setting, "Off").OfType<RegistryStringFlagSetOp>()).Set);
    }

    [Fact]
    public void ByteOnly_target_emits_a_byte_set_op_with_the_payload_byte()
    {
        var reg = Reg("Settings", "Settings", @"HKCU\StuckRects3") with { ByteIndex = 8, ByteOnly = true };
        var setting = Make(
            new[] { (Target)reg },
            new SettingState { Label = "On",  Set = new Dictionary<string, StateValue> { ["Settings"] = StateValue.Of(3) } },
            new SettingState { Label = "Off", Set = new Dictionary<string, StateValue> { ["Settings"] = StateValue.Of(2) } });

        var on = Assert.Single(ApplyPlanBuilder.Build(setting, "On").OfType<RegistryByteSetOp>());
        Assert.Equal(8, on.ByteIndex);
        Assert.Equal((byte)3, on.Value);
        Assert.Empty(ApplyPlanBuilder.Build(setting, "On").OfType<RegistryWriteOp>());

        Assert.Equal((byte)2, Assert.Single(ApplyPlanBuilder.Build(setting, "Off").OfType<RegistryByteSetOp>()).Value);
    }

    [Fact]
    public void Bit_op_is_emitted_per_mirror_path()
    {
        var reg = Reg("Settings", "Settings", @"HKCU\A", @"HKLM\B") with { ByteIndex = 4, BitMask = 0x20 };
        var setting = Make(
            new[] { (Target)reg },
            new SettingState { Label = "On", Set = new Dictionary<string, StateValue> { ["Settings"] = StateValue.Of(1) } });

        Assert.Equal(2, ApplyPlanBuilder.Build(setting, "On").OfType<RegistryBitSetOp>().Count());
    }

    [Fact]
    public void Composite_target_emits_a_sub_key_set_op_from_the_payload()
    {
        var reg = Reg("DirectXUserGlobalSettings", "DirectXUserGlobalSettings", @"HKCU\UserGpuPreferences")
            with { CompositeStringKey = "AutoHDREnable" };
        var setting = Make(
            new[] { (Target)reg },
            new SettingState { Label = "On",  Set = new Dictionary<string, StateValue> { ["DirectXUserGlobalSettings"] = StateValue.Of("1") } },
            new SettingState { Label = "Off", Set = new Dictionary<string, StateValue> { ["DirectXUserGlobalSettings"] = StateValue.Of("0") } });

        var on = Assert.Single(ApplyPlanBuilder.Build(setting, "On").OfType<RegistryCompositeSetOp>());
        Assert.Equal("AutoHDREnable", on.CompositeKey);
        Assert.Equal("1", on.SubValue);
        Assert.Empty(ApplyPlanBuilder.Build(setting, "On").OfType<RegistryWriteOp>());

        Assert.Equal("0", Assert.Single(ApplyPlanBuilder.Build(setting, "Off").OfType<RegistryCompositeSetOp>()).SubValue);
    }

    [Fact]
    public void Composite_target_with_absent_payload_emits_a_remove()
    {
        var reg = Reg("Packed", "Packed") with { CompositeStringKey = "SubKey" };
        var setting = Make(
            new[] { (Target)reg },
            new SettingState { Label = "Off", Set = new Dictionary<string, StateValue> { ["Packed"] = StateValue.Absent } });

        var op = Assert.Single(ApplyPlanBuilder.Build(setting, "Off").OfType<RegistryCompositeSetOp>());
        Assert.Null(op.SubValue);
        Assert.Empty(ApplyPlanBuilder.Build(setting, "Off").OfType<RegistryDeleteOp>());
    }

    [Fact]
    public void RegContent_setting_skips_registry_writes_and_emits_only_the_import()
    {
        // A setting that applies via a .reg import has detect-only registry targets - no registry write op.
        var setting = Make(
            new[] { Reg("Sentinel", "") },
            new SettingState
            {
                Label = "On",
                Set = new Dictionary<string, StateValue> { ["Sentinel"] = StateValue.Of("X") },
                Effects = new Effect[] { new RegContentEffect("REGDATA-ON") },
            },
            new SettingState
            {
                Label = "Off",
                Set = new Dictionary<string, StateValue> { ["Sentinel"] = StateValue.Absent },
                Effects = new Effect[] { new RegContentEffect("REGDATA-OFF") },
            });

        var onPlan = ApplyPlanBuilder.Build(setting, "On");
        Assert.Empty(onPlan.OfType<RegistryWriteOp>());
        Assert.Equal("REGDATA-ON", ((RegContentEffect)onPlan.OfType<EffectOp>().Single().Effect).Content);

        var offPlan = ApplyPlanBuilder.Build(setting, "Off");
        Assert.Empty(offPlan.OfType<RegistryDeleteOp>()); // Absent would normally DELETE; skipped for regcontent
        Assert.Equal("REGDATA-OFF", ((RegContentEffect)offPlan.OfType<EffectOp>().Single().Effect).Content);
    }

    [Fact]
    public void Native_power_effect_rides_alongside_the_registry_write()
    {
        // No RegContentEffect -> the registry write is NOT skipped; the native effect rides along after it.
        var setting = Make(
            new[] { Reg("HibernateEnabled", "HibernateEnabled") },
            new SettingState
            {
                Label = "On",
                Set = new Dictionary<string, StateValue> { ["HibernateEnabled"] = StateValue.Of(1) },
                Effects = new Effect[] { new NativePowerEffect(11, 1) },
            });

        var plan = ApplyPlanBuilder.Build(setting, "On");
        Assert.Single(plan.OfType<RegistryWriteOp>());
        Assert.Equal((byte)1, ((NativePowerEffect)plan.OfType<EffectOp>().Single().Effect).Value);
    }

    [Fact]
    public void Per_network_interface_target_emits_a_per_subkey_write_or_delete()
    {
        var reg = Reg("TcpAckFrequency", "TcpAckFrequency", @"HKLM\...\Interfaces") with { PerNetworkInterface = true };
        var setting = Make(
            new[] { (Target)reg },
            new SettingState { Label = "Off", Set = new Dictionary<string, StateValue> { ["TcpAckFrequency"] = StateValue.Of(1) } },
            new SettingState { Label = "On",  Set = new Dictionary<string, StateValue> { ["TcpAckFrequency"] = StateValue.Absent } });

        // "Off" writes 1 to TcpAckFrequency under every interface sub-key; no plain write op.
        var write = Assert.Single(ApplyPlanBuilder.Build(setting, "Off").OfType<RegistryPerSubkeyWriteOp>());
        Assert.Equal(@"HKLM\...\Interfaces", write.ParentPath);
        Assert.Equal(1, (int)write.Value);
        Assert.Empty(ApplyPlanBuilder.Build(setting, "Off").OfType<RegistryWriteOp>());

        // "On" (Absent) deletes the value under every sub-key; no plain delete op.
        Assert.Single(ApplyPlanBuilder.Build(setting, "On").OfType<RegistryPerSubkeyDeleteOp>());
        Assert.Empty(ApplyPlanBuilder.Build(setting, "On").OfType<RegistryDeleteOp>());
    }

    [Fact]
    public void Per_monitor_target_emits_a_per_subkey_write()
    {
        var reg = Reg("AutoColorManagementEnabled", "AutoColorManagementEnabled", @"HKLM\...\MonitorDataStore") with { PerMonitor = true };
        var setting = Make(
            new[] { (Target)reg },
            new SettingState { Label = "On", Set = new Dictionary<string, StateValue> { ["AutoColorManagementEnabled"] = StateValue.Of(1) } });

        var write = Assert.Single(ApplyPlanBuilder.Build(setting, "On").OfType<RegistryPerSubkeyWriteOp>());
        Assert.Equal(@"HKLM\...\MonitorDataStore", write.ParentPath);
        Assert.Equal(1, (int)write.Value);
        Assert.Empty(ApplyPlanBuilder.Build(setting, "On").OfType<RegistryWriteOp>());
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

    [Fact]
    public void BuildAction_RegistryWriteEffect_EmitsRegistryWriteOp_WithSynthesizedTarget()
    {
        var setting = new Setting
        {
            Id = "a",
            Display = new() { Name = "n", Description = "d", GroupName = "g" },
            Effects = new Effect[]
            {
                new RegistryWriteEffect(@"HKLM\SOFTWARE\X", "ConfigureStartPins", RegistryValueKind.String, "v") { IsGroupPolicy = true },
            },
        };

        var op = Assert.IsType<RegistryWriteOp>(Assert.Single(ApplyPlanBuilder.BuildAction(setting)));
        Assert.Equal(@"HKLM\SOFTWARE\X", op.Path);
        Assert.Equal("v", op.Value);
        Assert.Equal("ConfigureStartPins", op.Target.ValueName);
        Assert.Equal(RegistryValueKind.String, op.Target.Type);
        Assert.True(op.Target.IsGroupPolicy);
        Assert.Equal(new[] { @"HKLM\SOFTWARE\X" }, op.Target.Paths);
    }

    [Fact]
    public void BuildAction_ScriptEffect_EmitsEffectOp()
    {
        var setting = new Setting
        {
            Id = "a",
            Display = new() { Name = "n", Description = "d", GroupName = "g" },
            Effects = new Effect[] { new ScriptEffect("echo hi", RunContext.System) },
        };

        var op = Assert.IsType<EffectOp>(Assert.Single(ApplyPlanBuilder.BuildAction(setting)));
        var fx = Assert.IsType<ScriptEffect>(op.Effect);
        Assert.Equal("echo hi", fx.Script);
    }

    [Fact]
    public void BuildAction_PreservesEffectOrder()
    {
        var setting = new Setting
        {
            Id = "a",
            Display = new() { Name = "n", Description = "d", GroupName = "g" },
            Effects = new Effect[]
            {
                new RegistryWriteEffect(@"HKLM\A", "v1", RegistryValueKind.String, "x"),
                new RegistryWriteEffect(@"HKLM\B", "v2", RegistryValueKind.String, "y"),
                new ScriptEffect("echo hi", RunContext.System),
            },
        };

        var ops = ApplyPlanBuilder.BuildAction(setting);
        Assert.Collection(ops,
            o => Assert.Equal(@"HKLM\A", Assert.IsType<RegistryWriteOp>(o).Path),
            o => Assert.Equal(@"HKLM\B", Assert.IsType<RegistryWriteOp>(o).Path),
            o => Assert.IsType<EffectOp>(o));
    }
}
