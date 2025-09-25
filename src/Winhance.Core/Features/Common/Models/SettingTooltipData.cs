
namespace Winhance.Core.Features.Common.Models
{

    public class SettingTooltipData
    {

        public string SettingId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RegistrySetting? RegistrySetting { get; set; }
        public string DisplayValue { get; set; } = string.Empty;
        public Dictionary<RegistrySetting, object?> IndividualRegistryValues { get; set; } = new Dictionary<RegistrySetting, object?>();
        public List<CommandSetting> CommandSettings { get; set; } = new List<CommandSetting>();
        public List<PowerCfgSetting> PowerCfgSettings { get; set; } = new List<PowerCfgSetting>();
    }
}
