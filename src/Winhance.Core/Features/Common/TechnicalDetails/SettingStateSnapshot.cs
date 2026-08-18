using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.TechnicalDetails;

/// <summary>
/// Immutable view of a setting's resolved live state, handed to <see cref="TechnicalDetailsBuilder"/>.
/// The builder performs no live reads of its own — everything it needs is in here plus the
/// <see cref="Catalog.Setting"/> model.
/// </summary>
public sealed record SettingStateSnapshot
{
    public InputType InputType { get; init; }

    /// <summary>Toggle/CheckBox: whether the setting is on.</summary>
    public bool IsSelected { get; init; }

    /// <summary>Selection: index into <see cref="Catalog.Setting.States"/>, or null when custom.</summary>
    public int? SelectedIndex { get; init; }

    public int NumericValue { get; init; }
    public int AcValue { get; init; }
    public int DcValue { get; init; }
    public int AcNumericValue { get; init; }
    public int DcNumericValue { get; init; }

    public bool SupportsSeparateACDC { get; init; }
    public bool HasBattery { get; init; }

    /// <summary>The dropdown options as rendered, 1:1 and in order with the setting's states.</summary>
    public IReadOnlyList<ComboBoxDisplayOption> Options { get; init; } = [];

    /// <summary>
    /// Whether detection placed the setting on a known option. Anything other than
    /// <see cref="SettingDetectionOutcome.Resolved"/> means no option is marked current, which is the
    /// only case where reporting the live readings tells the user something new.
    /// </summary>
    public SettingDetectionOutcome Outcome { get; init; } = SettingDetectionOutcome.Resolved;

    /// <summary>
    /// Live per-registry-target readings keyed by <c>ValueName ?? "KeyExists"</c>, captured when the
    /// setting resolved to Custom. Null when detection matched an option (nothing to report) or when
    /// it failed outright (nothing to report it from).
    /// </summary>
    public IReadOnlyDictionary<string, object>? Readings { get; init; }
}
