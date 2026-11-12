using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

// Session-only: unlike the Windows-version filter this is never persisted, so every launch starts
// with the machine's own hardware.
public sealed class HardwareFilterService(IEventBus eventBus, ILogService logService) : IHardwareFilterService
{
    public bool IsFilterEnabled { get; private set; } = true;

    public event EventHandler<bool>? FilterStateChanged;

    // FilterStateChangedEvent is shared with the Windows-version filter: BaseSettingsFeatureViewModel
    // reloads a feature's settings on it without asking which gate moved.
    public void Set(bool enabled)
    {
        if (IsFilterEnabled == enabled) return;

        IsFilterEnabled = enabled;
        eventBus.Publish(new FilterStateChangedEvent(enabled));
        FilterStateChanged?.Invoke(this, enabled);
        logService.Log(LogLevel.Info, $"Hardware filter toggled to: {(enabled ? "ON" : "OFF")}");
    }

    public void Reset() => Set(true);
}
