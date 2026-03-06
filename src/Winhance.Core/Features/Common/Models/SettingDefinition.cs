using System;
using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Models;

public record SettingDefinition : BaseDefinition, ISettingItem
{
    public bool RequiresConfirmation { get; init; } = false;
    public string? ActionCommand { get; init; }
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
}
