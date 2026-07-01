using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Helpers;

namespace Winhance.Infrastructure.Features.Common.Services;

/// <summary>
/// Phase 6.8 full-state provider (additive): builds a complete typed <see cref="SettingStateResult"/> per setting
/// from the NEW catalog detection engine ALONE (<see cref="ICatalogDetectionService.DetectAsync"/>), with no call to
/// the old <c>SystemSettingsDiscoveryService.GetSettingStatesAsync</c>. It reproduces the field semantics of
/// <c>CatalogDetectionStateOverlay.Apply</c>, except it produces the WHOLE result (the overlay layers onto an
/// old-discovery base; this one builds from the <c>CatalogDetectionResult</c> alone).
///
/// Pairs a def to its catalog Setting by normalized Id (SettingIdAliases, so the retired OS-merged "-win10" variants
/// resolve to their canonical merged Setting); a def with no catalog peer returns an unsuccessful result rather than
/// throwing. RawValues is intentionally NOT produced (option B): the registry readings live on
/// <see cref="SettingStateResult.Readings"/> and AC/DC on the typed <see cref="SettingStateResult.AcValue"/>/
/// <see cref="SettingStateResult.DcValue"/> fields; the remaining auxiliary RawValues entries become typed in a later
/// increment. Wired to no consumer yet - gated by <c>FullStateProviderEquivalenceTests</c>.
/// </summary>
public sealed class CatalogSettingStateProvider : ICatalogSettingStateProvider
{
    private readonly ICatalogDetectionService _detection;

    public CatalogSettingStateProvider(ICatalogDetectionService detection)
    {
        _detection = detection;
    }

    public async Task<Dictionary<string, SettingStateResult>> GetStatesAsync(IReadOnlyList<SettingDefinition> settings)
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);

        // Pair each def to its catalog Setting by NORMALIZED id: a retired OS-merged "-win10" variant (absent from
        // SettingCatalog.All - the 6 "This PC" folder toggles merged into one build-gated Setting) resolves to its
        // canonical merged Setting via SettingIdAliases, mirroring the live UI pairing (SettingsLoadingService). A def
        // with no catalog peer even after normalizing gets a non-success result below (no throw). Each result stays
        // keyed by the ORIGINAL def id.
        Setting? Pair(SettingDefinition d) =>
            catalogById.TryGetValue(SettingIdAliases.Normalize(d.Id), out var s) ? s : null;

        // Detect over the DISTINCT canonical settings. Two OS variants can normalize to one canonical (only one loads
        // per OS in production), so dedupe by Id defensively rather than hand DetectAsync the same Setting twice.
        var detectionInput = settings
            .Select(Pair)
            .Where(s => s is not null)
            .Select(s => s!)
            .GroupBy(s => s.Id)
            .Select(g => g.First())
            .ToList();

        var detected = await _detection.DetectAsync(detectionInput).ConfigureAwait(false);

        var results = new Dictionary<string, SettingStateResult>();
        foreach (var def in settings)
        {
            if (Pair(def) is not { } catalogSetting)
            {
                // No catalog peer even after alias-normalizing - surface it, don't throw.
                results[def.Id] = new SettingStateResult { Success = false, ErrorMessage = "unpaired" };
                continue;
            }

            // Detection is keyed by the CANONICAL Setting.Id, which differs from def.Id for a normalized alias.
            var r = detected.TryGetValue(catalogSetting.Id, out var dr) ? dr : null;
            results[def.Id] = Map(def, catalogSetting, r);
        }

        return results;
    }

    /// <summary>Maps one new-engine <see cref="CatalogDetectionResult"/> (+ the def for InputType and the catalog
    /// setting for the dynamic-option source) onto a complete <see cref="SettingStateResult"/>, reproducing
    /// <c>CatalogDetectionStateOverlay.Apply</c>'s field semantics but built from the detection result alone.</summary>
    private static SettingStateResult Map(SettingDefinition def, Setting catalogSetting, CatalogDetectionResult? r)
    {
        if (r is null)
        {
            // The engine produced no entry for this paired setting (it always populates one per input, so this is
            // defensive); mirror the old discovery's per-setting failure shape.
            return new SettingStateResult { Success = false, ErrorMessage = "no detection result" };
        }

        // Common fields for every input type. RawValues is deliberately dropped (option B): the registry readings
        // are on Readings and the powercfg AC/DC on the typed AcValue/DcValue fields. A paired setting is Success
        // by default - a legitimate Custom state has Detected=false but the hybrid still reports Success=true, so
        // Success must NOT track r.Detected. IsEnabled is derived from the NEW model alone (see DeriveIsEnabled).
        var result = new SettingStateResult
        {
            Success = true,
            ErrorMessage = null,
            RawValues = null,
            IsEnabled = DeriveIsEnabled(catalogSetting, def.InputType, r),
            AcValue = r.AcValue,
            DcValue = r.DcValue,
            Readings = r.Readings,
        };

        // The power-plan (a dynamic-option source) carries its options/selection on the result and resolves its
        // CurrentValue exactly as the hybrid does: GetSettingStatesAsync treats it as a Selection, ResolveRawValuesToIndex
        // early-returns 0 because the def has no ComboBox with ValueMappings, and the overlay's Selection branch is a
        // no-op for the same reason - so the hybrid's CurrentValue is 0. Reproduce that literal 0 here.
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

        switch (def.InputType)
        {
            case InputType.Toggle:
            case InputType.CheckBox:
                // IsEnabled (the switch position) is already derived from the resolved "Enabled"/"Disabled" label;
                // a toggle carries no CurrentValue.
                return result;

            case InputType.Selection:
                // Resolve the new label back to the option index the view-model consumes, exactly as the overlay's
                // Selection branch does: first DisplayName match by Ordinal. No match / null label / no options ->
                // the Custom index (-1), matching ResolveRawValuesToIndex's unmatched fallback.
                return result with { CurrentValue = ResolveSelectionIndex(def, r.StateLabel) };

            case InputType.NumericRange:
                // The slider's value IS the raw AC powercfg value index (r.Value), the same int the hybrid stored as
                // RawValues["PowerCfgValue"] (the AC tuple member) and surfaced as CurrentValue. Both box int?/null.
                return result with { CurrentValue = r.Value };

            default:
                // Action (and any other) carries no detectable state; leave CurrentValue null.
                return result;
        }
    }

    /// <summary>Derives IsEnabled - the "this setting is NOT in the state/value Windows ships" verdict the old
    /// discovery exposed via its <c>.Any</c>/<c>!= 0</c> heuristic - from the NEW model ALONE. Decided done-right on
    /// 2026-07-01 (Marco): the Windows-grounded rule, anchored on the OBJECTIVE <see cref="RoleKind.WindowsDefault"/>
    /// role/value (what Windows ships), NOT the subjective <see cref="RoleKind.Recommended"/> role (which shifts per
    /// release and would mis-report a deliberately-changed setting). This replaces the old multi-target <c>.Any</c> lie
    /// the migration exists to retire, so a live divergence from the old hybrid here is OLD's bug - the IsEnabled gate
    /// is therefore a machine-independent model-conformance assertion (see FullStateProviderEquivalenceTests), NOT the
    /// live hybrid. Per input type:
    /// <list type="bullet">
    ///   <item>Numeric: the detected AC reading (system units, converted to display units) differs from the
    ///     WindowsDefault AC value. No reading / no WindowsDefault anchor -> false (cannot be "modified").</item>
    ///   <item>Toggle/CheckBox: the switch position = the literal "Enabled" state (a toggle's catalog States are always
    ///     labelled "Enabled"/"Disabled" by the converter, never semantic; the 297 toggles gate-proved this).</item>
    ///   <item>Selection: the detected state is NOT a WindowsDefault-role state, in the resolution context (AC for a
    ///     powercfg selection - detection resolves on AC; Always for a registry selection). A Custom/unrecognised read
    ///     (no matching state, no fallback) is non-default -> true.</item>
    ///   <item>Dynamic-option source (power plan) / Action / a selection with NO WindowsDefault anchor (the special
    ///     dns/system-tray detectors, all outside this gate's population) -> false, deferred to a later increment.</item>
    /// </list></summary>
    internal static bool DeriveIsEnabled(Setting catalogSetting, InputType inputType, CatalogDetectionResult r)
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

        // Toggle/CheckBox: the switch position (a toggle's States are always the literal "Enabled"/"Disabled").
        if (inputType is InputType.Toggle or InputType.CheckBox)
            return r.StateLabel == "Enabled";

        // No Windows-default anchor to be "modified" from: the power-plan option source and Action settings.
        if (catalogSetting.OptionSource is not null || inputType == InputType.Action)
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

    /// <summary>Resolves a new-engine state label to the option index the selection view-model consumes: the first
    /// option whose <c>DisplayName</c> equals the label (Ordinal), else <see cref="ComboBoxConstants.CustomStateIndex"/>
    /// (-1) for a Custom / null / option-less selection - mirroring the Selection branch of
    /// <c>CatalogDetectionStateOverlay.Apply</c> and the unmatched fallback of <c>ResolveRawValuesToIndex</c>.</summary>
    private static object ResolveSelectionIndex(SettingDefinition def, string? label)
    {
        var options = def.ComboBox?.Options;
        if (label is not null && options is not null)
        {
            for (int i = 0; i < options.Count; i++)
            {
                if (string.Equals(options[i].DisplayName, label, System.StringComparison.Ordinal))
                    return i;
            }
        }
        return ComboBoxConstants.CustomStateIndex;
    }
}
