using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Models;

public record SettingViewModelDependencies(
    ISettingWriteStrategySelector WriteStrategySelector,
    ILogService LogService,
    IDispatcherService DispatcherService,
    IDialogService DialogService,
    IRegeditLauncher RegeditLauncher,
    IApplicationModeService ApplicationModeService
);
