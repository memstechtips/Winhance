using System;
using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Models;

public sealed record SettingDefinition : BaseDefinition, ISettingItem
{
    public bool RequiresConfirmation { get; init; } = false;
    public IReadOnlyList<(int MinBuild, int MaxBuild)> SupportedBuildRanges { get; init; } = Array.Empty<(int MinBuild, int MaxBuild)>();
    public IReadOnlyList<ScheduledTaskSetting> ScheduledTaskSettings { get; init; } = Array.Empty<ScheduledTaskSetting>();
    public IReadOnlyList<PowerShellScriptSetting> PowerShellScripts { get; init; } = Array.Empty<PowerShellScriptSetting>();
    public IReadOnlyList<RegContentSetting> RegContents { get; init; } = Array.Empty<RegContentSetting>();
    public IReadOnlyList<PowerCfgSetting>? PowerCfgSettings { get; init; }
    public IReadOnlyList<NativePowerApiSetting> NativePowerApiSettings { get; init; } = Array.Empty<NativePowerApiSetting>();
    public IReadOnlyList<SettingDependency> Dependencies { get; init; } = Array.Empty<SettingDependency>();
    public IReadOnlyList<string>? AutoEnableSettingIds { get; init; }
    public bool RequiresBattery { get; init; }
    public bool RequiresLid { get; init; }
    public bool RequiresDesktop { get; init; }
    public bool RequiresBrightnessSupport { get; init; }
    public bool RequiresHybridSleepCapable { get; init; }
    public bool ValidateExistence { get; init; } = true;
    public string? ParentSettingId { get; init; }
    public bool RequiresAdvancedUnlock { get; init; } = false;
    /// <summary>
    /// True when this Selection setting has no objectively-better choice —
    /// the correct answer is user-, region-, or preference-driven. Badge
    /// computation for subjective settings yields <see cref="SettingBadgeKind.Preference"/>
    /// for any well-known option value, ignoring <see cref="ComboBoxOption.IsRecommended"/>
    /// and <see cref="ComboBoxOption.IsDefault"/> for the pill label.
    /// Authors may still mark a single option as Recommended/Default so Quick Actions
    /// has a target to apply; SettingCatalogValidator enforces at most one of each.
    /// </summary>
    public bool IsSubjectivePreference { get; init; } = false;

    /// <summary>
    /// For Toggle/CheckBox settings: explicit Recommended state (true = enabled,
    /// false = disabled). Mirrors the pattern of <see cref="ComboBoxOption.IsRecommended"/>
    /// — the recommendation lives on the SettingDefinition rather than per-RegistrySetting.
    ///
    /// Set this when the recommendation cannot be encoded as a non-null
    /// <c>RegistrySetting.RecommendedValue</c> — typically because the recommended state
    /// is "key absent" (e.g. EnabledValue = [null]). When set, this wins over per-key
    /// RecommendedValue derivation for badge / quick-set logic.
    ///
    /// Null means "no explicit toggle-level recommendation; fall back to per-key
    /// RecommendedValue, or no Recommended badge if those are also null".
    /// </summary>
    public bool? RecommendedToggleState { get; init; }

    /// <summary>
    /// For Toggle/CheckBox settings: explicit Windows default state (true = enabled,
    /// false = disabled). Parallel to <see cref="RecommendedToggleState"/>.
    ///
    /// Set this when the Windows default cannot be encoded as a non-null
    /// <c>RegistrySetting.DefaultValue</c> — typically for settings whose detection
    /// path is native (e.g. <c>DetectionType.SystemRestore</c>) and therefore has no
    /// RegistrySetting to carry the default value. When set, this wins over per-key
    /// DefaultValue derivation for badge / quick-set logic.
    ///
    /// Null means "no explicit toggle-level default; fall back to per-key
    /// DefaultValue, or no Default badge if those are also null".
    /// </summary>
    public bool? DefaultToggleState { get; init; }

    /// <summary>
    /// For Selection settings: when no <see cref="ComboBoxOption"/> matches the live registry
    /// state, resolve to the <see cref="ComboBoxOption.IsDefault"/> option instead of "Custom".
    ///
    /// Set this only when the setting's Windows-default state cannot be expressed as a single
    /// enumerable option value — e.g. a REG_BINARY blob whose default content varies between
    /// installs (the shortcut-suffix "link" value), or a bitfield where several distinct raw
    /// values all mean the default option (Win32PrioritySeparation: both 2 and 0x26 are
    /// "Programs"). The non-default option(s) must still map exact values, so a recognised
    /// non-default state resolves correctly; only genuinely unrecognised states fall back.
    ///
    /// Default false: an unmatched value stays "Custom" (strict detection). This is a superset
    /// of the all-backing-values-absent fallback, which still applies regardless of this flag.
    /// </summary>
    public bool ResolveUnmatchedToDefault { get; init; } = false;
}
