using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IBulkSettingsActionService
{
    Task<int> ApplyRecommendedAsync(IEnumerable<string> settingIds, IProgress<TaskProgressDetail>? progress = null);
    Task<int> ResetToDefaultsAsync(IEnumerable<string> settingIds, IProgress<TaskProgressDetail>? progress = null);
    Task<int> GetAffectedCountAsync(IEnumerable<string> settingIds, BulkActionType actionType);
}
