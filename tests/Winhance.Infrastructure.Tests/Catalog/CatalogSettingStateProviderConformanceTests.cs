using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

/// <summary>Machine-INDEPENDENT conformance for the <see cref="CatalogSettingStateProvider"/>. These construct
/// readings/detection results directly and assert against what Windows ships and the catalog model, never a live
/// comparison. Covers: the Windows-grounded IsEnabled rule (the <c>IsEnabled_*</c> facts + the
/// <c>Every_gate_*</c> structural invariants that keep the rule well-defined) and the selection value-match
/// fallback (the Custom-regression guard).
///
/// Run: dotnet test --filter CatalogSettingStateProviderConformance</summary>
public class CatalogSettingStateProviderConformanceTests
{
    private static readonly IReadOnlyDictionary<string, Setting> Catalog = SettingCatalog.All.ToDictionary(s => s.Id);

    // ============================================================================================================
    //  Gate-population predicates. Catalog-side translations of the IsPure* classifiers (IsPureRegistrySelection /
    //  IsPurePowerCfgSelection / IsPurePowerCfgNumeric). Faithful: "pure" == exactly one DETECTION mechanism and no
    //  custom detector. NOTE a PowerCfgTarget's EnablementKey is a NESTED RegTarget, not a top-level Target, so
    //  OfType over Targets never sees an enablement key - which is precisely what these predicates mean by
    //  "no registry settings".
    // ============================================================================================================

    private static bool IsPureRegistrySelection(Setting s) =>
        s.Control == ControlKind.Selection
        && s.Detector is null
        && s.Targets.OfType<RegTarget>().Any()
        && !s.Targets.OfType<PowerCfgTarget>().Any()
        && !s.Targets.OfType<TaskTarget>().Any();

    private static bool IsPurePowerCfgSelection(Setting s) =>
        s.Control == ControlKind.Selection
        && s.Detector is null
        && s.Targets.OfType<PowerCfgTarget>().Any()
        && !s.Targets.OfType<RegTarget>().Any()
        && !s.Targets.OfType<TaskTarget>().Any();

    private static bool IsPurePowerCfgNumeric(Setting s) =>
        s.Control == ControlKind.Slider
        && s.Detector is null
        && s.Targets.OfType<PowerCfgTarget>().Any()
        && !s.Targets.OfType<RegTarget>().Any()
        && !s.Targets.OfType<TaskTarget>().Any();

    // ============================================================================================================
    //  Machine-INDEPENDENT model-conformance for IsEnabled (the Windows-grounded "not in the Windows-default
    //  state" rule). These construct readings and assert against what Windows ships.
    // ============================================================================================================

    private static readonly WinBuild Win10 = new(19045);
    private static readonly WinBuild Win11 = new(26200);

    private static bool Derive(Setting s, string? stateLabel, WinBuild? build = null)
        => CatalogSettingStateProvider.DeriveIsEnabled(s, new CatalogDetectionResult { StateLabel = stateLabel }, build ?? Win11);

    private static bool DeriveNumeric(Setting s, int? acReadingSystemUnits)
        => CatalogSettingStateProvider.DeriveIsEnabled(s, new CatalogDetectionResult { Value = acReadingSystemUnits }, Win11);

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
        // WindowsDefault AC = 5 (%), per the shipped Balanced scheme in the image SYSTEM hives.
        // "%" converts system->display 1:1.
        var s = Catalog["processor-min-state"];
        Assert.False(DeriveNumeric(s, 5));      // at the Windows default -> not enabled
        Assert.True(DeriveNumeric(s, 0));       // 0% != 5 -> modified -> enabled
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
    public void Every_gate_selection_has_exactly_one_windows_default_anchor()
    {
        // The DeriveIsEnabled selection rule ("not the WindowsDefault state") is only well-defined when each gate-
        // population selection has EXACTLY ONE WindowsDefault anchor in its resolution context (AC for powercfg,
        // Always for registry). This pins the "a semantic state is not reliably a role" trap structurally: a future
        // authoring that drops or duplicates the default anchor fails here, not silently in the field.
        var population = SettingCatalog.All
            .Where(s => IsPureRegistrySelection(s) || IsPurePowerCfgSelection(s))
            .ToList();
        Assert.NotEmpty(population);   // vacuity guard: a silently-narrowed predicate must fail, not pass over nothing

        var offenders = new List<string>();
        foreach (var s in population)
        {
            // Per-build counting: an OS-divergent selection may legitimately anchor DIFFERENT states per build,
            // or have NO anchor on one build (theme-mode-windows on Win10, whose true default is the
            // apps-light/system-dark mix - not a representable state; DeriveIsEnabled defers there). What must
            // never happen: two anchors live on the same build (ambiguous), or no anchor on ANY build (the
            // dropped-anchor trap this fact originally pinned).
            int CountFor(WinBuild b) => s.States.Count(st =>
                st.HasRole(RoleKind.WindowsDefault, b, PowerContext.Always) ||
                st.HasRole(RoleKind.WindowsDefault, b, PowerContext.AC));
            int w10 = CountFor(Win10), w11 = CountFor(Win11);
            if (w10 > 1 || w11 > 1 || (w10 == 0 && w11 == 0))
                offenders.Add($"{s.Id}=w10:{w10},w11:{w11}");
        }

        Assert.True(offenders.Count == 0,
            "Every gate-population selection must carry at most one Windows-default anchor per build, and at least " +
            "one on some build, for the DeriveIsEnabled invariant to be well-defined. Offenders: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Os_divergent_selection_resolves_its_anchor_per_build()
    {
        // theme-mode-windows: Light Mode is the Windows default ONLY on Win11; Win10's true default is the
        // apps-light/system-dark mix (not a representable state), so IsEnabled defers there.
        var s = Catalog["theme-mode-windows"];
        Assert.False(Derive(s, "Light Mode", Win11));   // at the Win11 default -> not enabled
        Assert.True(Derive(s, "Dark Mode", Win11));     // non-default -> enabled
        Assert.True(Derive(s, null, Win11));            // Custom -> enabled
        Assert.False(Derive(s, "Light Mode", Win10));   // no Win10 anchor -> deferred
        Assert.False(Derive(s, null, Win10));           // no Win10 anchor -> deferred
    }

    [Fact]
    public void Every_gate_numeric_has_an_ac_windows_default()
    {
        // The numeric IsEnabled rule compares against Numeric.WindowsDefault(AC); assert every gate-population numeric
        // supplies one, so the rule is never silently a no-op (return false for lack of an anchor).
        var population = SettingCatalog.All.Where(IsPurePowerCfgNumeric).ToList();
        Assert.NotEmpty(population);   // vacuity guard: a silently-narrowed predicate must fail, not pass over nothing

        var offenders = new List<string>();
        foreach (var s in population)
        {
            if (s.Numeric is null || !s.Numeric.WindowsDefault.Any(cv => cv.Context == PowerContext.AC))
                offenders.Add(s.Id);
        }

        Assert.True(offenders.Count == 0,
            "Every gate-population numeric must carry a WindowsDefault(AC) value. Offenders: " + string.Join(", ", offenders));
    }

    /// <summary>Regression: a selection for which the engine yields NO resolved state label (null -
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
        var sysmain = SettingCatalog.Find("gaming-sysmain-service");
        Assert.NotNull(sysmain);

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

        var version = new Mock<IWindowsVersionService>();
        version.Setup(v => v.GetWindowsBuildNumber()).Returns(26200);
        version.Setup(v => v.GetWindowsBuildRevision()).Returns(0);
        var provider = new CatalogSettingStateProvider(detection.Object, new ComboBoxResolver(version.Object), version.Object);

        var states = await provider.GetStatesAsync(new[] { sysmain! });

        Assert.True(states.TryGetValue("gaming-sysmain-service", out var s));
        Assert.True(s!.Success);
        Assert.Equal(1, s.CurrentValue); // Start=3 value-matches "Manual" (index 1), not Custom (-1)
    }

    /// <summary>The invariant ResolveSelectionIndex rests on. It resolves a state label to an option index by
    /// taking the FIRST State whose Label matches (Ordinal) -- which is only well-defined if a selection's Labels
    /// are DISTINCT and non-empty. This pins the part that matters: duplicate or blank Labels would make the
    /// first-match resolution ambiguous and silently return the wrong option index.</summary>
    [Fact]
    public void Every_selection_has_distinct_non_empty_state_labels()
    {
        var selections = SettingCatalog.All.Where(s => s.Control == ControlKind.Selection).ToList();
        Assert.NotEmpty(selections);

        var offenders = new List<string>();
        foreach (var s in selections)
        {
            if (s.States.Any(st => string.IsNullOrWhiteSpace(st.Label)))
                offenders.Add($"{s.Id}: has a blank state Label");

            var dupes = s.States.GroupBy(st => st.Label, System.StringComparer.Ordinal)
                .Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (dupes.Count > 0)
                offenders.Add($"{s.Id}: duplicate state Labels [{string.Join(", ", dupes)}]");
        }

        Assert.True(offenders.Count == 0,
            "ResolveSelectionIndex takes the FIRST state whose Label matches, so a selection's Labels must be "
                + "distinct and non-empty:" + "\n" + string.Join("\n", offenders));
    }
}
