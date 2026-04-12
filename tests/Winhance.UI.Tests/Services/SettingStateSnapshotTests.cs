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

    // The test project output lives at tests/Winhance.UI.Tests/bin/x64/Debug/net10.0-windows10.0.19041.0/ .
    // The shipped configs live under src/Winhance.UI/Features/Common/Resources/Configs/ . From
    // AppContext.BaseDirectory that is six ".." hops up to the repo root
    // (net10.../Debug/x64/bin/Winhance.UI.Tests/tests/), then into src/.
    private static readonly string RecommendedConfigPath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "src", "Winhance.UI", "Features", "Common", "Resources", "Configs",
            "Winhance_Recommended_Config.winhance");

    private static readonly string DefaultConfigPath =
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "src", "Winhance.UI", "Features", "Common", "Resources", "Configs",
            "Winhance_Default_Config_Windows11_25H2.winhance");

    private static readonly Lazy<IReadOnlyDictionary<string, int>> RecommendedSelectionIndices =
        new(() => LoadSelectionIndices(RecommendedConfigPath));

    private static readonly Lazy<IReadOnlyDictionary<string, int>> DefaultSelectionIndices =
        new(() => LoadSelectionIndices(DefaultConfigPath));

    /// <summary>
    /// Produces a deterministic JSON map of { settingId -> (recommendedState, defaultState) } for every
    /// Selection / NumericRange / Toggle SettingDefinition in the catalog, using the CURRENT resolution logic.
    /// Run once before Phase A (writes baseline); rerun after Phase A (diffs against baseline).
    ///
    /// For Selection settings the "current" Recommended/Default INDEX is sourced from the shipped
    /// .winhance config presets (Winhance_Recommended_Config.winhance and
    /// Winhance_Default_Config_Windows11_25H2.winhance) — these are the authoritative encoding of
    /// what the UI applies when the user picks "Recommended" or "Default", and this is what Phase A
    /// must preserve. Reading RegistrySetting.RecommendedOption/DefaultOption would only cover the
    /// tiny handful of entries flagged IsPrimary and therefore misses almost every Selection setting.
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
            return RecommendedSelectionIndices.Value.TryGetValue(s.Id, out var idx)
                ? new { kind = "index", value = idx }
                : null;
        }
        return s.RegistrySettings?.FirstOrDefault()?.RecommendedValue;
    }

    private static object? ResolveDefault(SettingDefinition s)
    {
        if (s.InputType == InputType.Selection)
        {
            return DefaultSelectionIndices.Value.TryGetValue(s.Id, out var idx)
                ? new { kind = "index", value = idx }
                : null;
        }
        return s.RegistrySettings?.FirstOrDefault()?.DefaultValue;
    }

    /// <summary>
    /// Walks a .winhance JSON document and returns a map from setting Id -> SelectedIndex for every
    /// entry where InputType == 1 (Selection) and SelectedIndex is present.
    /// Any recognizable "Items" array is descended regardless of nesting depth, so the parser works
    /// for both the flat WindowsApps.Items layout and the nested Customize/Optimize -> Features ->
    /// &lt;Category&gt; -> Items layout.
    /// </summary>
    private static IReadOnlyDictionary<string, int> LoadSelectionIndices(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Config file not found at '{path}'. The snapshot test resolves this path from " +
                $"AppContext.BaseDirectory = '{AppContext.BaseDirectory}'. If the repo layout moved, " +
                "update the relative path in SettingStateSnapshotTests.",
                path);
        }

        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        Walk(doc.RootElement, map);
        return map;
    }

    private static void Walk(JsonElement element, Dictionary<string, int> map)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                // If this object itself is a Selection-type setting entry, record it.
                if (element.TryGetProperty("Id", out var idProp) &&
                    idProp.ValueKind == JsonValueKind.String &&
                    element.TryGetProperty("InputType", out var inputTypeProp) &&
                    inputTypeProp.ValueKind == JsonValueKind.Number &&
                    inputTypeProp.TryGetInt32(out var inputType) &&
                    inputType == 1 &&
                    element.TryGetProperty("SelectedIndex", out var selectedIndexProp) &&
                    selectedIndexProp.ValueKind == JsonValueKind.Number &&
                    selectedIndexProp.TryGetInt32(out var selectedIndex))
                {
                    map[idProp.GetString()!] = selectedIndex;
                }

                foreach (var prop in element.EnumerateObject())
                {
                    Walk(prop.Value, map);
                }
                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, map);
                }
                break;
        }
    }

    private sealed record SnapshotEntry(object? Recommended, object? Default, string InputType);
}
