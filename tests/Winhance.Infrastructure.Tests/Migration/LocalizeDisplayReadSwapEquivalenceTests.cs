using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Slice B2 (localization): retire the vestigial SettingLocalizationService.LocalizeSetting AND close the
/// non-English display regression it left orphaned. Since the Phase 6.7 Slice 11 def-&gt;catalog display cutover the
/// factory set Name/Description/GroupName from the RAW catalog Display (English) and the XAML bound them plain, so
/// non-en UI showed raw English. B2 has the factory + grouping localize the catalog Display fields via the canonical
/// Setting_{id}_Name / Setting_{id}_Description / SettingGroup_* keys (raw Display fallback) - reproducing exactly what
/// LocalizeSetting did off the def. This proves the read-swap faithful at the MODEL level over the whole paired
/// population (machine-independent - catalog + old defs only, no I/O; the en.json content equivalence is a REVIEW
/// concern, like B1: LocalizeOrFallback == old GetStringOrFallback, and the factory group-key derivation == old
/// GetLocalizedGroupName branch-for-branch). Two equalities make the swap byte-identical in every locale:
/// (1) the NEW key base (catalog Id) == the OLD key base (def.LocalizationId ?? def.Id), so Setting_{catalogId}_Name
///     == old Setting_{LocalizationId ?? Id}_Name (the group key is group-name-derived, identical given (2));
/// (2) the raw fallbacks match: catalog Display.Name/Description/GroupName == def.Name/Description/GroupName, so a
///     loc-miss falls back to the same raw string on both sides.
/// Survives the converter teardown (reads SettingCatalog.All + the old defs). Run: dotnet test --filter LocalizeDisplayReadSwap</summary>
public class LocalizeDisplayReadSwapEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public LocalizeDisplayReadSwapEquivalenceTests(ITestOutputHelper output) => _output = output;

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
    public void FactoryCatalogDisplay_MatchesOldDefDisplay_AndLocKeyBaseIsIdentical()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var compared = 0;
        var sawKnownPositive = false;

        foreach (var def in AllDefinitions())
        {
            // Mirror the loading service's pairing: normalize the 6 "-win10" ThisPc aliases to the canonical catalog
            // id (SettingsLoadingService: FirstOrDefault(c => c.Id == Normalize(setting.Id))). A def with no catalog
            // peer is skipped by the loading service too (logged + no VM), so skip it here.
            var canonical = SettingIdAliases.Normalize(def.Id);
            if (!catalogById.TryGetValue(canonical, out var paired))
                continue;
            compared++;
            if (def.Id == "gaming-game-mode")
                sawKnownPositive = true;

            // (1) Loc-key base identity. The factory localizes Name/Description via Setting_{paired.Id}_...; the old
            // service via Setting_{def.LocalizationId ?? def.Id}_.... LocalizationId is set ONLY on the 6 "-win10"
            // aliases (where it equals the canonical id), so oldBase == paired.Id universally - assert it.
            var oldBase = def.LocalizationId ?? def.Id;
            if (oldBase != paired.Id)
                mismatches.Add($"{def.Id}: loc-key base old '{oldBase}' != catalog '{paired.Id}'");

            // (2) Raw fallback identity. The factory falls back to the catalog Display value on a loc miss; the old
            // service fell back to the raw def value. For a byte-identical shown string on a miss these must match
            // (and this equality also makes the group loc key - which is derived from the group NAME - identical).
            if (paired.Display.Name != def.Name)
                mismatches.Add($"{def.Id}: Display.Name '{paired.Display.Name}' != def.Name '{def.Name}'");
            if (paired.Display.Description != def.Description)
                mismatches.Add($"{def.Id}: Display.Description '{paired.Display.Description}' != def.Description '{def.Description}'");
            if (paired.Display.GroupName != def.GroupName)
                mismatches.Add($"{def.Id}: Display.GroupName {Fmt(paired.Display.GroupName)} != def.GroupName {Fmt(def.GroupName)}");
        }

        _output.WriteLine($"{compared} paired settings compared, {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine("  " + m);

        Assert.True(compared > 300, $"only {compared} settings paired - population scoping bug (expected ALL settings, not just selections)");
        Assert.True(sawKnownPositive, "known-positive 'gaming-game-mode' not found in the paired population - scoping/vacuity bug");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} display read-swap / loc-key-base mismatches:\n" + string.Join("\n", mismatches));
    }

    private static string Fmt(string? v) => v is null ? "null" : $"\"{v}\"";
}
