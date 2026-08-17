using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Models;

/// <summary>
/// Groups the pass-through dependencies that SettingViewModelFactory
/// forwards unchanged to SettingItemViewModel constructors.
/// </summary>
public record SettingViewModelDependencies(
    ISettingWriteStrategySelector WriteStrategySelector,
    ILogService LogService,
    IDispatcherService DispatcherService,
    IDialogService DialogService,
    IRegeditLauncher RegeditLauncher,
    IApplicationModeService ApplicationModeService
);
