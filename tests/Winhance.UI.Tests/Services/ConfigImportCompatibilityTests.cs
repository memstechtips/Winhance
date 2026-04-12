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

/// <summary>
/// Compatibility guardrail for the Recommended/Default state-model refactor (Phase A).
///
/// The .winhance config wire format serializes Selection state as a
/// <c>SelectedIndex</c> integer. That shape is UNCHANGED by the refactor - what's
/// changed is the internal resolution code. This test pins a snapshot of a
/// pre-migration .winhance config file and proves that every Selection entry in it
/// still resolves correctly against the post-refactor
/// <see cref="SettingDefinition.ComboBox"/> / <see cref="ComboBoxMetadata.Options"/>
/// catalog - i.e. the app can still import configs exported by earlier versions.
/// </summary>
public class ConfigImportCompatibilityTests
{
    // Test project build output lives at tests/Winhance.UI.Tests/bin/x64/Debug/net10.0-windows10.0.19041.0/ .
    // From AppContext.BaseDirectory that's 4 ".." hops up to tests/Winhance.UI.Tests/ , then into Fixtures/.
    private static readonly string FixturePath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "Fixtures", "pre-migration-sample.winhance");

    [Fact]
    public void PreMigrationConfig_AllSelectionIndices_ResolveWithinOptionsRange()
    {
        File.Exists(FixturePath).Should().BeTrue(
            $"pinned pre-migration fixture must exist at '{FixturePath}'");

        using var doc = JsonDocument.Parse(File.ReadAllText(FixturePath));
        var selectionIds = new Dictionary<string, int>(StringComparer.Ordinal);
        Walk(doc.RootElement, selectionIds);

        // Sanity: the pinned config must actually contain Selection entries, otherwise
        // this test would silently pass even if the walker broke.
        selectionIds.Should().NotBeEmpty("pinned config must contain some Selection entries");

        var catalog = CollectAllSettings().ToDictionary(s => s.Id, s => s, StringComparer.Ordinal);

        foreach (var (id, selectedIndex) in selectionIds)
        {
            catalog.TryGetValue(id, out var setting).Should().BeTrue(
                $"setting '{id}' present in pre-migration config must exist in the post-refactor catalog");
            setting!.InputType.Should().Be(InputType.Selection,
                $"'{id}' was tagged InputType=1 (Selection) in the config; it must still be Selection in the catalog");

            // LoadDynamicOptions Selection settings (e.g. power-plan-selection) populate their
            // ComboBox options at runtime from system state; pre-migration configs that captured
            // an index for them are still valid as long as the setting is still Selection-typed.
            if (setting.Recommendation is { LoadDynamicOptions: true })
            {
                continue;
            }

            setting.ComboBox.Should().NotBeNull(
                $"'{id}' must have ComboBoxMetadata after migration");
            setting.ComboBox!.Options.Should().NotBeNull(
                $"'{id}' ComboBoxMetadata.Options must be populated (migrated)");
            setting.ComboBox.Options.Should().NotBeEmpty(
                $"'{id}' ComboBoxMetadata.Options must not be empty");

            selectedIndex.Should().BeInRange(0, setting.ComboBox.Options!.Count - 1,
                $"'{id}' SelectedIndex {selectedIndex} from pre-migration config must index into its {setting.ComboBox.Options.Count}-option list");

            var target = setting.ComboBox.Options[selectedIndex];
            target.Should().NotBeNull(
                $"'{id}' option at index {selectedIndex} must resolve to a non-null ComboBoxOption");
            target.DisplayName.Should().NotBeNullOrEmpty(
                $"'{id}' option at index {selectedIndex} must have a non-empty DisplayName");
        }
    }

    /// <summary>
    /// Walks a .winhance JSON document and records every Selection-type entry
    /// (<c>InputType == 1</c> with a <c>SelectedIndex</c>) as Id -> SelectedIndex.
    /// Mirrors the walker used by <see cref="SettingStateSnapshotTests"/> so the two
    /// tests agree on what "a Selection entry in a .winhance config" looks like.
    /// </summary>
    private static void Walk(JsonElement element, Dictionary<string, int> map)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
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

    // Same accessor list used by SettingStateSnapshotTests and SettingCatalogValidatorTests.
    // Duplicated intentionally rather than factored into a shared helper - the duplication
    // is contained (10 lines) and all three tests want to stay independent of each other.
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
        }.SelectMany(list => list);
}
