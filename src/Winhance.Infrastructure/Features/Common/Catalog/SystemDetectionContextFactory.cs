using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Optimize.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Catalog;

/// <summary>Builds a fresh <see cref="SystemDetectionContext"/> for each detection batch, injecting the live
/// Windows read services. A new instance per batch keeps each batch's pre-fetch cache isolated.</summary>
public sealed class SystemDetectionContextFactory : ISystemDetectionContextFactory
{
    private readonly IWindowsRegistryService _reg;
    private readonly ISystemRestoreService _restore;
    private readonly IScheduledTaskService _tasks;
    private readonly IPowerSettingsQueryService _power;
    private readonly IPowerService _powerService;
    private readonly ILogService _log;

    public SystemDetectionContextFactory(
        IWindowsRegistryService reg,
        ISystemRestoreService restore,
        IScheduledTaskService tasks,
        IPowerSettingsQueryService power,
        IPowerService powerService,
        ILogService log)
    {
        _reg = reg;
        _restore = restore;
        _tasks = tasks;
        _power = power;
        _powerService = powerService;
        _log = log;
    }

    public IPrefetchableDetectionContext Create() =>
        new SystemDetectionContext(_reg, _restore, _tasks, _power, _powerService, _log);
}
