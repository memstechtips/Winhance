using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.TechnicalDetails;

// The builder performs no live reads of its own; everything it needs is in here plus the catalog Setting.
public sealed record SettingStateSnapshot
{
    public InputType InputType { get; init; }

    public bool IsSelected { get; init; }

    public int? SelectedIndex { get; init; }

    public int NumericValue { get; init; }
    public int AcValue { get; init; }
    public int DcValue { get; init; }
    public int AcNumericValue { get; init; }
    public int DcNumericValue { get; init; }

    public bool SupportsSeparateACDC { get; init; }
    public bool HasBattery { get; init; }

    public IReadOnlyList<ComboBoxDisplayOption> Options { get; init; } = [];

    public SettingDetectionOutcome Outcome { get; init; } = SettingDetectionOutcome.Resolved;

    public IReadOnlyDictionary<string, object>? Readings { get; init; }
}
