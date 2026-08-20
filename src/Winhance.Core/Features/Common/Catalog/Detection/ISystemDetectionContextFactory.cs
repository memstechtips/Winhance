namespace Winhance.Core.Features.Common.Catalog;

// Pre-fetches the async reads for a batch, then serves every read synchronously; the engine and detectors stay synchronous.
public interface IPrefetchableDetectionContext : IDetectionContext
{
    Task PrefetchAsync(IReadOnlyCollection<Setting> settings);
}

// Single-use per batch (it caches the pre-fetched reads); must not be a shared singleton.
public interface ISystemDetectionContextFactory
{
    IPrefetchableDetectionContext Create();
}
