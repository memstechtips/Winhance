using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Helpers;

namespace Winhance.Infrastructure.Features.Common.Services;

/// <summary>
/// Full-state provider: builds a complete typed <see cref="SettingStateResult"/> per catalog
/// <see cref="Setting"/> from the catalog detection engine (<see cref="ICatalogDetectionService.DetectAsync"/>).
/// There is no untyped RawValues bag: the registry readings live on
/// <see cref="SettingStateResult.Readings"/> and the powercfg AC/DC on the typed <see cref="SettingStateResult.AcValue"/>/
/// <see cref="SettingStateResult.DcValue"/> fields. Gated by <c>CatalogSettingStateProviderConformanceTests</c>.
/// </summary>
public sealed class CatalogSettingStateProvider : ICatalogSettingStateProvider
{
    private readonly ICatalogDetectionService _detection;
    private readonly IComboBoxResolver _comboBoxResolver;

    public CatalogSettingStateProvider(ICatalogDetectionService detection, IComboBoxResolver comboBoxResolver)
    {
        _detection = detection;
        _comboBoxResolver = comboBoxResolver;
    }

    /// <summary>Builds a complete <see cref="SettingStateResult"/> per catalog <see cref="Setting"/>. A Setting
    /// is already the canonical merged catalog entry, so there is no <c>SettingIdAliases</c> normalization or
    /// -win10 alias pairing to do. Dedupes the detection input by Id defensively and keys each result by
    /// Setting.Id.</summary>
    public async Task<Dictionary<string, SettingStateResult>> GetStatesAsync(IReadOnlyList<Setting> settings)
    {
        var detectionInput = settings
            .GroupBy(s => s.Id)
            .Select(g => g.First())
            .ToList();

        var detected = await _detection.DetectAsync(detectionInput).ConfigureAwait(false);

        var results = new Dictionary<string, SettingStateResult>();
        foreach (var setting in settings)
        {
            var r = detected.TryGetValue(setting.Id, out var dr) ? dr : null;
            results[setting.Id] = Map(setting, r);
        }

        return results;
    }

    /// <summary>Maps one <see cref="CatalogDetectionResult"/> onto a complete
    /// <see cref="SettingStateResult"/>. Branches on the catalog <see cref="ControlKind"/> (derived from the
    /// setting shape).</summary>
    private SettingStateResult Map(Setting catalogSetting, CatalogDetectionResult? r)
    {
        if (r is null)
        {
            // The engine produced no entry for this setting (it always populates one per input, so this is
            // defensive).
            return new SettingStateResult { Success = false, ErrorMessage = "no detection result" };
        }

        // Common fields for every input type (no untyped RawValues - the registry readings are on Readings
        // and the powercfg AC/DC on the typed AcValue/DcValue fields). A setting is Success by default - a
        // legitimate Custom state has Detected=false but still reports Success=true, so Success must NOT track
        // r.Detected. IsEnabled is derived from the model alone (see DeriveIsEnabled).
        var result = new SettingStateResult
        {
            Success = true,
            ErrorMessage = null,
            IsEnabled = DeriveIsEnabled(catalogSetting, r),
            AcValue = r.AcValue,
            DcValue = r.DcValue,
            Readings = r.Readings,
        };

        // The power-plan (a dynamic-option source) carries its options/selection on the result; its CurrentValue
        // resolves to the literal 0.
        if (catalogSetting.OptionSource is not null)
        {
            return result with
            {
                CurrentValue = 0,
                DynamicOptions = r.Options,
                DynamicSelection = r.StateLabel,
                DynamicSelectionName = r.DynamicSelectionName,
            };
        }

        switch (catalogSetting.Control)
        {
            case ControlKind.Toggle:
                // IsEnabled (the switch position) is already derived from the resolved "Enabled"/"Disabled" label;
                // a toggle carries no CurrentValue.
                return result;

            case ControlKind.Selection:
                // Two-tier resolution: resolve the selection index from the StateLabel; when it resolves use it,
                // else fall back to the value-match (ComboBoxResolver.ResolveRawValuesToIndex over the
                // reconstructed reads). Resolving from the StateLabel ALONE maps a null / unmatched label to
                // Custom - so any selection for which the engine yields no resolved state label
                // (StateDetectionEngine returns null) would regress to Custom; the value-match fallback recovers
                // it. This hit the service dropdowns and delivery optimization.
                var labelIndex = ResolveSelectionIndex(catalogSetting, r.StateLabel);
                if (labelIndex != ComboBoxConstants.CustomStateIndex)
                    return result with { CurrentValue = labelIndex };
                var reads = CustomStateValueReconstructor.Build(catalogSetting, result)
                    .ToDictionary(kv => kv.Key, kv => kv.Value);
                return result with { CurrentValue = _comboBoxResolver.ResolveRawValuesToIndex(catalogSetting, reads) };

            case ControlKind.Slider:
                // The slider's value IS the raw AC powercfg value index (r.Value); both box int?/null.
                return result with { CurrentValue = r.Value };

            default:
                // Action (and any other) carries no detectable state; leave CurrentValue null.
                return result;
        }
    }

    /// <summary>Derives IsEnabled - the "this setting is NOT in the state/value Windows ships" verdict - from
    /// the model. Decided done-right on 2026-07-01 (Marco): the Windows-grounded rule, anchored on the OBJECTIVE
    /// <see cref="RoleKind.WindowsDefault"/> role/value (what Windows ships), NOT the subjective
    /// <see cref="RoleKind.Recommended"/> role (which shifts per release and would mis-report a
    /// deliberately-changed setting). The IsEnabled gate is a machine-independent model-conformance assertion
    /// (see CatalogSettingStateProviderConformanceTests). Per input type:
    /// <list type="bullet">
    ///   <item>Numeric: the detected AC reading (system units, converted to display units) differs from the
    ///     WindowsDefault AC value. No reading / no WindowsDefault anchor -> false (cannot be "modified").</item>
    ///   <item>Toggle/CheckBox: the switch position = the literal "Enabled" state (a toggle's catalog States are always
    ///     labelled "Enabled"/"Disabled" by the converter, never semantic).</item>
    ///   <item>Selection: the detected state is NOT a WindowsDefault-role state, in the resolution context (AC for a
    ///     powercfg selection - detection resolves on AC; Always for a registry selection). A Custom/unrecognised read
    ///     (no matching state, no fallback) is non-default -> true.</item>
    ///   <item>Dynamic-option source (power plan) / Action / a selection with NO WindowsDefault anchor (the special
    ///     dns/system-tray detectors, all outside this gate's population) -> false, deferred.</item>
    /// </list></summary>
    internal static bool DeriveIsEnabled(Setting catalogSetting, CatalogDetectionResult r)
    {
        // Numeric (stateless slider): modified from the Windows-default AC value. r.Value is the raw AC powercfg
        // reading in SYSTEM units; Numeric.WindowsDefault is in DISPLAY units (the converter pre-applied the same
        // ConvertSystemToDisplay), so convert the reading to display units before comparing - never compare raw.
        if (catalogSetting.Numeric is { } numeric)
        {
            if (r.Value is not int rawAc)
                return false;
            var defAc = numeric.WindowsDefault.FirstOrDefault(cv => cv.Context == PowerContext.AC);
            if (defAc is null)
                return false;
            return RecommendedSettingsResolver.ConvertSystemToDisplayUnits(rawAc, numeric.Units) != defAc.Value;
        }

        // Toggle: the switch position (a toggle's States are always the literal "Enabled"/"Disabled").
        if (catalogSetting.Control == ControlKind.Toggle)
            return r.StateLabel == "Enabled";

        // No Windows-default anchor to be "modified" from: the power-plan option source and Action settings.
        if (catalogSetting.OptionSource is not null || catalogSetting.Control == ControlKind.Action)
            return false;

        // Selection: NOT in the Windows-default option, in the resolution context. HasRole defaults to
        // PowerContext.Always; a powercfg default role is context-scoped (AC/DC) so check AC explicitly - detection
        // resolves a powercfg selection on AC. A registry-selection state never carries an AC role and a powercfg
        // state never carries an Always role, so the OR selects the right anchor for either kind.
        static bool IsWindowsDefaultState(SettingState s) =>
            s.HasRole(RoleKind.WindowsDefault, PowerContext.Always) ||
            s.HasRole(RoleKind.WindowsDefault, PowerContext.AC);

        // No state is a Windows default (the special dns/system-tray detectors) -> no anchor -> defer (false).
        if (!catalogSetting.States.Any(IsWindowsDefaultState))
            return false;

        var resolved = r.StateLabel is { } label
            ? catalogSetting.States.FirstOrDefault(s => string.Equals(s.Label, label, System.StringComparison.Ordinal))
            : null;

        // A Custom/unrecognised read (no matching state and no fallback) is non-default -> enabled.
        return resolved is null || !IsWindowsDefaultState(resolved);
    }

    /// <summary>Resolves a state label to the option index the selection view-model consumes: the first
    /// catalog <see cref="SettingState"/> whose <c>Label</c> equals the label (Ordinal), else
    /// <see cref="ComboBoxConstants.CustomStateIndex"/> (-1) for a Custom / null / state-less selection.
    /// CatalogSettingStateProviderConformanceTests' Every_selection_has_distinct_non_empty_state_labels pins
    /// the property this first-match lookup depends on -- duplicate or blank Labels would silently resolve to
    /// the wrong option. The StateLabel already IS a catalog State Label, so this is the natural match.</summary>
    private static int ResolveSelectionIndex(Setting setting, string? label)
    {
        if (label is not null)
        {
            var states = setting.States;
            for (int i = 0; i < states.Count; i++)
            {
                if (string.Equals(states[i].Label, label, System.StringComparison.Ordinal))
                    return i;
            }
        }
        return ComboBoxConstants.CustomStateIndex;
    }
}
