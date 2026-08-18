using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Catalog;

internal sealed class SystemDetectionContextFactory : ISystemDetectionContextFactory
{
    private readonly IWindowsRegistryService _reg;
    private readonly ISystemRestoreService _restore;
    private readonly IScheduledTaskStateService _tasks;
    private readonly IPowerSettingsQueryService _power;
    private readonly ILogService _log;

    public SystemDetectionContextFactory(
        IWindowsRegistryService reg,
        ISystemRestoreService restore,
        IScheduledTaskStateService tasks,
        IPowerSettingsQueryService power,
        ILogService log)
    {
        _reg = reg;
        _restore = restore;
        _tasks = tasks;
        _power = power;
        _log = log;
    }

    public IPrefetchableDetectionContext Create() =>
        new SystemDetectionContext(_reg, _restore, _tasks, _power, _log);
}
