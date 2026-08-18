using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Catalog;

public readonly record struct SettingDetection(string? Label, SettingDetectionOutcome Outcome, string? Detail = null)
{
    public static SettingDetection Resolved(string label) => new(label, SettingDetectionOutcome.Resolved);

    public static readonly SettingDetection Custom = new(null, SettingDetectionOutcome.Custom);

    public static SettingDetection Malformed(string detail) =>
        new(null, SettingDetectionOutcome.Malformed, detail);

    public static SettingDetection Undetermined(string detail) =>
        new(null, SettingDetectionOutcome.Undetermined, detail);

    public static SettingDetection FromLabel(string? label) =>
        label is null ? Custom : Resolved(label);
}
