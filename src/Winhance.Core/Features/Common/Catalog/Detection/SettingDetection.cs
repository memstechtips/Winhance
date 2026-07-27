using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>
/// The complete result of detecting one setting: which state it landed on, and - when it landed on
/// none - the honest reason why.
/// </summary>
/// <param name="Label">The resolved state's label, or null for any non-<see cref="SettingDetectionOutcome.Resolved"/>
/// outcome.</param>
/// <param name="Outcome">Whether the setting resolved, and if not, why not.</param>
/// <param name="Detail">Human-readable diagnostic for a <see cref="SettingDetectionOutcome.Malformed"/>
/// or <see cref="SettingDetectionOutcome.Undetermined"/> outcome (which value, expected vs actual
/// registry kind, or the exception message). Null when resolved or plain Custom. Intended for the log
/// and for issue reports - never shown raw in the UI.</param>
public readonly record struct SettingDetection(string? Label, SettingDetectionOutcome Outcome, string? Detail = null)
{
    /// <summary>Placed on a known state.</summary>
    public static SettingDetection Resolved(string label) => new(label, SettingDetectionOutcome.Resolved);

    /// <summary>Present and readable, but matching no declared state.</summary>
    public static readonly SettingDetection Custom = new(null, SettingDetectionOutcome.Custom);

    /// <summary>Present but stored under the wrong registry type for its target.</summary>
    public static SettingDetection Malformed(string detail) =>
        new(null, SettingDetectionOutcome.Malformed, detail);

    /// <summary>Detection failed; the current value is unknown.</summary>
    public static SettingDetection Undetermined(string detail) =>
        new(null, SettingDetectionOutcome.Undetermined, detail);

    /// <summary>A label-or-Custom result: the shape every matcher returns.</summary>
    public static SettingDetection FromLabel(string? label) =>
        label is null ? Custom : Resolved(label);
}
