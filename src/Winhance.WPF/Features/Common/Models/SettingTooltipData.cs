using System.Collections.Generic;
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Models
{
    /// <summary>
    /// UI-specific data structure for tooltip display.
    /// Contains individual registry values that are already retrieved during system state discovery.
    /// This keeps UI concerns separate from domain models while reusing existing registry calls.
    /// </summary>
    public class SettingTooltipData
    {
        /// <summary>
        /// Gets or sets the setting ID this tooltip data belongs to.
        /// </summary>
        public string SettingId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the individual current values for each registry setting.
        /// This dictionary maps each RegistrySetting to its current system value.
        /// Used for tooltip display to show individual registry values.
        /// </summary>
        public Dictionary<RegistrySetting, object?> IndividualRegistryValues { get; set; } = new Dictionary<RegistrySetting, object?>();

        /// <summary>
        /// Gets or sets the command settings for this setting.
        /// This is a copy of the domain model's command settings for tooltip display.
        /// </summary>
        public List<CommandSetting> CommandSettings { get; set; } = new List<CommandSetting>();
    }
}
