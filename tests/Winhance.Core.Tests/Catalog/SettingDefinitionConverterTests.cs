using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Catalog.Migration;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class SettingDefinitionConverterTests
{
    private static SettingDefinition ToggleDef(params RegistrySetting[] regs) => new()
    {
        Id = "t", Name = "n", Description = "d", InputType = InputType.Toggle,
        RegistrySettings = regs,
    };

    [Fact]
    public void Single_target_toggle_maps_enabled_disabled()
    {
        var def = ToggleDef(new RegistrySetting
        {
            KeyPath = @"HKLM\A", ValueName = "V",
            EnabledValue = new object?[] { 1 }, DisabledValue = new object?[] { 0 },
            RecommendedValue = null, DefaultValue = 0, ValueType = RegistryValueKind.DWord,
        });

        var s = SettingDefinitionConverter.ConvertToggle(def);

        var target = Assert.IsType<RegTarget>(Assert.Single(s.Targets));
        Assert.Equal("V", target.Key);
        Assert.Equal(new[] { @"HKLM\A" }, target.Paths);

        var enabled = s.States.Single(x => x.Label == "Enabled");
        var disabled = s.States.Single(x => x.Label == "Disabled");
        Assert.True(enabled.Set["V"].Matches(1, present: true));
        Assert.True(disabled.Set["V"].Matches(0, present: true));
    }

    [Fact]
    public void Mirror_paths_fold_into_one_target()
    {
        var def = ToggleDef(
            new RegistrySetting { KeyPath = @"HKCU\A", ValueName = "Hide", EnabledValue = new object?[] { 1 }, DisabledValue = new object?[] { null }, RecommendedValue = 1, DefaultValue = null, ValueType = RegistryValueKind.DWord },
            new RegistrySetting { KeyPath = @"HKLM\B", ValueName = "Hide", EnabledValue = new object?[] { 1 }, DisabledValue = new object?[] { null }, RecommendedValue = 1, DefaultValue = null, ValueType = RegistryValueKind.DWord });

        var s = SettingDefinitionConverter.ConvertToggle(def);

        var target = Assert.IsType<RegTarget>(Assert.Single(s.Targets));   // one target, two paths
        Assert.Equal(2, target.Paths.Count);
        // Disabled = [null] -> Absent
        Assert.True(s.States.Single(x => x.Label == "Disabled").Set["Hide"].Matches(null, present: false));
    }

    [Fact]
    public void Value_or_absent_array_becomes_or_absent()
    {
        var def = ToggleDef(new RegistrySetting
        {
            KeyPath = @"HKLM\A", ValueName = "V",
            EnabledValue = new object?[] { 1, null }, DisabledValue = new object?[] { 0 },
            RecommendedValue = 1, DefaultValue = 0, ValueType = RegistryValueKind.DWord,
        });
        var s = SettingDefinitionConverter.ConvertToggle(def);
        var enabled = s.States.Single(x => x.Label == "Enabled").Set["V"];
        Assert.True(enabled.Matches(1, present: true));
        Assert.True(enabled.Matches(null, present: false)); // OrAbsent
    }
}
