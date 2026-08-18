using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Helpers;

namespace Winhance.Infrastructure.Features.Common.Services;

// No untyped RawValues bag: registry readings live on Readings, powercfg AC/DC on the typed AcValue/DcValue.
// Gated by CatalogSettingStateProviderConformanceTests.
internal sealed class CatalogSettingStateProvider : ICatalogSettingStateProvider
{
    private readonly ICatalogDetectionService _detection;
    private readonly IComboBoxResolver _comboBoxResolver;
    private readonly IWindowsVersionService _version;

    public CatalogSettingStateProvider(ICatalogDetectionService detection, IComboBoxResolver comboBoxResolver, IWindowsVersionService version)
    {
        _detection = detection;
        _comboBoxResolver = comboBoxResolver;
        _version = version;
    }

    // A Setting is already the canonical merged entry, so no alias normalization; dedupes by Id defensively.
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

    private SettingStateResult Map(Setting catalogSetting, CatalogDetectionResult? r)
    {
        if (r is null)
        {
            // The engine produced no entry for this setting (it always populates one per input, so this is
            // defensive).
            return new SettingStateResult { Success = false, ErrorMessage = "no detection result" };
        }

        // A legitimate Custom state has Detected=false but still reports Success=true, so Success must NOT track
        // r.Detected.
        var result = new SettingStateResult
        {
            Success = true,
            ErrorMessage = null,
            IsEnabled = DeriveIsEnabled(catalogSetting, r, new WinBuild(_version.GetWindowsBuildNumber(), _version.GetWindowsBuildRevision())),
            AcValue = r.AcValue,
            DcValue = r.DcValue,
            Readings = r.Readings,
        };

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
                // a toggle carries no CurrentValue. The outcome comes from the detection engine rather than being
                // re-inferred from "StateLabel is null" - that inference is exactly what conflated an unrecognized
                // value, a wrong stored type and a detection crash into one indistinguishable "Custom".
                return result with { Outcome = r.Outcome, OutcomeDetail = r.OutcomeDetail };

            case ControlKind.Selection:
                // Two-tier resolution: resolve the selection index from the StateLabel; when it resolves use it,
                // else fall back to the value-match (ComboBoxResolver.ResolveRawValuesToIndex over the
                // reconstructed reads). Resolving from the StateLabel ALONE maps a null / unmatched label to
                // Custom - so any selection for which the engine yields no resolved state label
                // (StateDetectionEngine returns null) would regress to Custom; the value-match fallback recovers
                // it. This hit the service dropdowns and delivery optimization.
                // Malformed and Undetermined are decided upstream (a wrong stored type / a crash), and no
                // amount of index resolution can rescue them - carry them straight through.
                if (r.Outcome is SettingDetectionOutcome.Malformed or SettingDetectionOutcome.Undetermined)
                {
                    return result with
                    {
                        CurrentValue = ComboBoxConstants.CustomStateIndex,
                        Outcome = r.Outcome,
                        OutcomeDetail = r.OutcomeDetail,
                    };
                }

                var labelIndex = ResolveSelectionIndex(catalogSetting, r.StateLabel);
                if (labelIndex != ComboBoxConstants.CustomStateIndex)
                    return result with { CurrentValue = labelIndex };
                var reads = CustomStateValueReconstructor.Build(catalogSetting, result)
                    .ToDictionary(kv => kv.Key, kv => kv.Value);
                var valueMatchIndex = _comboBoxResolver.ResolveRawValuesToIndex(catalogSetting, reads);
                return result with
                {
                    CurrentValue = valueMatchIndex,
                    Outcome = valueMatchIndex == ComboBoxConstants.CustomStateIndex
                        ? SettingDetectionOutcome.Custom
                        : SettingDetectionOutcome.Resolved,
                };

            case ControlKind.Slider:
                // The slider's value IS the raw AC powercfg value index (r.Value).
                return result with { CurrentValue = r.Value };

            default:
                return result;
        }
    }

    // IsEnabled = "NOT in the state/value Windows ships", anchored on the OBJECTIVE WindowsDefault role - NOT the
    // subjective Recommended role, which shifts per release and would mis-report a deliberately-changed setting.
    // Numeric: AC reading (display units) != WindowsDefault AC value; no reading or no anchor -> false. Toggle:
    // switch position == the literal "Enabled" state. Selection: detected state is not a WindowsDefault-role state
    // in the resolution context (AC for powercfg, Always for registry); Custom -> true. Dynamic-option / Action /
    // no-anchor selections -> false.
    internal static bool DeriveIsEnabled(Setting catalogSetting, CatalogDetectionResult r, WinBuild build)
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

        // Selection: NOT in the Windows-default option, in the resolution context, on the LIVE build (an
        // OS-divergent selection resolves its anchor per build, mirroring the reset resolver). HasRole's
        // build-aware overload admits unconditional roles too; a powercfg default role is context-scoped (AC/DC)
        // so check AC explicitly - detection resolves a powercfg selection on AC. A registry-selection state
        // never carries an AC role and a powercfg state never carries an Always role, so the OR selects the
        // right anchor for either kind.
        bool IsWindowsDefaultState(SettingState s) =>
            s.HasRole(RoleKind.WindowsDefault, build, PowerContext.Always) ||
            s.HasRole(RoleKind.WindowsDefault, build, PowerContext.AC);

        // No state is a Windows default ON THIS BUILD (the special dns/system-tray detectors, or an OS-divergent
        // selection whose true default is not a representable state here, e.g. theme-mode-windows on Windows 10,
        // where the shipped default is the apps-light/system-dark mix) -> no anchor -> defer (false).
        if (!catalogSetting.States.Any(IsWindowsDefaultState))
            return false;

        var resolved = r.StateLabel is { } label
            ? catalogSetting.States.FirstOrDefault(s => string.Equals(s.Label, label, System.StringComparison.Ordinal))
            : null;

        // A Custom/unrecognised read (no matching state and no fallback) is non-default -> enabled.
        return resolved is null || !IsWindowsDefaultState(resolved);
    }

    // Every_selection_has_distinct_non_empty_state_labels pins the distinct-label property this depends on.
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
