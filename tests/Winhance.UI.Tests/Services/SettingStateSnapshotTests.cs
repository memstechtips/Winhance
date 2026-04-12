using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class SettingStateSnapshotTests
{
    private static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Fixtures", "setting-state-baseline.json");

    /// <summary>
    /// Produces a deterministic JSON map of { settingId -> (recommendedState, defaultState) } for every
    /// Selection / NumericRange / Toggle SettingDefinition in the catalog.
    ///
    /// The baseline (Fixtures/setting-state-baseline.json) was generated PRE-REFACTOR by reading the
    /// shipped .winhance config presets (Winhance_Recommended_Config.winhance and
    /// Winhance_Default_Config_Windows11_25H2.winhance). Those configs were the only reliable
    /// encoding of "which option is Recommended / Default for this Selection setting", because
    /// pre-refactor, RegistrySetting.RecommendedOption/DefaultOption was null on ~70 of 72 settings.
    ///
    /// POST-REFACTOR, the code itself answers this question: look up
    /// Options.FirstOrDefault(o => o.IsRecommended). This test now resolves indices from the
    /// ComboBoxOption.IsRecommended / IsDefault flags and compares the result against the
    /// configs-sourced baseline. A zero-diff proves every migration agent correctly transcribed
    /// the shipped config SelectedIndex values into the new flag model.
    /// </summary>
    [Fact]
    public void SettingStateBaseline_MatchesFixture()
    {
        var all = CollectAllSettings();
        var snapshot = all
            .OrderBy(s => s.Id, StringComparer.Ordinal)
            .ToDictionary(
                s => s.Id,
                s => new SnapshotEntry(
                    ResolveRecommended(s),
                    ResolveDefault(s),
                    s.InputType.ToString()),
                StringComparer.Ordinal);

        var json = JsonSerializer.Serialize(
            snapshot,
            new JsonSerializerOptions { WriteIndented = true });

        if (!File.Exists(FixturePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FixturePath)!);
            File.WriteAllText(FixturePath, json);
            throw new Xunit.Sdk.XunitException(
                $"Baseline file did not exist - wrote it to {FixturePath}. " +
                "Commit the baseline, then re-run the test and it must pass.");
        }

        var expected = File.ReadAllText(FixturePath);
        json.Should().Be(expected, because: "Phase A must not change ANY setting's resolved Recommended/Default state.");
    }

    private static IEnumerable<SettingDefinition> CollectAllSettings()
    {
        // Each *Optimizations / *Customizations file exposes a SettingGroup via a GetXxx() accessor,
        // and the group's .Settings property is the IReadOnlyList<SettingDefinition> we need.
        return new[]
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
        }.SelectMany(list => list);
    }

    private static object? ResolveRecommended(SettingDefinition s)
    {
        if (s.InputType == InputType.Selection)
        {
            // Post-refactor: the code itself answers "which option is Recommended".
            // A zero-diff against the baseline proves the migration agents transcribed
            // the shipped .winhance config SelectedIndex values correctly into the
            // ComboBoxOption.IsRecommended flag on each option.
            var options = s.ComboBox?.Options;
            if (options is null) return null;
            for (int i = 0; i < options.Count; i++)
                if (options[i].IsRecommended) return new { kind = "index", value = i };
            return null;
        }
        return s.RegistrySettings?.FirstOrDefault()?.RecommendedValue;
    }

    private static object? ResolveDefault(SettingDefinition s)
    {
        if (s.InputType == InputType.Selection)
        {
            var options = s.ComboBox?.Options;
            if (options is null) return null;
            for (int i = 0; i < options.Count; i++)
                if (options[i].IsDefault) return new { kind = "index", value = i };
            return null;
        }
        return s.RegistrySettings?.FirstOrDefault()?.DefaultValue;
    }

    private sealed record SnapshotEntry(object? Recommended, object? Default, string InputType);
}
