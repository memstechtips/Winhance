namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>Machine capabilities that are constant for the lifetime of the process. Synchronous because
/// both answers are blocking OS queries with no async API; the caller decides whether to offload.</summary>
public interface IHardwareDetectionService
{
    /// <summary>True with a battery, false without, and NULL when detection could not answer.
    /// Callers must pick their own default for null - they disagree: rendering a change receipt
    /// wants true (show both AC and DC), filtering the catalog wants false (don't offer a setting
    /// that cannot work here). Queried once and cached; a battery is not fitted mid-session.</summary>
    bool? HasBattery();

    /// <summary>True when the machine reports FastSystemS4 (hybrid sleep) support.</summary>
    bool SupportsHybridSleep();
}
