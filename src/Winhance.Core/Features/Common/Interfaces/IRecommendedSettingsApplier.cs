using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IRecommendedSettingsApplier
{
    // Suppresses per-setting restarts internally; does NOT flush - the caller flushes once via FlushCoalescedRestartsAsync.
    Task<IReadOnlyList<Setting>> ApplyRecommendedToSettingsAsync(
        IReadOnlyList<Setting> settings,
        ISettingApplicationService apply,
        IProgress<TaskProgressDetail>? progress = null);

    // Excludes the trigger; does NOT flush.
    Task<IReadOnlyList<Setting>> ApplyRecommendedForFeatureAsync(
        string triggerSettingId,
        ISettingApplicationService apply);

    // Also flushes one coalesced restart; for standalone callers.
    Task ApplyRecommendedSettingsForFeatureAsync(string settingId, ISettingApplicationService apply);
}
