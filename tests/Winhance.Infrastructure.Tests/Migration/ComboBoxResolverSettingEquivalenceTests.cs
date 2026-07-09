using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Catalog.Migration;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Slice 4bb-1 equivalence precondition for the reader-cluster cutover: the additive catalog-Setting
/// <see cref="ComboBoxResolver.ResolveRawValuesToIndex(Setting, System.Collections.Generic.Dictionary{string, object?})"/>
/// overload must resolve a live reads dictionary to the SAME selection option index as the def overload it will
/// replace, over every selection the app ships - EXCEPT the precedence-CORRECTED ids, which the catalog
/// deliberately makes diverge from the buggy old detection (see below). For each selection def with a catalog peer,
/// the reads are fed from the REAL def resolver's own inverse (ResolveIndexToRawValues, per option) plus the
/// all-absent and a bogus non-matching case, and BOTH the def and Setting overloads (the REAL ComboBoxResolver,
/// never reimplemented) are asked to resolve them - a divergence on a NON-corrected selection is a real regression
/// in the port. Pure; depends only on the catalog + the resolver, not the machine.
/// SCOPE: proven over CANONICAL-case reads (the values the app writes). The new model's StateValue.Matches uses
/// CatalogValueComparer, which is deliberately case-INSENSITIVE for strings where the def's ValueComparer is
/// case-sensitive - a hand-edited non-canonical-case registry string could resolve differently, but the app never
/// writes such a value and this overload is wired to nothing.
/// Run: dotnet test --filter ComboBoxResolverSettingEquivalence</summary>
public class ComboBoxResolverSettingEquivalenceTests
{
    // The precedence-corrected ids (CatalogAuthoringEquivalenceTests.PrecedenceCorrectedIds): their catalog
    // detection was corrected to the effective-value-by-precedence model, so they INTENTIONALLY diverge from the
    // old .Any/DefaultValue detection this overload's def counterpart reproduces. Among selections only
    // gaming-touch-keyboard-service is one (the rest are toggles), but the full set is listed to match the authoring
    // gate and stay correct if a corrected toggle ever becomes a selection. A divergence on any of these is
    // accepted; a divergence on any OTHER selection is a regression.
    private static readonly HashSet<string> PrecedenceCorrectedIds = new()
    {
        "privacy-advertising-id", "privacy-diagnostics", "privacy-lock-screen-overlay",
        "privacy-inking-typing-dictionary",
        "gaming-directx-flip-model", "gaming-directx-vrr-optimizations", "gaming-touch-keyboard-service",
    };

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
    public void ResolveRawValuesToIndex_Setting_overload_matches_the_def_overload_over_the_non_corrected_selections()
    {
        // The REAL resolver drives BOTH sides - the def overload is the ground truth, never reimplemented.
        var resolver = new ComboBoxResolver();
        var catalog = SettingCatalog.All.ToDictionary(s => s.Id);

        var selectionDefs = AllDefinitions()
            .Where(d => d.InputType == InputType.Selection)
            .ToList();

        var mismatches = new List<string>();
        var divergentIds = new HashSet<string>();
        var comparedSettingIds = new HashSet<string>();
        int comparedInputs = 0;
        int nonTrivialResults = 0; // a def result that is neither Custom(-1) nor 0, so an all-(-1)/all-0 pass is not vacuous

        foreach (var def in selectionDefs)
        {
            // Pair by NORMALIZED id, mirroring the provider (selections carry no -win10 alias, but be faithful).
            if (!catalog.TryGetValue(SettingIdAliases.Normalize(def.Id), out var setting))
                continue; // an unpaired selection is out of scope for this overload (the provider returns unpaired)

            // Representative reads: each option's canonical ValueMappings (the def resolver's own inverse), plus the
            // all-absent case (exercises the DefaultValue/absorbAbsent fill + the IsFallback / allBackingValuesAbsent
            // fallbacks), plus a bogus non-matching read.
            var readsCases = new List<Dictionary<string, object?>>();
            if (def.ComboBox?.Options is { } options)
                for (int idx = 0; idx < options.Count; idx++)
                    readsCases.Add(resolver.ResolveIndexToRawValues(def, idx));
            readsCases.Add(new Dictionary<string, object?>());
            readsCases.Add(new Dictionary<string, object?> { ["__no_such_key__"] = 12345 });

            foreach (var reads in readsCases)
            {
                int fromDef = resolver.ResolveRawValuesToIndex(def, new Dictionary<string, object?>(reads));
                int fromSetting = resolver.ResolveRawValuesToIndex(setting, new Dictionary<string, object?>(reads));
                comparedInputs++;
                comparedSettingIds.Add(def.Id);
                if (fromDef != ComboBoxConstants.CustomStateIndex && fromDef != 0)
                    nonTrivialResults++;
                if (fromDef != fromSetting)
                {
                    divergentIds.Add(def.Id);
                    mismatches.Add($"{def.Id}: reads={Fmt(reads)} def={fromDef} setting={fromSetting}");
                }
            }
        }

        // Non-vacuity: real coverage, and at least one genuinely-resolved (non-0, non-Custom) index.
        Assert.NotEmpty(comparedSettingIds);
        Assert.True(comparedInputs > 0, "no selection inputs compared - population scoping bug");
        Assert.True(nonTrivialResults > 0, "every def result was Custom(-1) or 0 - the comparison would pass vacuously");

        // Every divergence must be a documented precedence-corrected id; a divergence on any other selection is a
        // regression in the port. (This mirrors CatalogAuthoringEquivalenceTests, which excludes the same ids.)
        var unexpected = mismatches.Where(m => !PrecedenceCorrectedIds.Contains(m.Split(':')[0])).ToList();
        Assert.True(
            unexpected.Count == 0,
            $"{unexpected.Count} UNEXPECTED Setting-overload vs def-overload divergences (non precedence-corrected) over "
                + $"{comparedSettingIds.Count} selections:\n" + string.Join("\n", unexpected));

        // And every divergent id must be in the precedence-corrected set (redundant with the above but states the
        // invariant directly).
        Assert.Subset(PrecedenceCorrectedIds, divergentIds);
    }

    private static string Fmt(IReadOnlyDictionary<string, object?> d) =>
        "{" + string.Join(", ", d.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={FmtVal(kv.Value)}")) + "}";

    private static string FmtVal(object? v) =>
        v switch
        {
            null => "null",
            byte[] bytes => "[" + string.Join(",", bytes) + "]",
            _ => v.ToString() ?? "null",
        };
}
