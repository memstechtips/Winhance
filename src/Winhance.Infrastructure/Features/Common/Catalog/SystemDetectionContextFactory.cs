using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Catalog;

internal sealed class SystemDetectionContextFactory : ISystemDetectionContextFactory
{
    private readonly IWindowsRegistryService _reg;
    private readonly ISystemRestoreService _restore;
    private readonly IScheduledTaskStateService _tasks;
    private readonly IPowerSettingsQueryService _power;
    private readonly ILocalizationService _localization;
    private readonly ILogService _log;
    private readonly ISystemParametersService _systemParameters;

    public SystemDetectionContextFactory(
        IWindowsRegistryService reg,
        ISystemRestoreService restore,
        IScheduledTaskStateService tasks,
        IPowerSettingsQueryService power,
        ILocalizationService localization,
        ILogService log,
        ISystemParametersService systemParameters)
    {
        _reg = reg;
        _restore = restore;
        _tasks = tasks;
        _power = power;
        _localization = localization;
        _log = log;
        _systemParameters = systemParameters;
    }

    public IPrefetchableDetectionContext Create() =>
        new SystemDetectionContext(_reg, _restore, _tasks, _power, _localization, _log, _systemParameters);
}
