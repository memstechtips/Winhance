using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Catalog.Migration;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class SettingStructuralComparerTests
{
    private static Setting Baseline() => new()
    {
        Id = "s",
        Display = new()
        {
            Name = "N", Description = "D", GroupName = "G",
            Icon = new Icon(IconPack.Material, "X"), AddedInVersion = "1.0", IsSubjectivePreference = true,
        },
        Availability = new() { Builds = new[] { BuildRange.Windows11 } },
        Apply = new() { RequiresConfirmation = true, Restart = new RestartProcess("Explorer") },
        Targets = new Target[]
        {
            new RegTarget("v", new[] { @"HKLM\K" }, "V", RegistryValueKind.DWord) { AppliesTo = new[] { BuildRange.Windows11 } },
        },
        States = new[]
        {
            new SettingState { Label = "Enabled", Roles = new[] { StateRole.Recommended }, Links = new[] { new Link("other", LinkKind.Requires, "Enabled") }, Set = new Dictionary<string, StateValue> { ["v"] = StateValue.Of(0) } },
            new SettingState { Label = "Disabled", IsFallback = true, Set = new Dictionary<string, StateValue> { ["v"] = StateValue.Of(1) } },
        },
        UiParentId = "parent",
    };

    [Fact]
    public void Equal_settings_produce_no_diff()
    {
        Assert.Empty(SettingStructuralComparer.Diff(Baseline(), Baseline()));
    }

    [Fact]
    public void Icon_pack_change_is_caught()
    {
        var changed = Baseline();
        changed = changed with { Display = changed.Display with { Icon = new Icon(IconPack.Fluent, "X") } };
        Assert.Contains(SettingStructuralComparer.Diff(Baseline(), changed), s => s.StartsWith("Display"));
    }

    [Fact]
    public void Build_range_change_is_caught()
    {
        var changed = Baseline() with { Availability = new Availability { Builds = new[] { BuildRange.Windows10 } } };
        Assert.Contains(SettingStructuralComparer.Diff(Baseline(), changed), s => s.StartsWith("Availability.Builds"));
    }

    [Fact]
    public void Restart_target_change_is_caught()
    {
        var changed = Baseline() with { Apply = new ApplyBehavior { RequiresConfirmation = true, Restart = new RestartService("svc") } };
        Assert.Contains(SettingStructuralComparer.Diff(Baseline(), changed), s => s.StartsWith("Apply"));
    }

    [Fact]
    public void StateValue_change_is_caught()
    {
        var changed = Baseline();
        var states = changed.States.ToArray();
        states[0] = states[0] with { Set = new Dictionary<string, StateValue> { ["v"] = StateValue.Of(99) } };
        changed = changed with { States = states };
        Assert.Contains(SettingStructuralComparer.Diff(Baseline(), changed), s => s.Contains("Set[v]"));
    }

    [Fact]
    public void Target_count_change_is_caught()
    {
        var changed = Baseline() with { Targets = Array.Empty<Target>() };
        Assert.Contains(SettingStructuralComparer.Diff(Baseline(), changed), s => s.StartsWith("Targets count"));
    }

    [Fact]
    public void Target_applies_to_change_is_caught()
    {
        var changed = Baseline();
        var targets = changed.Targets.ToArray();
        targets[0] = ((RegTarget)targets[0]) with { AppliesTo = new[] { BuildRange.Windows10 } };
        changed = changed with { Targets = targets };
        Assert.Contains(SettingStructuralComparer.Diff(Baseline(), changed), s => s.Contains("AppliesTo"));
    }

    [Fact]
    public void Controls_change_is_caught()
    {
        var a = Baseline();
        var aStates = a.States.ToArray();
        aStates[0] = aStates[0] with { Controls = new Dictionary<string, string> { ["child"] = "Enabled" } };
        a = a with { States = aStates };

        var b = Baseline();
        var bStates = b.States.ToArray();
        bStates[0] = bStates[0] with { Controls = new Dictionary<string, string> { ["child"] = "Disabled" } };
        b = b with { States = bStates };

        Assert.Contains(SettingStructuralComparer.Diff(a, b), s => s.Contains("Controls"));
    }

    [Fact]
    public void Links_change_is_caught()
    {
        var changed = Baseline();
        var states = changed.States.ToArray();
        states[0] = states[0] with { Links = new[] { new Link("other", LinkKind.Requires, "Disabled") } };
        changed = changed with { States = states };
        Assert.Contains(SettingStructuralComparer.Diff(Baseline(), changed), s => s.StartsWith("States[0].Links"));
    }

    [Fact]
    public void Detector_config_change_is_caught()
    {
        var a = Baseline() with { Detector = new DnsServerDetector("Auto", new Dictionary<string, string> { ["1.1.1.1"] = "CF" }) };
        var b = Baseline() with { Detector = new DnsServerDetector("Auto", new Dictionary<string, string> { ["8.8.8.8"] = "Google" }) };
        Assert.Contains(SettingStructuralComparer.Diff(a, b), s => s.Contains("Detector"));
    }

    [Fact]
    public void Detector_identical_config_produces_no_diff()
    {
        var a = Baseline() with { Detector = new SystemRestoreDetector("On", "Off") };
        var b = Baseline() with { Detector = new SystemRestoreDetector("On", "Off") };
        Assert.Empty(SettingStructuralComparer.Diff(a, b));
    }

    [Fact]
    public void PowerCfg_attribute_change_is_caught()
    {
        var a = Baseline() with { Targets = new Target[] { new PowerCfgTarget("p", "sub", "guidA", PowerModeSupport.Both) }, States = Array.Empty<SettingState>() };
        var b = Baseline() with { Targets = new Target[] { new PowerCfgTarget("p", "sub", "guidB", PowerModeSupport.Both) }, States = Array.Empty<SettingState>() };
        Assert.Contains(SettingStructuralComparer.Diff(a, b), s => s.Contains("powercfg"));
    }

    private static Setting Action(params Effect[] effects) => new()
    {
        Id = "a",
        Display = new() { Name = "n", Description = "d", GroupName = "g" },
        Effects = effects,
    };

    [Fact]
    public void Effects_IdenticalRegistryWrites_WithDistinctByteArrays_AreEqual()
    {
        var a = Action(new RegistryWriteEffect(@"HKCU\T", "Favorites", RegistryValueKind.Binary, new byte[0]));
        var b = Action(new RegistryWriteEffect(@"HKCU\T", "Favorites", RegistryValueKind.Binary, new byte[0]));
        Assert.Empty(SettingStructuralComparer.Diff(a, b)); // distinct byte[] instances must compare by content
    }

    [Fact]
    public void Effects_DifferentByteContent_Differs()
    {
        var a = Action(new RegistryWriteEffect(@"HKCU\T", "Favorites", RegistryValueKind.Binary, new byte[] { 1 }));
        var b = Action(new RegistryWriteEffect(@"HKCU\T", "Favorites", RegistryValueKind.Binary, new byte[] { 2 }));
        Assert.NotEmpty(SettingStructuralComparer.Diff(a, b));
    }

    [Fact]
    public void Effects_DifferentIsGroupPolicy_Differs()
    {
        var a = Action(new RegistryWriteEffect(@"HKLM\E", "ConfigureStartPins", RegistryValueKind.String, "x"));
        var b = Action(new RegistryWriteEffect(@"HKLM\E", "ConfigureStartPins", RegistryValueKind.String, "x") { IsGroupPolicy = true });
        Assert.NotEmpty(SettingStructuralComparer.Diff(a, b));
    }

    [Fact]
    public void Effects_DifferentScriptBody_Differs()
    {
        var a = Action(new ScriptEffect("one", RunContext.System));
        var b = Action(new ScriptEffect("two", RunContext.System));
        Assert.NotEmpty(SettingStructuralComparer.Diff(a, b));
    }

    [Fact]
    public void Effects_DifferentCount_Differs()
    {
        var a = Action(new ScriptEffect("one", RunContext.System));
        var b = Action(new ScriptEffect("one", RunContext.System), new ScriptEffect("two", RunContext.System));
        Assert.NotEmpty(SettingStructuralComparer.Diff(a, b));
    }
}
