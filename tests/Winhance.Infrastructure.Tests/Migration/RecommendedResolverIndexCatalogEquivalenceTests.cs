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
/// RecommendedSettingsResolver.GetRecommendedIndex / GetDefaultIndex reproduce the SettingDefinition versions
/// EXACTLY over the whole population, machine-independently (catalog + old defs only, no I/O). The def versions
/// read ComboBox.Options[i].IsRecommended / .IsDefault; the converter builds one State per option in order and
/// maps IsRecommended -> StateRole(Recommended) / IsDefault -> StateRole(WindowsDefault) (ConvertSelection), so
/// the first role-carrying State index == the def's first flagged-option index. No merged (-win10) setting is a
/// Selection (all 6 aliases are This PC toggles), so the Selection roles are unconditional and the equivalence is
/// build-invariant. Survives the converter teardown (reads SettingCatalog.All + the old defs).</summary>
public class RecommendedResolverIndexCatalogEquivalenceTests
{
    private readonly ITestOutputHelper _output;
    public RecommendedResolverIndexCatalogEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public void CatalogIndexHelpers_MatchDefVersions_OverThePopulation()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var compared = 0;
        var nonNullRecommended = 0;
        var nonNullDefault = 0;

        foreach (var def in AllDefinitions())
        {
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var setting))
                continue;
            compared++;

            var defRec = RecommendedSettingsResolver.GetRecommendedIndex(def);
            var catRec = RecommendedSettingsResolver.GetRecommendedIndex(setting);
            if (defRec != null) nonNullRecommended++;
            if (defRec != catRec)
                mismatches.Add($"{def.Id}: GetRecommendedIndex def {Fmt(defRec)} != catalog {Fmt(catRec)}");

            var defDef = RecommendedSettingsResolver.GetDefaultIndex(def);
            var catDef = RecommendedSettingsResolver.GetDefaultIndex(setting);
            if (defDef != null) nonNullDefault++;
            if (defDef != catDef)
                mismatches.Add($"{def.Id}: GetDefaultIndex def {Fmt(defDef)} != catalog {Fmt(catDef)}");
        }

        _output.WriteLine($"{compared} settings compared, {nonNullRecommended} non-null recommended, {nonNullDefault} non-null default, {mismatches.Count} mismatches");
        foreach (var m in mismatches) _output.WriteLine("  " + m);

        Assert.True(compared >= 300, $"only {compared} settings paired - population scoping bug (expected 400+)");
        Assert.True(nonNullRecommended > 0, "no non-null recommended index - GetRecommendedIndex comparison is vacuous");
        Assert.True(nonNullDefault > 0, "no non-null default index - GetDefaultIndex comparison is vacuous");

        // ConvertSystemTray / ConvertDnsServer now carry the option IsRecommended/IsDefault -> StateRole mapping (via the
        // shared RolesForOption helper, like ConvertSelection), so gaming-dns-server / taskbar-system-tray-icons-11 no longer
        // diverge - the overloads reproduce the def index over the WHOLE population. CatalogAuthoringEquivalenceTests re-gates
        // the authored roles == Convert(def) for these 2 detector selections.
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} index-helper catalog-vs-def mismatches:\n" + string.Join("\n", mismatches));
    }

    private static string Fmt(int? v) => v is null ? "null" : v.Value.ToString();
}
