using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

// Session-only like the hardware filter. Starts at the newest release rather than this PC's build, because an
// answer file is nearly always used for a fresh install from current media.
public sealed class TargetWindowsService(IEventBus eventBus, ILogService logService) : ITargetWindowsService
{
    public int? Build { get; private set; } = WindowsReleases.Latest.Build;

    public event EventHandler? Changed;

    public void Set(int? build)
    {
        if (Build == build) return;

        Build = build;
        eventBus.Publish(new FilterStateChangedEvent(build is not null));
        Changed?.Invoke(this, EventArgs.Empty);
        logService.Log(LogLevel.Info, $"Builder target Windows set to: {build?.ToString() ?? "all versions"}");
    }

    public void Reset() => Set(WindowsReleases.Latest.Build);
}
