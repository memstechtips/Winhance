using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IRecommendedSettingsApplier
{
    /// <summary>
    /// Apply recommended values for an explicit, already OS-filtered set. Suppresses per-setting
    /// restarts internally and returns the settings actually applied. DOES NOT flush restarts —
    /// the caller flushes once via IProcessRestartManager.FlushCoalescedRestartsAsync.
    /// </summary>
    Task<IReadOnlyList<Setting>> ApplyRecommendedToSettingsAsync(
        IReadOnlyList<Setting> settings,
        ISettingApplicationService apply,
        IProgress<TaskProgressDetail>? progress = null);

    /// <summary>
    /// Resolve a feature's settings (excluding the trigger), apply recommended, return applied.
    /// DOES NOT flush — caller flushes.
    /// </summary>
    Task<IReadOnlyList<Setting>> ApplyRecommendedForFeatureAsync(
        string triggerSettingId,
        ISettingApplicationService apply);

    /// <summary>
    /// Resolve + apply recommended for a feature AND flush one coalesced restart. Standalone callers.
    /// </summary>
    Task ApplyRecommendedSettingsForFeatureAsync(string settingId, ISettingApplicationService apply);
}
