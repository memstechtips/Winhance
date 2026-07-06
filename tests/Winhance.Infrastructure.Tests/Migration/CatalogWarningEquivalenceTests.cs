using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Per-option Warning is a LIVE presentation field (the sibling of Tooltip / Slice B) that the
/// 2026-06-22 catalog design dropped on a presentation-blind audit - on the def path it is read by
/// SettingLocalizationService + the optionWarnings precompute in SettingsLoadingService; 9 per-option warnings
/// ship across update-policy + gaming/performance. It now lives on the catalog SettingState.Warning, populated by
/// the converter from ComboBoxOption.Warning. This proves the catalog carries the def's per-option warning
/// byte-for-byte, index-aligned, over the WHOLE selection population - machine-independently (catalog + old defs
/// only, no I/O). It survives the converter teardown (reads SettingCatalog.All + the old defs, not the converter),
/// complementing CatalogAuthoringEquivalenceTests (authored == converter, which the comparer's new Warning diff
/// now enforces). Run: dotnet test --filter CatalogWarningEquivalence</summary>
public class CatalogWarningEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public CatalogWarningEquivalenceTests(ITestOutputHelper output) => _output = output;

    private static IEnumerable<SettingDefinition> AllDefinitions()
    {
        return new[]
        {
            ExplorerCustomizations.GetExplorerCustomizations().Settings,
            StartMenuCustomizations.GetStartMenuCustomizations().Settings,
            TaskbarCustomizations.GetTaskbarCustomizations().Settings,
            WindowsThemeCustomizations.GetWindowsThemeCustomizations().Settings,
            PowerOptimizations.GetPowerOptimizations().Settings,
            GamingAndPerformanceOptimizations.GetGamingAndPerformanceOptimizations().Settings,
            NotificationOptimizations.GetNotificationOptimizations().Settings,
            PrivacyAndSecurityOptimizations.GetPrivacyAndSecurityOptimizations().Settings,
            SoundOptimizations.GetSoundOptimizations().Settings,
            UpdateOptimizations.GetUpdateOptimizations().Settings,
        }.SelectMany(group => group);
    }

    [Fact]
    public void CatalogStateWarning_MatchesOldComboBoxOptionWarning_PerIndex()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var compared = 0;
        var nonNullWarnings = 0;

        foreach (var def in AllDefinitions())
        {
            if (def.InputType != InputType.Selection || def.Id == SettingIds.PowerPlanSelection)
                continue; // power-plan is a dynamic-option selection with no static per-option states
            var options = def.ComboBox?.Options;
            if (options == null)
                continue;
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var s))
                continue;

            if (options.Count != s.States.Count)
            {
                mismatches.Add($"{def.Id}: ComboBox has {options.Count} options but catalog has {s.States.Count} States");
                continue;
            }
            compared++;

            for (int i = 0; i < options.Count; i++)
            {
                if (!string.IsNullOrEmpty(options[i].Warning))
                    nonNullWarnings++;
                if (options[i].Warning != s.States[i].Warning)
                    mismatches.Add($"{def.Id}[{i}]: old Warning {Fmt(options[i].Warning)} != catalog State.Warning {Fmt(s.States[i].Warning)}");
            }
        }

        _output.WriteLine($"{compared} selection settings compared, {nonNullWarnings} non-null warnings, {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine($"  {m}");

        Assert.True(compared > 50, $"only compared {compared} selection settings - population scoping bug");
        Assert.True(nonNullWarnings > 0, "no non-null warning found - the comparison would be vacuous (the per-option warnings must be carried)");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} warning mismatches (catalog State.Warning vs old ComboBoxOption.Warning):\n" + string.Join("\n", mismatches));
    }

    private static string Fmt(string? v) => v is null ? "null" : $"\"{v}\"";
}
