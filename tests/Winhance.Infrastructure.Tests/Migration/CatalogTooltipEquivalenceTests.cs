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

/// <summary>Slice B ("should've been from the start"): per-option Tooltip is a LIVE presentation field the
/// 2026-06-22 catalog design dropped on a presentation-blind audit (it is read by ConfigReviewService's review UI;
/// the update-policy selection ships 4 real per-option tooltips). It now lives on the catalog SettingState.Tooltip,
/// populated by the converter from ComboBoxOption.Tooltip. This proves the catalog carries the def's per-option
/// tooltip byte-for-byte, index-aligned, over the WHOLE selection population - machine-independently (catalog + old
/// defs only, no I/O). It survives the converter teardown (reads SettingCatalog.All + the old defs, not the
/// converter), complementing CatalogAuthoringEquivalenceTests (authored == converter, which the comparer's new
/// Tooltip diff now enforces). Run: dotnet test --filter CatalogTooltipEquivalence</summary>
public class CatalogTooltipEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public CatalogTooltipEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public void CatalogStateTooltip_MatchesOldComboBoxOptionTooltip_PerIndex()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var compared = 0;
        var nonNullTooltips = 0;

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
                if (!string.IsNullOrEmpty(options[i].Tooltip))
                    nonNullTooltips++;
                if (options[i].Tooltip != s.States[i].Tooltip)
                    mismatches.Add($"{def.Id}[{i}]: old Tooltip {Fmt(options[i].Tooltip)} != catalog State.Tooltip {Fmt(s.States[i].Tooltip)}");
            }
        }

        _output.WriteLine($"{compared} selection settings compared, {nonNullTooltips} non-null tooltips, {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine($"  {m}");

        Assert.True(compared > 50, $"only compared {compared} selection settings - population scoping bug");
        Assert.True(nonNullTooltips > 0, "no non-null tooltip found - the comparison would be vacuous (the update-policy tooltips must be carried)");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} tooltip mismatches (catalog State.Tooltip vs old ComboBoxOption.Tooltip):\n" + string.Join("\n", mismatches));
    }

    private static string Fmt(string? v) => v is null ? "null" : $"\"{v}\"";
}
