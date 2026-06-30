using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>A selectable option produced at runtime (not authored as a static State). <see cref="Value"/> is the
/// stored selection - for the power plan that is the scheme GUID, so detection matches the live selection directly
/// without an index round-trip. <see cref="ExistsOnSystem"/> is false for an option offered but not currently present
/// (e.g. a predefined power plan not installed on this machine - selecting it creates/imports it); the UI shows it as
/// available-to-create. Defaults true (an enumerated option is normally present).</summary>
public sealed record DynamicOption(string Label, string Value, bool ExistsOnSystem = true);

/// <summary>A setting whose selectable options are produced at runtime from the machine rather than authored as
/// static States (e.g. the installed power plans, which vary per PC). Replaces the old per-setting LoadDynamicOptions
/// flag and the SettingId-based special-casing. The source enumerates the live options and reports the current
/// selection from the same <see cref="IDetectionContext"/> the detectors use - so a dynamic-option setting needs no
/// separate current-selection detector.</summary>
public interface IDynamicOptionSource
{
    /// <summary>The machine's current options for this setting, in display order. Reads from the detection context's
    /// pre-fetched cache (synchronous, like the detectors).</summary>
    IReadOnlyList<DynamicOption> EnumerateOptions(IDetectionContext context);

    /// <summary>The currently-selected option's <see cref="DynamicOption.Value"/> (e.g. the active scheme GUID), or
    /// null when nothing is selected / readable.</summary>
    string? CurrentSelection(IDetectionContext context);

    /// <summary>The currently-selected option's RAW display NAME (e.g. the active power plan's OS name), or null. The
    /// option <see cref="DynamicOption.Label"/> is a localization key for a predefined plan, so the raw name is read
    /// separately here. Default null for sources with no separate raw name.</summary>
    string? CurrentSelectionName(IDetectionContext context) => null;
}

/// <summary>Runtime-sources the machine's installed power plans as the power-plan selection's options; the active
/// scheme GUID is both the per-option Value and the current selection. Supplants the old PowerPlanComboBoxService
/// index/GUID round-tripping and the `setting.Id == PowerPlanSelection` special-cases.</summary>
public sealed class PowerPlanOptionSource : IDynamicOptionSource
{
    public IReadOnlyList<DynamicOption> EnumerateOptions(IDetectionContext context) => context.InstalledPowerPlans();

    public string? CurrentSelection(IDetectionContext context) => context.ActivePowerPlanGuid();

    public string? CurrentSelectionName(IDetectionContext context) => context.ActivePowerPlanName();
}
