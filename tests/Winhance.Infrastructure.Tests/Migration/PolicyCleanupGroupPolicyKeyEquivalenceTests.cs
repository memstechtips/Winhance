using System;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>SettingDefinition retirement (PolicyCleanupService cutover, additive proof): proves the catalog-Setting
/// group-policy key extraction the ported PolicyCleanupService uses (RegTarget.IsGroupPolicy Paths + RegistryWrite
/// Effect.IsGroupPolicy) reproduces the OLD def extraction (RegistrySetting.IsGroupPolicy KeyPath) EXACTLY, per
/// canonical id, over the whole population, machine-independently (catalog + old defs only, no I/O). PolicyCleanup
/// reads GetAllBypassedSettings() (OS-build-relaxed, hardware + existence kept) == the new GetAll(includeOther
/// OsVersions:true); membership equivalence is proven separately (CatalogFeaturePartition / MembershipFilter
/// equivalence), so proving the per-canonical-id key SET is identical proves the final deduplicated policy-key set is
/// identical. The -win10 merged variants union onto the canonical merged Setting (build-gated Targets), so old defs
/// are grouped by SettingIdAliases.Normalize and their keys unioned before comparison. Survives the converter
/// teardown (reads SettingCatalog.All + the old defs). Run: dotnet test --filter PolicyCleanupGroupPolicyKey.
/// TWO settings diverge and the divergence is ACCEPTED (Marco 2026-07-08, catalog authoritative): the catalog's
/// IsGroupPolicy flags already drive live detection (CatalogDiscovery precedence), so PolicyCleanup follows them.
/// start-recommended-section: the 3-path HideRecommendedSection mirror folds to one RegTarget with one flag, so the
/// PolicyManager\current\device\Start path (IsGroupPolicy=false in the def) is cleaned too - a per-path flag the
/// model cannot split. privacy-inking-typing-dictionary: the catalog marks the CPSS\Store target group-policy where
/// the def did not. This test PINS that exact divergent-id set (a THIRD divergence fails - re-verify the catalog
/// authoring is intentional) and asserts the divergence is ADDITIVE-ONLY (the new set never DROPS a key old had).</summary>
public class PolicyCleanupGroupPolicyKeyEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public PolicyCleanupGroupPolicyKeyEquivalenceTests(ITestOutputHelper output) => _output = output;

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

    // Mirrors the OLD PolicyCleanupService: a group-policy RegistrySetting contributes its KeyPath.
    private static HashSet<string> OldGroupPolicyKeys(SettingDefinition def)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (def.RegistrySettings != null)
            foreach (var rs in def.RegistrySettings)
                if (rs.IsGroupPolicy && !string.IsNullOrEmpty(rs.KeyPath))
                    set.Add(rs.KeyPath);
        return set;
    }

    // Mirrors the PORTED PolicyCleanupService: group-policy RegTargets (every Path of a mirror) + group-policy
    // RegistryWriteEffects (an Action carries its registry writes as setting-level effects; per-state scanned
    // defensively). The powercfg EnablementKey is a nested RegTarget on PowerCfgTarget, not a top-level Target, so
    // Targets.OfType<RegTarget>() correctly excludes it (it is never a group-policy key anyway).
    private static HashSet<string> NewGroupPolicyKeys(Setting s)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in s.Targets.OfType<RegTarget>())
            if (t.IsGroupPolicy)
                foreach (var p in t.Paths)
                    if (!string.IsNullOrEmpty(p))
                        set.Add(p);
        foreach (var e in s.Effects.OfType<RegistryWriteEffect>())
            if (e.IsGroupPolicy && !string.IsNullOrEmpty(e.Path))
                set.Add(e.Path);
        foreach (var st in s.States)
            foreach (var e in st.Effects.OfType<RegistryWriteEffect>())
                if (e.IsGroupPolicy && !string.IsNullOrEmpty(e.Path))
                    set.Add(e.Path);
        return set;
    }

    [Fact]
    public void CatalogGroupPolicyKeys_MatchDefVersions_PerCanonicalId_OverAllSettings()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);

        // Group old defs by canonical id (the -win10 merged variants collapse onto their canonical Setting) and
        // union their group-policy keys, so a merged Setting is compared against the union of its OS-variant defs.
        var oldByCanonical = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var def in AllDefinitions())
        {
            var canon = SettingIdAliases.Normalize(def.Id);
            if (!oldByCanonical.TryGetValue(canon, out var set))
                oldByCanonical[canon] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in OldGroupPolicyKeys(def))
                set.Add(k);
        }

        var mismatches = new List<string>();
        var divergentIds = new List<string>();
        var regressions = new List<string>();
        var comparedSettings = 0;
        var settingsWithGpKeys = 0;
        var totalGpKeys = 0;

        foreach (var (canon, oldSet) in oldByCanonical)
        {
            if (!catalogById.TryGetValue(canon, out var setting))
            {
                if (oldSet.Count > 0)
                    mismatches.Add($"{canon}: no catalog peer but has {oldSet.Count} group-policy keys");
                continue;
            }
            comparedSettings++;
            var newSet = NewGroupPolicyKeys(setting);
            if (oldSet.Count > 0)
            {
                settingsWithGpKeys++;
                totalGpKeys += oldSet.Count;
            }
            if (!oldSet.SetEquals(newSet))
            {
                divergentIds.Add(canon);
                mismatches.Add($"{canon}: old [{string.Join(", ", oldSet.OrderBy(x => x))}] != new [{string.Join(", ", newSet.OrderBy(x => x))}]");
                if (!oldSet.IsSubsetOf(newSet))
                    regressions.Add($"{canon}: new set DROPS a key the old set had - old [{string.Join(", ", oldSet.OrderBy(x => x))}] new [{string.Join(", ", newSet.OrderBy(x => x))}]");
            }
        }

        _output.WriteLine($"{comparedSettings} settings compared, {settingsWithGpKeys} with group-policy keys ({totalGpKeys} keys), {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine("  " + m);

        Assert.True(comparedSettings >= 400, $"only {comparedSettings} settings compared - population scoping bug (expected ~412)");
        Assert.True(settingsWithGpKeys > 0, "no group-policy keys found anywhere - the comparison is vacuous");

        // The divergence is additive-only: the catalog cleans a superset of the old policy keys, never fewer.
        Assert.True(regressions.Count == 0, $"{regressions.Count} ADDITIVE-ONLY violations (new set drops an old key):\n" + string.Join("\n", regressions));

        // PIN the exact accepted divergent set (Marco 2026-07-08, catalog authoritative). A NEW divergence means the
        // catalog IsGroupPolicy authoring changed - re-verify it is intentional before widening this set.
        var expectedDivergent = new HashSet<string> { "start-recommended-section", "privacy-inking-typing-dictionary" };
        var actualDivergent = new HashSet<string>(divergentIds);
        Assert.True(actualDivergent.SetEquals(expectedDivergent),
            $"divergent set [{string.Join(", ", actualDivergent.OrderBy(x => x))}] != expected [{string.Join(", ", expectedDivergent.OrderBy(x => x))}]:\n" + string.Join("\n", mismatches));
    }
}
