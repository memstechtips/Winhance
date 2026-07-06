using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Slice B (localization): retire the LAST live read of the old def's per-option Warning. The loading
/// bridge computed <c>optionWarnings</c> from <c>def.ComboBox.Options[i].Warning</c> (localized by
/// SettingLocalizationService via the <c>Setting_{LocalizationId ?? Id}_OptionWarning_{i}</c> key); the factory now
/// computes it from the catalog <c>Setting.States[i].Warning</c> (localized via <c>Setting_{catalogId}_OptionWarning_{i}</c>).
/// The status banner reads <c>optionWarnings[selectedIndex]</c>, and the factory builds one option per catalog State
/// (index i), so the list is index-aligned with States. This proves the read-swap faithful at the MODEL level over
/// the whole population (machine-independent - catalog + old defs only, no I/O; the en.json localization uses the SAME
/// key + SAME raw fallback on both sides, proven by the raw-value + loc-key-base equalities here). Complements
/// CatalogWarningEquivalenceTests (converter carry) by covering the exact production read (incl. power-plan /
/// null-ComboBox, which that test skips) and the loc-key base identity. Survives the converter teardown (reads
/// SettingCatalog.All + the old defs). Run: dotnet test --filter OptionWarningReadSwap</summary>
public class OptionWarningReadSwapEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public OptionWarningReadSwapEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public void FactoryCatalogWarnings_MatchOldDefWarnings_OverBannerReachableRange()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var pairedSettings = 0;
        var comparedSelections = 0;
        var warningBearing = 0;
        var nonNullCatalogWarnings = 0;

        foreach (var def in AllDefinitions())
        {
            // Mirror the loading service's pairing: normalize the 6 "-win10" ThisPc aliases to the canonical
            // catalog id (SettingsLoadingService.cs: FirstOrDefault(c => c.Id == Normalize(setting.Id))). A def
            // with no catalog peer is skipped by the loading service too (logged + no VM), so skip it here.
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var paired))
                continue;
            pairedSettings++;

            // OLD production source (raw, pre-localization): def.ComboBox?.Options?.Select(o => o.Warning) - null
            // when the setting has no ComboBox (toggles, power-plan). NEW source: paired.States.Select(Warning).
            var oldWarnings = def.ComboBox?.Options?.Select(o => o.Warning).ToList();
            var newWarnings = paired.States.Select(st => st.Warning).ToList();

            if (def.InputType == InputType.Selection)
                comparedSelections++;

            // The factory builds one option per catalog State (index i), so a user selects index 0..States.Count-1
            // (Custom is a separate sentinel). The banner reads optionWarnings[selectedIndex] over that range,
            // treating out-of-range/null as no-warning. Compare the two sources at every reachable index.
            for (int i = 0; i < newWarnings.Count; i++)
            {
                if (!string.IsNullOrEmpty(newWarnings[i]))
                    nonNullCatalogWarnings++;

                var oldVal = (oldWarnings != null && i < oldWarnings.Count) ? oldWarnings[i] : null;
                if (oldVal != newWarnings[i])
                    mismatches.Add($"{def.Id}[{i}]: old {Fmt(oldVal)} != catalog {Fmt(newWarnings[i])}");
            }

            // An OLD warning at an index beyond the catalog States is at an option the factory never builds
            // (only States.Count options exist), so it is unreachable by the banner - but assert it is empty so
            // a genuinely dropped warning still fails.
            if (oldWarnings != null)
                for (int i = newWarnings.Count; i < oldWarnings.Count; i++)
                    if (!string.IsNullOrEmpty(oldWarnings[i]))
                        mismatches.Add($"{def.Id}[{i}]: old warning {Fmt(oldWarnings[i])} has no catalog State (dropped)");

            // Loc-key base identity: the factory localizes via Setting_{paired.Id}_OptionWarning_{i}; the old
            // service via Setting_{def.LocalizationId ?? def.Id}_OptionWarning_{i}. For the localized string to be
            // identical the key base must match (else a warning falls back to the raw string in non-en locales).
            var defHasWarning = def.ComboBox?.Options?.Any(o => !string.IsNullOrEmpty(o.Warning)) == true;
            var catalogHasWarning = paired.States.Any(st => !string.IsNullOrEmpty(st.Warning));
            if (defHasWarning || catalogHasWarning)
            {
                warningBearing++;
                var oldBase = def.LocalizationId ?? def.Id;
                var newBase = paired.Id; // == SettingIdAliases.Normalize(def.Id)
                if (oldBase != newBase)
                    mismatches.Add($"{def.Id}: loc-key base mismatch old '{oldBase}' vs catalog '{newBase}' (warning key diverges)");
            }
        }

        _output.WriteLine($"{pairedSettings} paired settings ({comparedSelections} selections), {warningBearing} warning-bearing, {nonNullCatalogWarnings} non-null catalog warnings, {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine("  " + m);

        Assert.True(comparedSelections > 50, $"only {comparedSelections} selections paired - population scoping bug");
        Assert.True(nonNullCatalogWarnings >= 9, $"expected >= 9 catalog warnings (the shipped set), found {nonNullCatalogWarnings} - vacuous/regressed");
        Assert.True(warningBearing >= 6, $"only {warningBearing} warning-bearing settings - population scoping bug");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} option-warning read-swap mismatches:\n" + string.Join("\n", mismatches));
    }

    private static string Fmt(string? v) => v is null ? "null" : $"\"{v}\"";
}
