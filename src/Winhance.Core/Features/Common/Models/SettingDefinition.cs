using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Models;

public record SettingDefinition : BaseDefinition, ISettingItem
{
    public bool RequiresConfirmation { get; init; } = false;
    public string? ActionCommand { get; init; }
    public List<(int MinBuild, int MaxBuild)> SupportedBuildRanges { get; init; } = new();
    public List<CommandSetting> CommandSettings { get; init; } = new();
    public List<PowerShellScriptSetting> PowerShellScripts { get; init; } = new();
    public List<RegContentSetting> RegContents { get; init; } = new();
    public List<PowerCfgSetting>? PowerCfgSettings { get; set; }
    public List<SettingDependency> Dependencies { get; init; } = new();
    public bool RequiresBattery { get; init; }
    public bool RequiresLid { get; init; }
    public bool RequiresDesktop { get; init; }
    public bool RequiresBrightnessSupport { get; init; }
    public bool ValidateExistence { get; init; } = true;
    public string? ParentSettingId { get; init; }
    public bool RequiresAdvancedUnlock { get; init; } = false;
}
