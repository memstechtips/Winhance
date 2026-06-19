namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Custom detection for settings whose live state cannot be expressed as target-value matches
/// (DNS, system-tray icons, system restore, update policy, power plan). Returns the matching state's
/// Label, or null for Custom. Reads the system through the supplied context so detectors stay testable.</summary>
public interface IStateDetector
{
    string? Detect(Setting setting, IDetectionContext context);
}
