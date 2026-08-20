using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

// Session-only: unlike the Windows-version filter this is never persisted, so every launch starts
// with the machine's own hardware.
public sealed class HardwareFilterService : IHardwareFilterService
{
    private readonly IEventBus _eventBus;
    private readonly ILogService _log;

    public HardwareFilterService(IEventBus eventBus, ILogService log)
    {
        _eventBus = eventBus;
        _log = log;
    }

    public bool IsFilterEnabled { get; private set; } = true;

    public event EventHandler<bool>? FilterStateChanged;

    // FilterStateChangedEvent is shared with the Windows-version filter: BaseSettingsFeatureViewModel
    // reloads a feature's settings on it without asking which gate moved.
    public Task SetAsync(bool enabled)
    {
        if (IsFilterEnabled == enabled) return Task.CompletedTask;

        IsFilterEnabled = enabled;
        _eventBus.Publish(new FilterStateChangedEvent(enabled));
        FilterStateChanged?.Invoke(this, enabled);
        _log.Log(LogLevel.Info, $"Hardware filter toggled to: {(enabled ? "ON" : "OFF")}");
        return Task.CompletedTask;
    }

    public Task ResetAsync() => SetAsync(true);
}
