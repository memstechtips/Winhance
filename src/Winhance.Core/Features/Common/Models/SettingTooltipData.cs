
namespace Winhance.Core.Features.Common.Models
{

    public class SettingTooltipData
    {
        public string SettingId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DisplayValue { get; set; } = string.Empty;
        public IReadOnlyDictionary<RegistrySetting, string?> IndividualRegistryValues { get; set; } = new Dictionary<RegistrySetting, string?>();
        public IReadOnlyList<ScheduledTaskSetting> ScheduledTaskSettings { get; set; } = new List<ScheduledTaskSetting>();
        public IReadOnlyList<PowerCfgSetting> PowerCfgSettings { get; set; } = new List<PowerCfgSetting>();
    }
}
