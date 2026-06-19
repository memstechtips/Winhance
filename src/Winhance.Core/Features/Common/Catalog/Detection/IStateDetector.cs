namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Custom detection: returns the live state's <see cref="SettingState.Label"/> directly,
/// or null for Custom. Replaces the old DetectionType enum + special-discovery handlers + sentinel bag.</summary>
public interface IStateDetector
{
    string? Detect(IStateReadings readings);
}
