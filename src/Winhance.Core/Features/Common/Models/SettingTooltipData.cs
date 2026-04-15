
namespace Winhance.Core.Features.Common.Models;

public sealed record SettingTooltipData
{
    public string SettingId { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string DisplayValue { get; init; } = string.Empty;
    public IReadOnlyDictionary<RegistrySetting, string?> IndividualRegistryValues { get; init; } = new Dictionary<RegistrySetting, string?>();
    public IReadOnlyList<ScheduledTaskSetting> ScheduledTaskSettings { get; init; } = new List<ScheduledTaskSetting>();
    public IReadOnlyList<PowerCfgSetting> PowerCfgSettings { get; init; } = new List<PowerCfgSetting>();
    public IReadOnlyList<PowerShellScriptSetting> PowerShellScripts { get; init; } = new List<PowerShellScriptSetting>();
    public IReadOnlyList<RegContentSetting> RegContents { get; init; } = new List<RegContentSetting>();
    public IReadOnlyList<SettingDependency> Dependencies { get; init; } = new List<SettingDependency>();
    public IReadOnlyDictionary<PowerCfgSetting, (int? AC, int? DC)> CurrentPowerValues { get; init; } = new Dictionary<PowerCfgSetting, (int? AC, int? DC)>();

    /// <summary>
    /// Optional reference back to the originating SettingDefinition. Needed by the Technical Details
    /// panel to resolve Selection-type Recommended/Default column values from
    /// <c>ComboBox.Options[i].ValueMappings</c> (since per-entry RecommendedValue/DefaultValue are
    /// null for Selection settings in the new state model).
    /// </summary>
    public SettingDefinition? SettingDefinition { get; init; }
}
