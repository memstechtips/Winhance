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
    /// Selection / NumericRange / Toggle SettingDefinition in the catalog, using the CURRENT resolution logic.
    /// Run once before Phase A (writes baseline); rerun after Phase A (diffs against baseline).
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
            var primary = s.RegistrySettings?.FirstOrDefault(r => r.IsPrimary);
            if (primary?.RecommendedOption is string opt &&
                int.TryParse(opt, out var idx)) return new { kind = "index", value = idx };
            if (primary?.RecommendedOption != null) return new { kind = "option-name", value = primary.RecommendedOption };
            return null;
        }
        return s.RegistrySettings?.FirstOrDefault()?.RecommendedValue;
    }

    private static object? ResolveDefault(SettingDefinition s)
    {
        if (s.InputType == InputType.Selection)
        {
            var primary = s.RegistrySettings?.FirstOrDefault(r => r.IsPrimary);
            if (primary?.DefaultOption is string opt &&
                int.TryParse(opt, out var idx)) return new { kind = "index", value = idx };
            if (primary?.DefaultOption != null) return new { kind = "option-name", value = primary.DefaultOption };
            return null;
        }
        return s.RegistrySettings?.FirstOrDefault()?.DefaultValue;
    }

    private sealed record SnapshotEntry(object? Recommended, object? Default, string InputType);
}
