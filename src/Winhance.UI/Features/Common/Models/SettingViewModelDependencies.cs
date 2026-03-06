using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Models;

/// <summary>
/// Groups the 6 pass-through dependencies that SettingViewModelFactory
/// forwards unchanged to SettingItemViewModel constructors.
/// </summary>
public record SettingViewModelDependencies(
    ISettingApplicationService SettingApplicationService,
    ILogService LogService,
    IDispatcherService DispatcherService,
    IDialogService DialogService,
    IEventBus EventBus,
    IRegeditLauncher RegeditLauncher
);
