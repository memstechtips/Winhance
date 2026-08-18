namespace Winhance.Core.Features.Common.Catalog;

/// <summary>A detection context that pre-fetches its asynchronous reads (scheduled tasks, powercfg values, the
/// active power plan) for a batch of settings up front, then serves every read synchronously from that cache.
/// The detection engine and detectors stay synchronous; the batch driver awaits <see cref="PrefetchAsync"/> once
/// before detecting.</summary>
public interface IPrefetchableDetectionContext : IDetectionContext
{
    /// <summary>Reads and caches the asynchronous values the supplied settings will need, so the subsequent
    /// synchronous reads are served from memory. Call once per batch, before detecting.</summary>
    Task PrefetchAsync(IReadOnlyCollection<Setting> settings);
}

/// <summary>Creates a fresh detection context for each detection batch. The context caches per-batch pre-fetched
/// reads, so it is single-use and must not be a shared singleton; the batch driver creates one, pre-fetches, then
/// detects.</summary>
public interface ISystemDetectionContextFactory
{
    IPrefetchableDetectionContext Create();
}
