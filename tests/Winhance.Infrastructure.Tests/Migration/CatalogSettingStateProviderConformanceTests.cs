using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Catalog.Migration;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Catalog;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Machine-INDEPENDENT conformance for the new-engine <see cref="CatalogSettingStateProvider"/> - the permanent
/// guard left behind after old discovery + the equivalence oracle were retired (Phase 6.9 teardown). These construct
/// readings/detection results directly and assert against what Windows ships and the catalog model, never a live
/// old-vs-new comparison. Covers: the Windows-grounded IsEnabled rule (the <c>IsEnabled_*</c> facts + the
/// <c>Every_gate_*</c> structural invariants that keep the rule well-defined), the win10-alias pairing, and the
/// selection value-match fallback (the Phase 6.9 Custom-regression guard).
///
/// Run: dotnet test --filter CatalogSettingStateProviderConformance</summary>
public class CatalogSettingStateProviderConformanceTests
{
    private static readonly IReadOnlyDictionary<string, Setting> Catalog = SettingCatalog.All.ToDictionary(s => s.Id);

    /// <summary>Every SettingDefinition the app ships, straight from the static feature providers (no DI, no
    /// Windows-version filtering) - the same population the migration equivalence tests used.</summary>
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

    // ============================================================================================================
    //  Machine-INDEPENDENT model-conformance for IsEnabled (the done-right, Windows-grounded "not in the Windows-
    //  default state" rule). These construct readings and assert against what Windows ships - NOT a live hybrid.
    // ============================================================================================================

    private static bool Derive(Setting s, string? stateLabel)
        => CatalogSettingStateProvider.DeriveIsEnabled(s, new CatalogDetectionResult { StateLabel = stateLabel });

    private static bool DeriveNumeric(Setting s, int? acReadingSystemUnits)
        => CatalogSettingStateProvider.DeriveIsEnabled(s, new CatalogDetectionResult { Value = acReadingSystemUnits });

    [Fact]
    public void IsEnabled_registry_selection_is_modified_from_windows_default()
    {
        // A 3-option service: Automatic = WindowsDefault, ManualRecommended = Recommended, Disabled = no role.
        // The hard instance: Disabled is neither default nor recommended, yet it IS a modification from the Windows
        // default, so it must read enabled - this is where `!WindowsDefault` and `HasRole(Recommended)` diverge.
        var s = Catalog["gaming-print-spooler-service"];
        Assert.False(Derive(s, "ServiceOption_Automatic"));         // Windows default -> not enabled
        Assert.True(Derive(s, "ServiceOption_ManualRecommended"));  // recommended, non-default -> enabled
        Assert.True(Derive(s, "ServiceOption_Disabled"));           // non-default, non-recommended -> enabled
        Assert.True(Derive(s, null));                               // Custom / unrecognised -> non-default -> enabled
    }

    [Fact]
    public void IsEnabled_powercfg_selection_uses_the_ac_context_default()
    {
        // SelectionStates(LidActions, recAC:1, recDC:1, defAC:1, defDC:1) -> option index 1 is the AC default. The
        // WindowsDefault role is context-scoped (AC), so IsEnabled must check the AC role, not the Always default.
        var s = Catalog["lid-close-action"];
        Assert.False(Derive(s, "Template_LidActions_Option_1"));    // AC default -> not enabled
        Assert.True(Derive(s, "Template_LidActions_Option_0"));     // non-default -> enabled
        Assert.True(Derive(s, "Template_LidActions_Option_3"));     // non-default -> enabled
    }

    [Fact]
    public void IsEnabled_numeric_percent_is_modified_from_the_default_value()
    {
        // WindowsDefault AC = 0 (%). "%" converts system->display 1:1.
        var s = Catalog["processor-min-state"];
        Assert.False(DeriveNumeric(s, 0));      // at the Windows default -> not enabled
        Assert.True(DeriveNumeric(s, 100));     // 100% -> enabled
        Assert.False(DeriveNumeric(s, null));   // no reading -> cannot be modified -> not enabled
    }

    [Fact]
    public void IsEnabled_numeric_minutes_converts_system_units_before_comparing()
    {
        // WindowsDefault AC = 20 (minutes); the raw powercfg reading is in seconds. The compare MUST convert
        // system->display first (1200s -> 20min) - comparing the raw 1200 to 20 would wrongly read enabled.
        var s = Catalog["power-harddisk-timeout"];
        Assert.False(DeriveNumeric(s, 1200));   // 1200s = 20min = default -> not enabled
        Assert.True(DeriveNumeric(s, 600));     // 600s = 10min != 20 -> enabled
    }

    [Fact]
    public void IsEnabled_toggle_tracks_the_enabled_label_unchanged()
    {
        // Toggles keep the proven switch-position rule (StateLabel == "Enabled"); any toggle Setting exercises it.
        var toggle = Catalog.Values.First(x => x.Control == ControlKind.Toggle);
        Assert.True(Derive(toggle, "Enabled"));
        Assert.False(Derive(toggle, "Disabled"));
        Assert.False(Derive(toggle, null));   // a Custom toggle -> not enabled
    }

    [Fact]
    public void ResolveSelectionIndex_state_labels_match_option_displaynames_over_selections()
    {
        // Slice 4bb-2: the provider's ResolveSelectionIndex was ported to match the new-engine StateLabel against
        // catalog States[i].Label instead of the old ComboBox.Options[i].DisplayName. That is byte-equivalent iff,
        // for every selection, the catalog States are in option order with Label == the option DisplayName (the
        // converter builds them that way; the catalog must match). This machine-independent [Fact] pins the
        // invariant over the whole selection population, so a future authoring that reorders states or gives one a
        // richer/localized Label - which would silently change which index a StateLabel resolves to - fails here.
        var offenders = new List<string>();
        int comparedSelections = 0;
        foreach (var def in AllDefinitions())
        {
            if (def.InputType != InputType.Selection || def.ComboBox?.Options is not { } options)
                continue;
            if (!Catalog.TryGetValue(SettingIdAliases.Normalize(def.Id), out var s))
                continue;
            comparedSelections++;
            if (s.States.Count != options.Count)
            {
                offenders.Add($"{def.Id}: States.Count {s.States.Count} != Options.Count {options.Count}");
                continue;
            }
            for (int i = 0; i < options.Count; i++)
                if (!string.Equals(s.States[i].Label, options[i].DisplayName, System.StringComparison.Ordinal))
                    offenders.Add($"{def.Id}[{i}]: State.Label '{s.States[i].Label}' != Option.DisplayName '{options[i].DisplayName}'");
        }

        Assert.True(comparedSelections > 0, "no selections compared - population scoping bug");
        Assert.True(offenders.Count == 0,
            "catalog selection States must be in option order with Label == the option DisplayName (else " +
            "ResolveSelectionIndex diverges from the old ComboBox-based resolution):\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void Every_gate_selection_has_exactly_one_windows_default_anchor()
    {
        // The DeriveIsEnabled selection rule ("not the WindowsDefault state") is only well-defined when each gate-
        // population selection has EXACTLY ONE WindowsDefault anchor in its resolution context (AC for powercfg,
        // Always for registry). This pins the "a semantic state is not reliably a role" trap structurally: a future
        // authoring that drops or duplicates the default anchor fails here, not silently in the field.
        var offenders = new List<string>();
        foreach (var def in AllDefinitions())
        {
            bool pureReg = RegistryToggleEquivalenceHarness.IsPureRegistrySelection(def);
            bool purePwr = RegistryToggleEquivalenceHarness.IsPurePowerCfgSelection(def);
            if (!pureReg && !purePwr) continue;
            if (!Catalog.TryGetValue(def.Id, out var s)) continue;

            int windowsDefaults = s.States.Count(st =>
                st.HasRole(RoleKind.WindowsDefault, PowerContext.Always) ||
                st.HasRole(RoleKind.WindowsDefault, PowerContext.AC));
            if (windowsDefaults != 1)
                offenders.Add($"{def.Id}={windowsDefaults}");
        }

        Assert.True(offenders.Count == 0,
            "Every gate-population selection must carry exactly one Windows-default anchor state for the DeriveIsEnabled " +
            "invariant to be well-defined. Offenders (id=count): " + string.Join(", ", offenders));
    }

    [Fact]
    public void Every_gate_numeric_has_an_ac_windows_default()
    {
        // The numeric IsEnabled rule compares against Numeric.WindowsDefault(AC); assert every gate-population numeric
        // supplies one, so the rule is never silently a no-op (return false for lack of an anchor).
        var offenders = new List<string>();
        foreach (var def in AllDefinitions())
        {
            if (!RegistryToggleEquivalenceHarness.IsPurePowerCfgNumeric(def)) continue;
            if (!Catalog.TryGetValue(def.Id, out var s)) continue;
            if (s.Numeric is null || !s.Numeric.WindowsDefault.Any(cv => cv.Context == PowerContext.AC))
                offenders.Add(def.Id);
        }

        Assert.True(offenders.Count == 0,
            "Every gate-population numeric must carry a WindowsDefault(AC) value. Offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public async Task Provider_pairs_the_win10_aliases_to_their_canonical_setting()
    {
        // Slice 5: the 6 OS-merged "-win10" ThisPC-folder defs are absent from SettingCatalog.All; the provider must
        // normalize them to their canonical merged Setting (like SettingsLoadingService) instead of reporting them
        // unpaired. Because a "-win10" def and its canonical peer pair to the SAME Setting and read the SAME live
        // detection, the provider must produce FIELD-IDENTICAL results for both - a machine-independent proof that the
        // alias pairing works (it holds on Win10 or Win11: both sides read whatever target is live on this build).
        var log = new Mock<ILogService>();
        var interactiveUser = new Mock<IInteractiveUserService>();
        interactiveUser.Setup(x => x.IsOtsElevation).Returns(false);
        var reg = new WindowsRegistryService(log.Object, interactiveUser.Object);
        var powerQuery = new PowerSettingsQueryService(log.Object);
        var factory = new SystemDetectionContextFactory(
            reg,
            new Mock<ISystemRestoreService>().Object,
            new Mock<IScheduledTaskService>().Object,
            powerQuery,
            log.Object);
        var detection = new CatalogDetectionService(factory, log.Object);
        // ResolveRawValuesToIndex (the selection value-match base) is pure, so the resolver needs no dependencies.
        var provider = new CatalogSettingStateProvider(detection, new ComboBoxResolver());

        var allDefs = AllDefinitions().ToList();
        var aliasIds = new[]
        {
            "explorer-customization-thispc-folder-desktop-win10",
            "explorer-customization-thispc-folder-documents-win10",
            "explorer-customization-thispc-folder-downloads-win10",
            "explorer-customization-thispc-folder-music-win10",
            "explorer-customization-thispc-folder-pictures-win10",
            "explorer-customization-thispc-folder-videos-win10",
        };

        var mismatches = new List<string>();
        int checkedPairs = 0;

        foreach (var aliasId in aliasIds)
        {
            var canonicalId = SettingIdAliases.Normalize(aliasId);
            Assert.NotEqual(aliasId, canonicalId); // sanity: it really is a retired alias

            var win10Def = allDefs.FirstOrDefault(d => d.Id == aliasId);
            var canonicalDef = allDefs.FirstOrDefault(d => d.Id == canonicalId);
            if (win10Def is null || canonicalDef is null)
            {
                mismatches.Add($"{aliasId}: def missing (win10={win10Def is not null}, canonical={canonicalDef is not null})");
                continue;
            }

            var states = await provider.GetStatesAsync(new[] { win10Def, canonicalDef });
            var win10 = states[aliasId];
            var canonical = states[canonicalId];

            // Paired now, not "unpaired".
            Assert.True(win10.Success, $"{aliasId} should pair to its canonical Setting, got unpaired ({win10.ErrorMessage})");
            checkedPairs++;

            // Field-identical to the canonical peer (both pair to the same Setting + read the same detection).
            CompareField(mismatches, aliasId, "IsEnabled", canonical.IsEnabled, win10.IsEnabled);
            CompareField(mismatches, aliasId, "CurrentValue", canonical.CurrentValue, win10.CurrentValue);
            CompareField(mismatches, aliasId, "Success", canonical.Success, win10.Success);
            CompareField(mismatches, aliasId, "AcValue", canonical.AcValue, win10.AcValue);
            CompareField(mismatches, aliasId, "DcValue", canonical.DcValue, win10.DcValue);
            CompareReadings(mismatches, aliasId, canonical.Readings, win10.Readings);
        }

        Assert.True(checkedPairs == aliasIds.Length, $"expected {aliasIds.Length} alias pairs, paired {checkedPairs}");
        Assert.True(mismatches.Count == 0, "win10 alias pairing diverged from the canonical peer:\n" + string.Join("\n", mismatches));
    }

    /// <summary>Regression (Phase 6.9): a selection for which the new engine yields NO resolved state label (null -
    /// StateDetectionEngine found no match, or the label isn't a verbatim option DisplayName) must fall back to the
    /// value-match the live UI consumed (ResolveRawValuesToIndex over the reads), NOT collapse to the Custom index.
    /// The old pipeline resolved these via discovery's value-match and the overlay's Selection branch preserved it via
    /// `return old`; the provider first kept only the label override, so the service dropdowns + delivery optimization
    /// regressed to Custom in the live UI. Machine-independent: the detection result is mocked, so it fails
    /// deterministically off-Windows if the value-match base is ever dropped again.</summary>
    [Fact]
    public async Task Selection_with_no_resolved_label_falls_back_to_value_match_not_Custom()
    {
        // gaming-sysmain-service: option index 1 is Start=3 ("Manual"). The engine reports NO label (the regression
        // trigger) but the live reads say Start=3, so the value-match must land on index 1, never Custom (-1).
        var sysmain = AllDefinitions().First(d => d.Id == "gaming-sysmain-service");

        var detection = new Mock<ICatalogDetectionService>();
        detection
            .Setup(d => d.DetectAsync(It.IsAny<IReadOnlyCollection<Setting>>()))
            .ReturnsAsync(new Dictionary<string, CatalogDetectionResult>
            {
                ["gaming-sysmain-service"] = new CatalogDetectionResult
                {
                    StateLabel = null,
                    Detected = false,
                    Readings = new Dictionary<string, object?> { ["Start"] = 3 },
                },
            });

        var provider = new CatalogSettingStateProvider(detection.Object, new ComboBoxResolver());

        var states = await provider.GetStatesAsync(new[] { sysmain });

        Assert.True(states.TryGetValue("gaming-sysmain-service", out var s));
        Assert.True(s!.Success);
        Assert.Equal(1, s.CurrentValue); // Start=3 value-matches "Manual" (index 1), not Custom (-1)
    }

    private static void CompareField(List<string> mismatches, string id, string field, object? left, object? right)
    {
        if (!ScalarEquals(left, right))
            mismatches.Add($"{id}.{field}: canonical={Fmt(left)} alias={Fmt(right)}");
    }

    private static void CompareReadings(
        List<string> mismatches,
        string id,
        IReadOnlyDictionary<string, object?>? left,
        IReadOnlyDictionary<string, object?>? right)
    {
        if (left is null && right is null)
            return;
        if (left is null || right is null)
        {
            mismatches.Add($"{id}.Readings: canonical={(left is null ? "<null>" : $"{{{left.Count}}}")} alias={(right is null ? "<null>" : $"{{{right.Count}}}")}");
            return;
        }
        var keys = left.Keys.Union(right.Keys);
        foreach (var key in keys)
        {
            object? a = left.TryGetValue(key, out var av) ? av : null;
            object? b = right.TryGetValue(key, out var bv) ? bv : null;
            if (!ScalarEquals(a, b))
                mismatches.Add($"{id}.Readings[{key}]: canonical={Fmt(a)} alias={Fmt(b)}");
        }
    }

    private static bool ScalarEquals(object? a, object? b)
    {
        if (a is null || b is null)
            return a is null && b is null;
        if (a is byte[] ba && b is byte[] bb)
            return ba.SequenceEqual(bb);
        return a.Equals(b);
    }

    private static string Fmt(object? v) => v switch
    {
        null => "<null>",
        byte[] bytes => "byte[" + string.Join(",", bytes) + "]",
        _ => $"{v} ({v.GetType().Name})",
    };
}
