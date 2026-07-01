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
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Phase 6.8 full-state-provider equivalence gate, run on Windows against the live machine. Proves the NEW
/// additive <see cref="CatalogSettingStateProvider"/> - which builds a complete <see cref="SettingStateResult"/> per
/// setting from the new catalog detection engine ALONE - matches the CURRENT live hybrid (the old discovery's
/// <see cref="SystemSettingsDiscoveryService.GetSettingStatesAsync"/> with <see cref="CatalogDetectionStateOverlay"/>
/// layered on, exactly as <c>CatalogDetectionOverlayHelper</c> applies it in the running app) for the catalog-paired
/// settings. Both tracks read the SAME live registry + power scheme via the SAME real service instances, so this is a
/// behavioural equivalence check, not a reimplementation.
///
/// Population: catalog-paired pure-registry toggles/selections + pure-powercfg selections/numerics (the
/// <c>RegistryToggleEquivalenceHarness.IsPure*</c> predicates), so only well-isolated detection mechanisms are
/// exercised. OS-merged settings (any catalog RegTarget carries an AppliesTo build gate) are excluded - their single-OS
/// old def legitimately differs from the multi-OS catalog setting, mirroring <c>CustomStateReadingsEquivalenceTests</c>.
///
/// RawValues is intentionally EXCLUDED from the comparison (option B): the provider deliberately does not resurrect it
/// (registry readings live on Readings, AC/DC on the typed fields). TooltipData, if present, is likewise out of scope.
///
/// IsEnabled is ALSO excluded from the live-hybrid comparison (decided done-right, Marco 2026-07-01): for a
/// selection/numeric the hybrid derives IsEnabled from old discovery's <c>.Any</c>/<c>!= 0</c> heuristic - the buggy
/// multi-target model this migration retires - so the hybrid is the WRONG oracle for it. The provider's IsEnabled
/// (the Windows-grounded "not in the Windows-default state" rule) is instead gated by the machine-INDEPENDENT
/// model-conformance facts in this class (the <c>IsEnabled_*</c> and <c>Every_gate_*</c> [Fact]s), which construct
/// readings and assert against what Windows ships - never the live hybrid.
///
/// Run: dotnet test --filter FullStateProviderEquivalence</summary>
public class FullStateProviderEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public FullStateProviderEquivalenceTests(ITestOutputHelper output) => _output = output;

    private static readonly IReadOnlyDictionary<string, Setting> Catalog = SettingCatalog.All.ToDictionary(s => s.Id);

    /// <summary>Every SettingDefinition the app ships, straight from the static feature providers (no DI, no
    /// Windows-version filtering) - the same population the other migration equivalence tests use.</summary>
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
    public async Task Provider_matches_GetSettingStatesAsync_plus_overlay_for_paired_settings()
    {
        // Real registry service reading the live machine; its two ctor deps are not exercised by the read path.
        var log = new Mock<ILogService>();
        var interactiveUser = new Mock<IInteractiveUserService>();
        interactiveUser.Setup(x => x.IsOtsElevation).Returns(false);
        var reg = new WindowsRegistryService(log.Object, interactiveUser.Object);

        // Real power query reading the live power scheme - shared by the old discovery AND the new context factory so
        // both tracks resolve powercfg AC/DC from the same source (must NOT be mocked, or powercfg settings diverge).
        var powerQuery = new PowerSettingsQueryService(log.Object);

        // OLD: the app's real hybrid base. Powercfg goes through the real power query; the other non-registry sources
        // (special-discovery, scheduled-task, system-restore) are no-op mocks - the population avoids those branches.
        var discovery = new SystemSettingsDiscoveryService(
            reg,
            log.Object,
            powerQuery,
            new Mock<ISpecialDiscoveryRegistry>().Object,
            new Mock<IScheduledTaskService>().Object,
            new Mock<ISystemRestoreService>().Object);

        // NEW: the real catalog detection engine over the live system context (same registry + same power query).
        var factory = new SystemDetectionContextFactory(
            reg,
            new Mock<ISystemRestoreService>().Object,
            new Mock<IScheduledTaskService>().Object,
            powerQuery,
            log.Object);
        var detection = new CatalogDetectionService(factory, log.Object);

        // The system under test: the full-state provider (new engine alone).
        var provider = new CatalogSettingStateProvider(detection);

        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);

        // Population: catalog-paired, well-isolated detection mechanisms (registry toggle/selection, powercfg
        // selection/numeric), excluding OS-merged build-gated catalog settings (their old single-OS def legitimately
        // differs). Power-plan-selection is included via IsPurePowerCfgSelection? No - it has no ComboBox, so it falls
        // outside every Pure* predicate; it is added explicitly below.
        var pairedDefs = AllDefinitions()
            .Where(d =>
                RegistryToggleEquivalenceHarness.IsPureRegistryToggle(d) ||
                RegistryToggleEquivalenceHarness.IsPureRegistrySelection(d) ||
                RegistryToggleEquivalenceHarness.IsPurePowerCfgSelection(d) ||
                RegistryToggleEquivalenceHarness.IsPurePowerCfgNumeric(d))
            .Where(d => catalogById.ContainsKey(d.Id))
            // Exclude OS-merged settings (any catalog RegTarget gated by build) - single-OS old def vs multi-OS catalog.
            .Where(d => catalogById[d.Id].Targets.OfType<RegTarget>().All(rt => rt.AppliesTo.Count == 0))
            .ToList();

        // Add the power-plan selection explicitly (an OptionSource setting; no ComboBox, so no Pure* predicate matches)
        // when it is catalog-paired, to exercise the dynamic-option branch.
        var powerPlanDef = AllDefinitions().FirstOrDefault(d => d.Id == Winhance.Core.Features.Common.Constants.SettingIds.PowerPlanSelection);
        if (powerPlanDef is not null && catalogById.ContainsKey(powerPlanDef.Id) && !pairedDefs.Any(d => d.Id == powerPlanDef.Id))
            pairedDefs.Add(powerPlanDef);

        // ORACLE: reproduce the live hybrid exactly as CatalogDetectionOverlayHelper does - old discovery base, then
        // the catalog overlay applied per setting.
        var oldStates = await discovery.GetSettingStatesAsync(pairedDefs);
        var pairedCatalogSettings = pairedDefs.Select(d => catalogById[d.Id]).ToList();
        var newResults = await detection.DetectAsync(pairedCatalogSettings);

        var hybrid = new Dictionary<string, SettingStateResult>();
        foreach (var def in pairedDefs)
        {
            if (!oldStates.TryGetValue(def.Id, out var oldState))
                continue;
            newResults.TryGetValue(def.Id, out var newResult);
            hybrid[def.Id] = CatalogDetectionStateOverlay.Apply(def, oldState, newResult);
        }

        // PROVIDER: the new engine alone.
        var provided = await provider.GetStatesAsync(pairedDefs);

        int compared = 0;
        var mismatches = new List<string>();

        foreach (var def in pairedDefs)
        {
            if (!hybrid.TryGetValue(def.Id, out var h) || !provided.TryGetValue(def.Id, out var p))
                continue;

            compared++;

            // IsEnabled is intentionally NOT compared against the hybrid: for a selection/numeric the hybrid's
            // IsEnabled is old discovery's `.Any`/`!= 0` heuristic - the buggy multi-target model this migration
            // retires - so the hybrid is the WRONG oracle for it. The provider's IsEnabled ("not in the Windows-
            // default state") is gated instead by the machine-independent model-conformance facts below (IsEnabled_*).
            CompareField(mismatches, def.Id, "CurrentValue", h.CurrentValue, p.CurrentValue);
            CompareField(mismatches, def.Id, "Success", h.Success, p.Success);
            CompareField(mismatches, def.Id, "ErrorMessage", h.ErrorMessage, p.ErrorMessage);
            CompareField(mismatches, def.Id, "AcValue", h.AcValue, p.AcValue);
            CompareField(mismatches, def.Id, "DcValue", h.DcValue, p.DcValue);
            CompareField(mismatches, def.Id, "DynamicSelection", h.DynamicSelection, p.DynamicSelection);
            CompareField(mismatches, def.Id, "DynamicSelectionName", h.DynamicSelectionName, p.DynamicSelectionName);
            CompareDynamicOptions(mismatches, def.Id, h.DynamicOptions, p.DynamicOptions);
            CompareReadings(mismatches, def.Id, h.Readings, p.Readings);
            // RawValues intentionally excluded (option B).
        }

        _output.WriteLine($"{compared - DistinctMismatchedSettings(mismatches)}/{compared} settings fully match ({pairedDefs.Count} paired)");
        if (mismatches.Count > 0)
        {
            _output.WriteLine("Mismatches:");
            foreach (var m in mismatches)
                _output.WriteLine($"  {m}");
        }

        Assert.True(compared > 0, "no paired settings were compared - the test is vacuous");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} field(s) differ between hybrid and provider - see output");
    }

    // ============================================================================================================
    //  Machine-INDEPENDENT model-conformance for IsEnabled (the done-right, Windows-grounded "not in the Windows-
    //  default state" rule). These construct readings and assert against what Windows ships - NOT the live hybrid,
    //  whose selection/numeric IsEnabled is old discovery's `.Any`/`!= 0` bug. They replace the IsEnabled half of the
    //  live-equivalence comparison (removed above).
    // ============================================================================================================

    private static bool Derive(Setting s, InputType inputType, string? stateLabel)
        => CatalogSettingStateProvider.DeriveIsEnabled(s, inputType, new CatalogDetectionResult { StateLabel = stateLabel });

    private static bool DeriveNumeric(Setting s, int? acReadingSystemUnits)
        => CatalogSettingStateProvider.DeriveIsEnabled(s, InputType.NumericRange, new CatalogDetectionResult { Value = acReadingSystemUnits });

    [Fact]
    public void IsEnabled_registry_selection_is_modified_from_windows_default()
    {
        // A 3-option service: Automatic = WindowsDefault, ManualRecommended = Recommended, Disabled = no role.
        // The hard instance: Disabled is neither default nor recommended, yet it IS a modification from the Windows
        // default, so it must read enabled - this is where `!WindowsDefault` and `HasRole(Recommended)` diverge.
        var s = Catalog["gaming-print-spooler-service"];
        Assert.False(Derive(s, InputType.Selection, "ServiceOption_Automatic"));         // Windows default -> not enabled
        Assert.True(Derive(s, InputType.Selection, "ServiceOption_ManualRecommended"));  // recommended, non-default -> enabled
        Assert.True(Derive(s, InputType.Selection, "ServiceOption_Disabled"));           // non-default, non-recommended -> enabled
        Assert.True(Derive(s, InputType.Selection, null));                               // Custom / unrecognised -> non-default -> enabled
    }

    [Fact]
    public void IsEnabled_powercfg_selection_uses_the_ac_context_default()
    {
        // SelectionStates(LidActions, recAC:1, recDC:1, defAC:1, defDC:1) -> option index 1 is the AC default. The
        // WindowsDefault role is context-scoped (AC), so IsEnabled must check the AC role, not the Always default.
        var s = Catalog["lid-close-action"];
        Assert.False(Derive(s, InputType.Selection, "Template_LidActions_Option_1"));    // AC default -> not enabled
        Assert.True(Derive(s, InputType.Selection, "Template_LidActions_Option_0"));     // non-default -> enabled
        Assert.True(Derive(s, InputType.Selection, "Template_LidActions_Option_3"));     // non-default -> enabled
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
        var toggle = Catalog.Values.First(x => x.Numeric is null && x.OptionSource is null
            && x.States.Any(st => st.Label == "Enabled"));
        Assert.True(Derive(toggle, InputType.Toggle, "Enabled"));
        Assert.False(Derive(toggle, InputType.Toggle, "Disabled"));
        Assert.False(Derive(toggle, InputType.Toggle, null));   // a Custom toggle -> not enabled
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
        var provider = new CatalogSettingStateProvider(detection);

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

    private static int DistinctMismatchedSettings(IEnumerable<string> mismatches)
        => mismatches.Select(m => m.Split('.')[0]).Distinct().Count();

    private static void CompareField(List<string> mismatches, string id, string field, object? hybrid, object? provider)
    {
        if (!ScalarEquals(hybrid, provider))
            mismatches.Add($"{id}.{field}: old={Fmt(hybrid)} new={Fmt(provider)}");
    }

    private static void CompareDynamicOptions(
        List<string> mismatches,
        string id,
        IReadOnlyList<DynamicOption>? hybrid,
        IReadOnlyList<DynamicOption>? provider)
    {
        if (hybrid is null && provider is null)
            return;
        if (hybrid is null || provider is null)
        {
            mismatches.Add($"{id}.DynamicOptions: old={(hybrid is null ? "<null>" : $"[{hybrid.Count}]")} new={(provider is null ? "<null>" : $"[{provider.Count}]")}");
            return;
        }
        if (hybrid.Count != provider.Count)
        {
            mismatches.Add($"{id}.DynamicOptions: old=[{hybrid.Count}] new=[{provider.Count}]");
            return;
        }
        for (int i = 0; i < hybrid.Count; i++)
        {
            // DynamicOption is a record - value equality over Label/Value/ExistsOnSystem.
            if (!Equals(hybrid[i], provider[i]))
                mismatches.Add($"{id}.DynamicOptions[{i}]: old={hybrid[i]} new={provider[i]}");
        }
    }

    private static void CompareReadings(
        List<string> mismatches,
        string id,
        IReadOnlyDictionary<string, object?>? hybrid,
        IReadOnlyDictionary<string, object?>? provider)
    {
        if (hybrid is null && provider is null)
            return;
        if (hybrid is null || provider is null)
        {
            mismatches.Add($"{id}.Readings: old={(hybrid is null ? "<null>" : $"{{{hybrid.Count}}}")} new={(provider is null ? "<null>" : $"{{{provider.Count}}}")}");
            return;
        }
        var keys = hybrid.Keys.Union(provider.Keys);
        foreach (var key in keys)
        {
            object? a = hybrid.TryGetValue(key, out var av) ? av : null;
            object? b = provider.TryGetValue(key, out var bv) ? bv : null;
            if (!ScalarEquals(a, b))
                mismatches.Add($"{id}.Readings[{key}]: old={Fmt(a)} new={Fmt(b)}");
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
