namespace Winhance.Core.Features.Common.Catalog;

/// <summary>Abstracts the actual system writes so the executor is testable without touching the registry,
/// task scheduler, or power API. The real Windows implementation is provided when the engine is wired in.
/// Each method returns true on success.</summary>
public interface IStateWriter
{
    bool WriteRegistry(RegTarget target, string path, object value);
    bool DeleteRegistry(RegTarget target, string path);
    bool EnsureRegistryKey(RegTarget target, string path);
    bool SetTask(TaskTarget target, bool enabled);
    bool RunEffect(Effect effect);
}
