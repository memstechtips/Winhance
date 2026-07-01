using System.Collections.Generic;
using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IDependencyManager
{
    Task<bool> HandleSettingEnabledAsync(string settingId, IEnumerable<ISettingItem> allSettings, ISettingApplicationService settingApplicationService, ICatalogSettingStateProvider stateProvider);
    Task HandleSettingDisabledAsync(string settingId, IEnumerable<ISettingItem> allSettings, ISettingApplicationService settingApplicationService, ICatalogSettingStateProvider stateProvider);
    Task HandleSettingValueChangedAsync(string settingId, IEnumerable<ISettingItem> allSettings, ISettingApplicationService settingApplicationService, ICatalogSettingStateProvider stateProvider);
}
