using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Xunit;
using Winhance.TestSupport;

using Winhance.Core.Features.Common.Localization;
namespace Winhance.Core.Tests.Catalog;

public class ApplyPlanBuilderTests
{
    private static RegTarget Reg(string key, string valueName, params string[] paths) =>
        new(key, paths.Length == 0 ? DefaultPaths : paths, valueName, RegistryValueKind.DWord);

    private static readonly WinBuild Win11 = new(22631);
    private static readonly WinBuild Win10 = new(19045);
    private static readonly string[] DefaultPaths = [@"HKEY_LOCAL_MACHINE\TEST"];
    private static readonly string[] SvcPath = [@"HKLM\Svc"];
    private static readonly string[] SoftwareXPath = [@"HKLM\SOFTWARE\X"];

    private static Setting Make(IReadOnlyList<Target> targets, params SettingState[] states) =>
        new() { Id = "t", Display = new() { Name = TestKeys.Of("t"), Description = TestKeys.Of("t") }, Targets = targets, States = states };

    [Fact]
    public void Build_ConcreteValue_WritesToEachMirrorPath()
    {
        var setting = Make(
            new[] { Reg("Hide", "HideSCAMeetNow", @"HKCU\A", @"HKLM\B") },
            new SettingState { Label = TestKeys.Of("On"), Set = new Dictionary<string, StateValue> { ["Hide"] = StateValue.Of(1) } });

        var plan = ApplyPlanBuilder.Build(setting, TestKeys.Of("On"));

        var writes = plan.OfType<RegistryWriteOp>().ToList();
        Assert.Equal(2, writes.Count);
        Assert.Contains(writes, w => w.Path == @"HKCU\A" && (int)w.Value == 1);
        Assert.Contains(writes, w => w.Path == @"HKLM\B" && (int)w.Value == 1);
    }

    [Fact]
    public void Build_LockableTarget_UnlocksBeforeAndLocksAfterOnlyTheProtectiveValue()
    {
        var setting = Make(
            new Target[] { new RegTarget("Start", SvcPath, "Start", RegistryValueKind.DWord) { LockWhenValue = 4 } },
            new SettingState { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["Start"] = StateValue.Of(4) } },
            new SettingState { Label = TestKeys.Of("Manual"),   Set = new Dictionary<string, StateValue> { ["Start"] = StateValue.Of(3) } });

        Assert.Collection(ApplyPlanBuilder.Build(setting, LocKey.Common.Disabled),
            op => Assert.IsType<RegistryUnlockKeyOp>(op),
            op => Assert.IsType<RegistryWriteOp>(op),
            op => Assert.IsType<RegistryLockKeyOp>(op));

        Assert.Collection(ApplyPlanBuilder.Build(setting, TestKeys.Of("Manual")),
            op => Assert.IsType<RegistryUnlockKeyOp>(op),
            op => Assert.IsType<RegistryWriteOp>(op));
    }

    [Fact]
    public void Build_AbsentState_EmitsADeletePerPath()
    {
        var setting = Make(
            new[] { Reg("Hide", "HideSCAMeetNow", @"HKCU\A", @"HKLM\B") },
            new SettingState { Label = TestKeys.Of("Off"), Set = new Dictionary<string, StateValue> { ["Hide"] = StateValue.Absent } });

        var plan = ApplyPlanBuilder.Build(setting, TestKeys.Of("Off"));

        Assert.Equal(2, plan.OfType<RegistryDeleteOp>().Count());
        Assert.Empty(plan.OfType<RegistryWriteOp>());
    }

    [Fact]
    public void Build_OrAbsentValue_WritesTheConcreteValue()
    {
        var setting = Make(
            new[] { Reg("Start", "Start") },
            new SettingState { Label = TestKeys.Of("Manual"), Set = new Dictionary<string, StateValue> { ["Start"] = StateValue.Of(3).OrAbsent() } });

        var op = Assert.Single(ApplyPlanBuilder.Build(setting, TestKeys.Of("Manual")).OfType<RegistryWriteOp>());
        Assert.Equal(3, (int)op.Value);
    }

    [Fact]
    public void Build_ExistsState_EmitsEnsureKey()
    {
        var setting = Make(
            new[] { Reg("Flag", "Flag") },
            new SettingState { Label = TestKeys.Of("On"), Set = new Dictionary<string, StateValue> { ["Flag"] = StateValue.Exists } });

        Assert.Single(ApplyPlanBuilder.Build(setting, TestKeys.Of("On")).OfType<RegistryEnsureKeyOp>());
    }

    [Fact]
    public void Build_StateWithEffects_EmitsEffectsAfterRegistryOps()
    {
        var setting = Make(
            new[] { Reg("Start", "Start") },
            new SettingState
            {
                Label = LocKey.Common.Disabled,
                Set = new Dictionary<string, StateValue> { ["Start"] = StateValue.Of(4) },
                Effects = new Effect[] { new ScriptEffect("rename.ps1", RunContext.System) },
            });

        var plan = ApplyPlanBuilder.Build(setting, LocKey.Common.Disabled);
        Assert.True(plan[^1] is EffectOp);
        Assert.Equal("rename.ps1", ((ScriptEffect)plan.OfType<EffectOp>().Single().Effect).Script);
    }

    [Fact]
    public void Build_BitTarget_EmitsABitSetOpKeyedOffThePayload()
    {
        var reg = Reg("Settings", "Settings", @"HKCU\CabinetState") with { ByteIndex = 4, BitMask = 0x20 };
        var setting = Make(
            new[] { (Target)reg },
            new SettingState { Label = TestKeys.Of("On"),  Set = new Dictionary<string, StateValue> { ["Settings"] = StateValue.Of(1) } },
            new SettingState { Label = TestKeys.Of("Off"), Set = new Dictionary<string, StateValue> { ["Settings"] = StateValue.Of(0) } });

        var on = Assert.Single(ApplyPlanBuilder.Build(setting, TestKeys.Of("On")).OfType<RegistryBitSetOp>());
        Assert.Equal(4, on.ByteIndex);
        Assert.Equal((byte)0x20, on.BitMask);
        Assert.True(on.Set);
        Assert.Empty(ApplyPlanBuilder.Build(setting, TestKeys.Of("On")).OfType<RegistryWriteOp>());

        Assert.False(Assert.Single(ApplyPlanBuilder.Build(setting, TestKeys.Of("Off")).OfType<RegistryBitSetOp>()).Set);
    }

    [Fact]
    public void Build_StringFlagTarget_EmitsAFlagSetOpKeyedOffThePayload()
    {
        var reg = Reg("Flags", "Flags", @"HKCU\MouseKeys") with { StringFlagMask = 0x04, StringFlagAbsentBase = 62 };
        var setting = Make(
            new[] { (Target)reg },
            new SettingState { Label = TestKeys.Of("On"),  Set = new Dictionary<string, StateValue> { ["Flags"] = StateValue.Of(true) } },
            new SettingState { Label = TestKeys.Of("Off"), Set = new Dictionary<string, StateValue> { ["Flags"] = StateValue.Of(false) } });

        var on = Assert.Single(ApplyPlanBuilder.Build(setting, TestKeys.Of("On")).OfType<RegistryStringFlagSetOp>());
        Assert.Equal(0x04, on.FlagMask);
        Assert.Equal(62, on.AbsentBase);
        Assert.True(on.Set);
        Assert.Empty(ApplyPlanBuilder.Build(setting, TestKeys.Of("On")).OfType<RegistryWriteOp>());

        Assert.False(Assert.Single(ApplyPlanBuilder.Build(setting, TestKeys.Of("Off")).OfType<RegistryStringFlagSetOp>()).Set);
    }

    [Fact]
    public void Build_ListItemTarget_EmitsAListItemOpKeyedOffThePayload()
    {
        var reg = Reg("Pages", "SettingsPageVisibility", @"HKLM\Explorer") with { ListItem = "home", ListPrefix = "hide:" };
        var setting = Make(
            new[] { (Target)reg },
            new SettingState { Label = TestKeys.Of("On"),  Set = new Dictionary<string, StateValue> { ["Pages"] = StateValue.Of(true) } },
            new SettingState { Label = TestKeys.Of("Off"), Set = new Dictionary<string, StateValue> { ["Pages"] = StateValue.Of(false).OrAbsent() } });

        var on = Assert.Single(ApplyPlanBuilder.Build(setting, TestKeys.Of("On")));
        var op = Assert.IsType<RegistryListItemSetOp>(on);
        Assert.Equal("hide:", op.Prefix);
        Assert.Equal("home", op.Item);
        Assert.True(op.Present);

        Assert.False(Assert.Single(ApplyPlanBuilder.Build(setting, TestKeys.Of("Off")).OfType<RegistryListItemSetOp>()).Present);
    }

    [Fact]
    public void Build_ByteOnlyTarget_EmitsAByteSetOpWithThePayloadByte()
    {
        var reg = Reg("Settings", "Settings", @"HKCU\StuckRects3") with { ByteIndex = 8, ByteOnly = true };
        var setting = Make(
            new[] { (Target)reg },
            new SettingState { Label = TestKeys.Of("On"),  Set = new Dictionary<string, StateValue> { ["Settings"] = StateValue.Of(3) } },
            new SettingState { Label = TestKeys.Of("Off"), Set = new Dictionary<string, StateValue> { ["Settings"] = StateValue.Of(2) } });

        var on = Assert.Single(ApplyPlanBuilder.Build(setting, TestKeys.Of("On")).OfType<RegistryByteSetOp>());
        Assert.Equal(8, on.ByteIndex);
        Assert.Equal((byte)3, on.Value);
        Assert.Empty(ApplyPlanBuilder.Build(setting, TestKeys.Of("On")).OfType<RegistryWriteOp>());

        Assert.Equal((byte)2, Assert.Single(ApplyPlanBuilder.Build(setting, TestKeys.Of("Off")).OfType<RegistryByteSetOp>()).Value);
    }

    [Fact]
    public void Build_BitTargetWithMirrorPaths_EmitsABitOpPerPath()
    {
        var reg = Reg("Settings", "Settings", @"HKCU\A", @"HKLM\B") with { ByteIndex = 4, BitMask = 0x20 };
        var setting = Make(
            new[] { (Target)reg },
            new SettingState { Label = TestKeys.Of("On"), Set = new Dictionary<string, StateValue> { ["Settings"] = StateValue.Of(1) } });

        Assert.Equal(2, ApplyPlanBuilder.Build(setting, TestKeys.Of("On")).OfType<RegistryBitSetOp>().Count());
    }

    [Fact]
    public void Build_CompositeTarget_EmitsASubKeySetOpFromThePayload()
    {
        var reg = Reg("DirectXUserGlobalSettings", "DirectXUserGlobalSettings", @"HKCU\UserGpuPreferences")
            with { CompositeStringKey = "AutoHDREnable" };
        var setting = Make(
            new[] { (Target)reg },
            new SettingState { Label = TestKeys.Of("On"),  Set = new Dictionary<string, StateValue> { ["DirectXUserGlobalSettings"] = StateValue.Of("1") } },
            new SettingState { Label = TestKeys.Of("Off"), Set = new Dictionary<string, StateValue> { ["DirectXUserGlobalSettings"] = StateValue.Of("0") } });

        var on = Assert.Single(ApplyPlanBuilder.Build(setting, TestKeys.Of("On")).OfType<RegistryCompositeSetOp>());
        Assert.Equal("AutoHDREnable", on.CompositeKey);
        Assert.Equal("1", on.SubValue);
        Assert.Empty(ApplyPlanBuilder.Build(setting, TestKeys.Of("On")).OfType<RegistryWriteOp>());

        Assert.Equal("0", Assert.Single(ApplyPlanBuilder.Build(setting, TestKeys.Of("Off")).OfType<RegistryCompositeSetOp>()).SubValue);
    }

    [Fact]
    public void Build_CompositeTargetWithAbsentPayload_EmitsARemove()
    {
        var reg = Reg("Packed", "Packed") with { CompositeStringKey = "SubKey" };
        var setting = Make(
            new[] { (Target)reg },
            new SettingState { Label = TestKeys.Of("Off"), Set = new Dictionary<string, StateValue> { ["Packed"] = StateValue.Absent } });

        var op = Assert.Single(ApplyPlanBuilder.Build(setting, TestKeys.Of("Off")).OfType<RegistryCompositeSetOp>());
        Assert.Null(op.SubValue);
        Assert.Empty(ApplyPlanBuilder.Build(setting, TestKeys.Of("Off")).OfType<RegistryDeleteOp>());
    }

    [Fact]
    public void Build_RegContentSetting_SkipsRegistryWritesAndEmitsOnlyTheImport()
    {
        // A setting that applies via a .reg import has detect-only registry targets - no registry write op.
        var setting = Make(
            new[] { Reg("Sentinel", "") },
            new SettingState
            {
                Label = TestKeys.Of("On"),
                Set = new Dictionary<string, StateValue> { ["Sentinel"] = StateValue.Of("X") },
                Effects = new Effect[] { new RegContentEffect("REGDATA-ON") },
            },
            new SettingState
            {
                Label = TestKeys.Of("Off"),
                Set = new Dictionary<string, StateValue> { ["Sentinel"] = StateValue.Absent },
                Effects = new Effect[] { new RegContentEffect("REGDATA-OFF") },
            });

        var onPlan = ApplyPlanBuilder.Build(setting, TestKeys.Of("On"));
        Assert.Empty(onPlan.OfType<RegistryWriteOp>());
        Assert.Equal("REGDATA-ON", ((RegContentEffect)onPlan.OfType<EffectOp>().Single().Effect).Content);

        var offPlan = ApplyPlanBuilder.Build(setting, TestKeys.Of("Off"));
        Assert.Empty(offPlan.OfType<RegistryDeleteOp>()); // Absent would normally DELETE; skipped for regcontent
        Assert.Equal("REGDATA-OFF", ((RegContentEffect)offPlan.OfType<EffectOp>().Single().Effect).Content);
    }

    [Fact]
    public void Build_ScriptEffect_RunsAfterTheRegistryWrite()
    {
        // No RegContentEffect -> the registry write is NOT skipped; the script rides along after it.
        var setting = Make(
            new[] { Reg("HibernateEnabled", "HibernateEnabled") },
            new SettingState
            {
                Label = TestKeys.Of("On"),
                Set = new Dictionary<string, StateValue> { ["HibernateEnabled"] = StateValue.Of(1) },
                Effects = new Effect[] { new ScriptEffect("powercfg /hibernate on", RunContext.System) },
            });

        var plan = ApplyPlanBuilder.Build(setting, TestKeys.Of("On"));
        Assert.Single(plan.OfType<RegistryWriteOp>());
        Assert.Equal("powercfg /hibernate on", ((ScriptEffect)plan.OfType<EffectOp>().Single().Effect).Script);
    }

    [Fact]
    public void Build_PerNetworkInterfaceTarget_EmitsAPerSubkeyWriteOrDelete()
    {
        var reg = Reg("TcpAckFrequency", "TcpAckFrequency", @"HKLM\...\Interfaces") with { PerNetworkInterface = true };
        var setting = Make(
            new[] { (Target)reg },
            new SettingState { Label = TestKeys.Of("Off"), Set = new Dictionary<string, StateValue> { ["TcpAckFrequency"] = StateValue.Of(1) } },
            new SettingState { Label = TestKeys.Of("On"),  Set = new Dictionary<string, StateValue> { ["TcpAckFrequency"] = StateValue.Absent } });

        var write = Assert.Single(ApplyPlanBuilder.Build(setting, TestKeys.Of("Off")).OfType<RegistryPerSubkeyWriteOp>());
        Assert.Equal(@"HKLM\...\Interfaces", write.ParentPath);
        Assert.Equal(1, (int)write.Value);
        Assert.Empty(ApplyPlanBuilder.Build(setting, TestKeys.Of("Off")).OfType<RegistryWriteOp>());

        Assert.Single(ApplyPlanBuilder.Build(setting, TestKeys.Of("On")).OfType<RegistryPerSubkeyDeleteOp>());
        Assert.Empty(ApplyPlanBuilder.Build(setting, TestKeys.Of("On")).OfType<RegistryDeleteOp>());
    }

    [Fact]
    public void Build_PerMonitorTarget_EmitsAPerSubkeyWrite()
    {
        var reg = Reg("AutoColorManagementEnabled", "AutoColorManagementEnabled", @"HKLM\...\MonitorDataStore") with { PerMonitor = true };
        var setting = Make(
            new[] { (Target)reg },
            new SettingState { Label = TestKeys.Of("On"), Set = new Dictionary<string, StateValue> { ["AutoColorManagementEnabled"] = StateValue.Of(1) } });

        var write = Assert.Single(ApplyPlanBuilder.Build(setting, TestKeys.Of("On")).OfType<RegistryPerSubkeyWriteOp>());
        Assert.Equal(@"HKLM\...\MonitorDataStore", write.ParentPath);
        Assert.Equal(1, (int)write.Value);
        Assert.Empty(ApplyPlanBuilder.Build(setting, TestKeys.Of("On")).OfType<RegistryWriteOp>());
    }

    [Fact]
    public void Build_TaskState_EmitsEnableOrDisable()
    {
        var setting = Make(
            new[] { (Target)new TaskTarget("Task", @"\MS\Win\Task") },
            new SettingState { Label = TestKeys.Of("On"),  Set = new Dictionary<string, StateValue> { ["Task"] = StateValue.Of(true) } },
            new SettingState { Label = TestKeys.Of("Off"), Set = new Dictionary<string, StateValue> { ["Task"] = StateValue.Of(false) } });

        Assert.True(ApplyPlanBuilder.Build(setting, TestKeys.Of("On")).OfType<TaskSetOp>().Single().Enabled);
        Assert.False(ApplyPlanBuilder.Build(setting, TestKeys.Of("Off")).OfType<TaskSetOp>().Single().Enabled);
    }

    [Fact]
    public void Build_UnknownStateLabel_Throws()
    {
        var setting = Make(new[] { Reg("K", "V") },
            new SettingState { Label = TestKeys.Of("On"), Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(1) } });
        Assert.Throws<System.ArgumentException>(() => ApplyPlanBuilder.Build(setting, TestKeys.Of("Nope")));
    }

    [Fact]
    public void Build_FallbackWithPartialSet_SkipsUncoveredTargets()
    {
        var setting = Make(
            new[] { Reg("A", "A"), Reg("B", "B") },
            new SettingState { Label = TestKeys.Of("Default"), IsFallback = true, Set = new Dictionary<string, StateValue> { ["A"] = StateValue.Of(1) } });

        var plan = ApplyPlanBuilder.Build(setting, TestKeys.Of("Default"));
        Assert.Single(plan.OfType<RegistryWriteOp>());
        Assert.Equal(@"HKEY_LOCAL_MACHINE\TEST", plan.OfType<RegistryWriteOp>().Single().Path);
    }

    [Fact]
    public void BuildAction_RegistryWriteEffect_EmitsRegistryWriteOp_WithSynthesizedTarget()
    {
        var setting = new Setting
        {
            Id = "a",
            Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d"), GroupName = TestKeys.Of("g") },
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
        Assert.Equal(SoftwareXPath, op.Target.Paths);
    }

    [Fact]
    public void BuildAction_ScriptEffect_EmitsEffectOp()
    {
        var setting = new Setting
        {
            Id = "a",
            Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d"), GroupName = TestKeys.Of("g") },
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
            Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d"), GroupName = TestKeys.Of("g") },
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
    [Fact]
    public void Build_WindowsDefaultState_WritesItsResetSetWithoutTheResetFlag()
    {
        // Apply Recommended and the per-card quick-set issue a NORMAL apply. When the state they land on is
        // also the Windows default, it must still DELETE the targets its ResetSet covers - otherwise those two
        // buttons re-stamp what Apply Windows Defaults removed.
        var setting = Make(
            new[] { Reg("k", "V") },
            new SettingState
            {
                Label = LocKey.Common.Enabled,
                Roles = new[] { StateRole.WindowsDefault },
                Set = new Dictionary<string, StateValue> { ["k"] = StateValue.Of(1).OrAbsent() },
                ResetSet = new Dictionary<string, StateValue> { ["k"] = StateValue.Absent },
            });

        var plan = ApplyPlanBuilder.Build(setting, LocKey.Common.Enabled);

        Assert.IsType<RegistryDeleteOp>(Assert.Single(plan));
    }

    [Fact]
    public void Build_BuildScopedWindowsDefault_WritesItsResetSetOnlyOnTheBuildItAppliesTo()
    {
        var setting = Make(
            new[] { Reg("k", "V") },
            new SettingState
            {
                Label = LocKey.Common.Enabled,
                Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                Set = new Dictionary<string, StateValue> { ["k"] = StateValue.Of(1) },
                ResetSet = new Dictionary<string, StateValue> { ["k"] = StateValue.Absent },
            });

        Assert.IsType<RegistryDeleteOp>(Assert.Single(ApplyPlanBuilder.Build(setting, LocKey.Common.Enabled, Win11)));
        Assert.IsType<RegistryWriteOp>(Assert.Single(ApplyPlanBuilder.Build(setting, LocKey.Common.Enabled, Win10)));
    }

    private const string PolicyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\DataCollection";
    private const string WindowsOwnPolicyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection";

    private static Setting PolicyMirror(IReadOnlyDictionary<string, StateValue>? resetSet = null) => Make(
        new[] { Reg("k", "V", WindowsOwnPolicyPath, PolicyPath) },
        new SettingState
        {
            Label = LocKey.Common.Enabled,
            Roles = new[] { StateRole.WindowsDefault },
            Set = new Dictionary<string, StateValue> { ["k"] = StateValue.Of(1).OrAbsent() },
            ResetSet = resetSet,
        },
        new SettingState { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["k"] = StateValue.Of(0) } });

    [Fact]
    public void Build_WindowsDefault_DeletesTheGroupPolicyPathAndWritesWindowsOwnMirror()
    {
        var plan = ApplyPlanBuilder.Build(PolicyMirror(), LocKey.Common.Enabled);

        Assert.Equal(PolicyPath, Assert.Single(plan.OfType<RegistryDeleteOp>()).Path);
        var write = Assert.Single(plan.OfType<RegistryWriteOp>());
        Assert.Equal(WindowsOwnPolicyPath, write.Path);
        Assert.Equal(1, (int)write.Value);
    }

    [Fact]
    public void Build_NonDefaultState_StillWritesTheGroupPolicyPath()
    {
        var plan = ApplyPlanBuilder.Build(PolicyMirror(), LocKey.Common.Disabled);

        Assert.Empty(plan.OfType<RegistryDeleteOp>());
        Assert.Equal(2, plan.OfType<RegistryWriteOp>().Count(w => (int)w.Value == 0));
    }

    [Fact]
    public void Build_ResetSetNamingTheTarget_OverridesTheGroupPolicyDelete()
    {
        var plan = ApplyPlanBuilder.Build(
            PolicyMirror(new Dictionary<string, StateValue> { ["k"] = StateValue.Of(1) }), LocKey.Common.Enabled);

        Assert.Empty(plan.OfType<RegistryDeleteOp>());
        Assert.Equal(2, plan.OfType<RegistryWriteOp>().Count(w => (int)w.Value == 1));
    }
}
