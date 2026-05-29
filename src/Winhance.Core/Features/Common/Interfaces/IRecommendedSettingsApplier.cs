using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IRecommendedSettingsApplier
{
    /// Apply recommended values for an explicit, already OS-filtered set. Suppresses per-setting
    /// restarts internally and returns the settings actually applied. DOES NOT flush restarts —
    /// the caller flushes once via IProcessRestartManager.FlushCoalescedRestartsAsync.
    Task<IReadOnlyList<SettingDefinition>> ApplyRecommendedToSettingsAsync(
        IReadOnlyList<SettingDefinition> settings,
        ISettingApplicationService apply,
        IProgress<TaskProgressDetail>? progress = null);

    /// Resolve a feature's settings (excluding the trigger), apply recommended, return applied.
    /// DOES NOT flush — caller flushes.
    Task<IReadOnlyList<SettingDefinition>> ApplyRecommendedForFeatureAsync(
        string triggerSettingId,
        ISettingApplicationService apply);

    /// Resolve + apply recommended for a feature AND flush one coalesced restart. Standalone callers.
    Task ApplyRecommendedSettingsForFeatureAsync(string settingId, ISettingApplicationService apply);
}
