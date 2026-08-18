using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

public sealed class PendingRestartService(IEventBus eventBus, ILogService logService) : IPendingRestartService
{
    // Settings are applied from background threads, and in parallel during a bulk apply, so every read
    // and write of the set is gated. PendingSettingIds hands back a copy for the same reason - the UI
    // enumerates it on the dispatcher thread while applies may still be running.
    private readonly HashSet<string> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public bool IsPending
    {
        get { lock (_gate) { return _pending.Count > 0; } }
    }

    public IReadOnlyCollection<string> PendingSettingIds
    {
        get { lock (_gate) { return _pending.ToArray(); } }
    }

    public void Register(string settingId)
    {
        if (string.IsNullOrWhiteSpace(settingId))
            return;

        lock (_gate)
        {
            if (!_pending.Add(settingId))
                return; // already registered - no state change, so no event
        }

        logService.Log(LogLevel.Debug,
            $"[PendingRestartService] '{settingId}' applied; Explorer restart pending");
        eventBus.Publish(new PendingRestartChangedEvent { IsPending = true });
    }

    public void Clear()
    {
        lock (_gate)
        {
            if (_pending.Count == 0)
                return; // nothing pending - no state change, so no event

            _pending.Clear();
        }

        logService.Log(LogLevel.Debug, "[PendingRestartService] Pending Explorer restart cleared");
        eventBus.Publish(new PendingRestartChangedEvent { IsPending = false });
    }
}
