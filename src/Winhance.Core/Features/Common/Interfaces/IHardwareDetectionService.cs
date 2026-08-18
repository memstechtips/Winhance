namespace Winhance.Core.Features.Common.Interfaces;

// Synchronous because both answers are blocking OS queries with no async API; the caller decides whether to offload.
public interface IHardwareDetectionService
{
    // NULL when detection could not answer. Callers pick their own default for null - they disagree: a change receipt
    // wants true (show AC and DC), catalog filtering wants false. Queried once and cached; a battery is not fitted mid-session.
    bool? HasBattery();

    // Reads the FastSystemS4 (hybrid sleep) capability.
    bool SupportsHybridSleep();
}
