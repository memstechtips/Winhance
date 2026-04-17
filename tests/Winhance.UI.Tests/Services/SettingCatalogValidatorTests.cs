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
    public void Selection_HasAtLeastOneDefault(string id, SettingDefinition s)
    {
        if (s.InputType != InputType.Selection || s.ComboBox?.Options is null) return;
        // PowerCfg-backed Selection: Default state lives on PowerCfgSetting.DefaultValueAC/DC per power mode.
        if (IsPowerCfgBacked(s)) return;
        var defaults = s.ComboBox.Options.Count(o => o.IsDefault);
        // Subjective settings whose Windows factory default varies by locale (measurement-system,
        // currency-decimal, etc.) flag MULTIPLE options as IsDefault — each is a default in some
        // locale. Non-subjective settings still expect exactly one.
        if (s.IsSubjectivePreference)
            defaults.Should().BeGreaterThanOrEqualTo(1, $"{id} (subjective) must have at least one option with IsDefault = true");
        else
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

    // NOTE: Under the multi-badge model, IsSubjectivePreference + IsRecommended can coexist.
    // The Preference pill says "this is a matter of taste"; the Recommended pill says "but Winhance
    // suggests this option." Both pills display independently. A subjective setting MAY carry a
    // Winhance recommendation — there is no constraint preventing it.
    // IsSubjectivePreference applies to any InputType (Toggle, CheckBox, Selection, NumericRange, ...),
    // not just Selection. For Toggles/CheckBoxes it signals "user choice — Winhance makes no
    // recommendation," which is why Toggle_HasRecommendation below exempts subjective settings.

    [Theory]
    [MemberData(nameof(AllSettings))]
    public void Selection_NoDuplicateDisplayNames(string id, SettingDefinition s)
    {
        if (s.InputType != InputType.Selection || s.ComboBox?.Options is null) return;
        var names = s.ComboBox.Options.Select(o => o.DisplayName).ToList();
        names.Should().OnlyHaveUniqueItems($"{id} ComboBox options must have unique DisplayNames");
    }

    /// <summary>
    /// Every Toggle/CheckBox SettingDefinition that has registry-backed state must declare a
    /// Winhance recommendation — either via the toggle-level <see cref="SettingDefinition.RecommendedToggleState"/>
    /// flag or via at least one <see cref="RegistrySetting.RecommendedValue"/>. Catches future
    /// drift where someone adds a toggle without filling in the recommendation.
    /// Settings backed only by ScheduledTask / PowerCfg / NativePowerApi / PowerShellScripts /
    /// RegContents are exempt (they carry their recommendation on those models, not here).
    /// Settings flagged <see cref="SettingDefinition.IsSubjectivePreference"/> are also exempt — the
    /// flag explicitly means "user choice, no Winhance recommendation."
    /// </summary>
    [Theory]
    [MemberData(nameof(AllSettings))]
    public void Toggle_HasRecommendation(string id, SettingDefinition s)
    {
        if (s.InputType != InputType.Toggle && s.InputType != InputType.CheckBox) return;
        if (s.RegistrySettings is null || s.RegistrySettings.Count == 0) return;
        if (s.IsSubjectivePreference) return;

        bool hasToggleLevelFlag = s.RecommendedToggleState.HasValue;
        bool hasPerKeyValue = s.RegistrySettings.Any(r => r.RecommendedValue != null);

        (hasToggleLevelFlag || hasPerKeyValue).Should().BeTrue(
            $"{id} is a Toggle/CheckBox and must declare a Winhance recommendation — set " +
            $"SettingDefinition.RecommendedToggleState or at least one RegistrySetting.RecommendedValue");
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
