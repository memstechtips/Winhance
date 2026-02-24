
namespace Winhance.Core.Features.Common.Models
{

    public record SettingTooltipData
    {
        public string SettingId { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string DisplayValue { get; init; } = string.Empty;
        public IReadOnlyDictionary<RegistrySetting, string?> IndividualRegistryValues { get; init; } = new Dictionary<RegistrySetting, string?>();
        public IReadOnlyList<ScheduledTaskSetting> ScheduledTaskSettings { get; init; } = new List<ScheduledTaskSetting>();
        public IReadOnlyList<PowerCfgSetting> PowerCfgSettings { get; init; } = new List<PowerCfgSetting>();
    }
}
