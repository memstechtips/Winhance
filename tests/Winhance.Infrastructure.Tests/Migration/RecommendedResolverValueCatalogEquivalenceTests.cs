using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Slice 2 foundation (additive, wired to nothing yet): proves the new catalog-Setting overloads of
/// RecommendedSettingsResolver.HasRecommendedValue / HasDefaultValue / BuildPowerCfgApplyValue reproduce the
/// SettingDefinition versions EXACTLY over the whole population, machine-independently (catalog + old defs only,
/// no I/O). The apply-cluster (RecommendedSettingsApplier / BulkSettingsActionService) repoints onto these at
/// Slice 3; the def versions stay live until then.
///
/// HasRecommendedValue / HasDefaultValue union three signals; the def is already HYBRID (toggle via
/// CatalogToggleState, powercfg + registry-selection via def fields). The catalog overload reads all three off the
/// catalog: the toggle via the SAME build-aware CatalogToggleState call, a powercfg slider via Numeric, a selection
/// (registry unconditional OR powercfg context-scoped) via a role on a state. Tested at a Windows 10 AND a Windows
/// 11 build so the merged (-win10) toggles' build-scoped Windows default is exercised on both OSes.
///
/// BuildPowerCfgApplyValue: a powercfg Selection's per-mode recommended/default option index == the state carrying
/// the context-scoped role (== FindOptionIndexForPowerCfgValue(RecommendedValueAC)); a NumericRange's per-context
/// value is Numeric.Recommended/WindowsDefault (already display units, same conversion the def applies). Survives
/// the converter teardown (reads SettingCatalog.All + the old defs). Run: dotnet test --filter RecommendedResolverValueCatalog</summary>
public class RecommendedResolverValueCatalogEquivalenceTests
{
    private readonly ITestOutputHelper _output;
    public RecommendedResolverValueCatalogEquivalenceTests(ITestOutputHelper output) => _output = output;

    // A Windows 10 build (< 22000) and a Windows 11 build (>= 22000): the only build-sensitive contribution is the
    // toggle default of the merged This PC settings, which is OS-divergent - covering both proves parity on each OS.
    private static readonly WinBuild Win10 = new(19045);
    private static readonly WinBuild Win11 = new(22631);

    private static IEnumerable<SettingDefinition> AllDefinitions() => new[]
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
    }.SelectMany(g => g);

    [Fact]
    public void HasRecommendedValue_MatchesDefVersion_OverThePopulation()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var compared = 0;
        var trueCount = 0;

        foreach (var def in AllDefinitions())
        {
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var setting))
                continue;
            compared++;

            foreach (var build in new[] { Win10, Win11 })
            {
                var defHas = RecommendedSettingsResolver.HasRecommendedValue(def, build);
                var catHas = RecommendedSettingsResolver.HasRecommendedValue(setting, build);
                if (defHas) trueCount++;
                if (defHas != catHas)
                    mismatches.Add($"{def.Id} @ build {build.Build}: HasRecommendedValue def {defHas} != catalog {catHas}");
            }
        }

        _output.WriteLine($"{compared} settings compared x2 builds, {trueCount} def-true, {mismatches.Count} mismatches");
        foreach (var m in mismatches) _output.WriteLine("  " + m);

        Assert.True(compared >= 300, $"only {compared} settings paired - population scoping bug (expected 400+)");
        Assert.True(trueCount > 30, $"only {trueCount} def-true results - HasRecommendedValue comparison is vacuous");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} HasRecommendedValue catalog-vs-def mismatches:\n" + string.Join("\n", mismatches));
    }

    [Fact]
    public void HasDefaultValue_MatchesDefVersion_OverThePopulation()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var compared = 0;
        var trueCount = 0;

        foreach (var def in AllDefinitions())
        {
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var setting))
                continue;
            compared++;

            foreach (var build in new[] { Win10, Win11 })
            {
                var defHas = RecommendedSettingsResolver.HasDefaultValue(def, build);
                var catHas = RecommendedSettingsResolver.HasDefaultValue(setting, build);
                if (defHas) trueCount++;
                if (defHas != catHas)
                    mismatches.Add($"{def.Id} @ build {build.Build}: HasDefaultValue def {defHas} != catalog {catHas}");
            }
        }

        _output.WriteLine($"{compared} settings compared x2 builds, {trueCount} def-true, {mismatches.Count} mismatches");
        foreach (var m in mismatches) _output.WriteLine("  " + m);

        Assert.True(compared >= 300, $"only {compared} settings paired - population scoping bug (expected 400+)");
        Assert.True(trueCount > 30, $"only {trueCount} def-true results - HasDefaultValue comparison is vacuous");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} HasDefaultValue catalog-vs-def mismatches:\n" + string.Join("\n", mismatches));
    }

    [Fact]
    public void BuildPowerCfgApplyValue_MatchesDefVersion_OverThePowerCfgPopulation()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var comparedSettings = 0;
        var nonNullResults = 0;

        foreach (var def in AllDefinitions())
        {
            if (def.PowerCfgSettings is not { Count: > 0 })
                continue;
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var setting))
                continue;
            comparedSettings++;

            foreach (var useRecommended in new[] { true, false })
            {
                var defVal = RecommendedSettingsResolver.BuildPowerCfgApplyValue(def, useRecommended);
                var catVal = RecommendedSettingsResolver.BuildPowerCfgApplyValue(setting, useRecommended);
                if (defVal != null) nonNullResults++;
                if (!ApplyValuesEqual(defVal, catVal))
                    mismatches.Add($"{def.Id} (useRecommended={useRecommended}): def {Describe(defVal)} != catalog {Describe(catVal)}");
            }
        }

        _output.WriteLine($"{comparedSettings} powercfg settings x2 modes, {nonNullResults} non-null results, {mismatches.Count} mismatches");
        foreach (var m in mismatches) _output.WriteLine("  " + m);

        Assert.True(comparedSettings >= 30, $"only {comparedSettings} powercfg settings paired - population scoping bug (expected ~40+)");
        Assert.True(nonNullResults > 20, $"only {nonNullResults} non-null results - BuildPowerCfgApplyValue equivalence is vacuous");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} BuildPowerCfgApplyValue catalog-vs-def mismatches:\n" + string.Join("\n", mismatches));
    }

    // The apply value is null, a boxed int (single-mode index/value), or a Dictionary{ACValue,DCValue} (Separate mode).
    private static bool ApplyValuesEqual(object? a, object? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;

        if (a is Dictionary<string, object?> da && b is Dictionary<string, object?> db)
        {
            if (da.Count != db.Count) return false;
            foreach (var kv in da)
            {
                if (!db.TryGetValue(kv.Key, out var bv)) return false;
                if (!IntEquals(kv.Value, bv)) return false;
            }
            return true;
        }
        if (a is Dictionary<string, object?> || b is Dictionary<string, object?>) return false; // shape mismatch
        return IntEquals(a, b);
    }

    private static bool IntEquals(object? a, object? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return Convert.ToInt32(a) == Convert.ToInt32(b);
    }

    private static string Describe(object? v) => v switch
    {
        null => "null",
        Dictionary<string, object?> d => "{" + string.Join(", ", d.Select(kv => $"{kv.Key}={kv.Value}")) + "}",
        _ => v.ToString() ?? "null",
    };
}
