using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class SettingCatalogValidatorTests
{
    // PowerCfg-backed Selection settings have per-mode Recommended/Default state via
    // PowerRecommendation (RecommendedOptionAC/DC) + PowerCfgSetting.RecommendedValueAC/DC /
    // DefaultValueAC/DC, not via ComboBoxOption.IsRecommended/IsDefault flags. A single flag can't
    // encode distinct AC/DC recommendations, so they're exempt from the single-flag validator rules.
    // The universal rules (Options set, no duplicate DisplayNames) still apply.
    private static bool IsPowerCfgBacked(SettingDefinition s) =>
        s.PowerCfgSettings is { Count: > 0 };

    public static IEnumerable<object[]> AllSettings() =>
        CollectAllSettings().Select(s => new object[] { s.Id, s });

    private static IEnumerable<SettingDefinition> CollectAllSettings() =>
        new[]
        {
            GamingAndPerformanceOptimizations.GetGamingAndPerformanceOptimizations().Settings,
            NotificationOptimizations.GetNotificationOptimizations().Settings,
            PowerOptimizations.GetPowerOptimizations().Settings,
            PrivacyAndSecurityOptimizations.GetPrivacyAndSecurityOptimizations().Settings,
            SoundOptimizations.GetSoundOptimizations().Settings,
            UpdateOptimizations.GetUpdateOptimizations().Settings,
            ExplorerCustomizations.GetExplorerCustomizations().Settings,
            StartMenuCustomizations.GetStartMenuCustomizations().Settings,
            TaskbarCustomizations.GetTaskbarCustomizations().Settings,
            WindowsThemeCustomizations.GetWindowsThemeCustomizations().Settings,
        }.SelectMany(l => l);

    [Theory]
    [MemberData(nameof(AllSettings))]
    public void Selection_ComboBoxMetadata_HasOptions(string id, SettingDefinition s)
    {
        if (s.InputType != InputType.Selection) return;
        // LoadDynamicOptions Selection settings (e.g. power-plan-selection) populate ComboBox
        // options at runtime from system state; no static ComboBoxMetadata.
        if (s.Recommendation is { LoadDynamicOptions: true }) return;
        s.ComboBox.Should().NotBeNull($"{id} is Selection and must have ComboBoxMetadata");
        s.ComboBox!.Options.Should().NotBeNull($"{id} ComboBoxMetadata.Options must be set (migrated)");
        s.ComboBox.Options.Should().NotBeEmpty($"{id} ComboBoxMetadata.Options must not be empty");
    }

    [Theory]
    [MemberData(nameof(AllSettings))]
    public void Selection_ExactlyOneDefault(string id, SettingDefinition s)
    {
        if (s.InputType != InputType.Selection || s.ComboBox?.Options is null) return;
        // PowerCfg-backed Selection: Default state lives on PowerCfgSetting.DefaultValueAC/DC per power mode.
        if (IsPowerCfgBacked(s)) return;
        var defaults = s.ComboBox.Options.Count(o => o.IsDefault);
        defaults.Should().Be(1, $"{id} must have exactly one option with IsDefault = true");
    }

    [Theory]
    [MemberData(nameof(AllSettings))]
    public void Selection_AtMostOneRecommended(string id, SettingDefinition s)
    {
        if (s.InputType != InputType.Selection || s.ComboBox?.Options is null) return;
        // PowerCfg-backed Selection: Recommended state lives on PowerRecommendation (AC/DC) +
        // PowerCfgSetting.RecommendedValueAC/DC. AC and DC can recommend different options.
        if (IsPowerCfgBacked(s)) return;
        var recommended = s.ComboBox.Options.Count(o => o.IsRecommended);
        recommended.Should().BeLessThanOrEqualTo(1, $"{id} must have at most one option with IsRecommended = true");
    }

    [Theory]
    [MemberData(nameof(AllSettings))]
    public void SubjectivePreference_HasZeroRecommendedOptions(string id, SettingDefinition s)
    {
        if (!s.IsSubjectivePreference) return;

        s.InputType.Should().Be(InputType.Selection,
            because: $"'{id}' is flagged IsSubjectivePreference but that is a Selection-only concept");

        var options = s.ComboBox?.Options ?? Array.Empty<ComboBoxOption>();
        options.Where(o => o.IsRecommended).Should().BeEmpty(
            because: $"'{id}' is subjective - Winhance must have no Recommended option. " +
                     "If Winhance has an opinion, flag the right option with IsRecommended instead of " +
                     "flagging the setting IsSubjectivePreference.");
    }

    [Theory]
    [MemberData(nameof(AllSettings))]
    public void Selection_NoDuplicateDisplayNames(string id, SettingDefinition s)
    {
        if (s.InputType != InputType.Selection || s.ComboBox?.Options is null) return;
        var names = s.ComboBox.Options.Select(o => o.DisplayName).ToList();
        names.Should().OnlyHaveUniqueItems($"{id} ComboBox options must have unique DisplayNames");
    }

    [Theory]
    [MemberData(nameof(AllSettings))]
    public void Selection_RegistrySettings_RecommendedAndDefaultValue_MustBeNull(string id, SettingDefinition s)
    {
        if (s.InputType != InputType.Selection || s.RegistrySettings is null) return;
        // PowerCfg-backed Selection: their state source is PowerCfgSetting, not RegistrySetting.
        // Guard is vacuously true today (they have no top-level RegistrySettings) but kept for clarity.
        if (IsPowerCfgBacked(s)) return;
        foreach (var reg in s.RegistrySettings)
        {
            reg.RecommendedValue.Should().BeNull(
                $"{id} is Selection - {reg.ValueName ?? "(key-level)"} RecommendedValue must be null (resolved via ComboBoxOption.ValueMappings)");
            reg.DefaultValue.Should().BeNull(
                $"{id} is Selection - {reg.ValueName ?? "(key-level)"} DefaultValue must be null (resolved via ComboBoxOption.ValueMappings)");
        }
    }
}
