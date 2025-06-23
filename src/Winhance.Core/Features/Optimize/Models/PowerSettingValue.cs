using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Winhance.Core.Features.Optimize.Models
{
    /// <summary>
    /// Represents a power setting value that can be applied to a power plan.
    /// </summary>
    public class PowerSettingApplyValue
    {
        /// <summary>
        /// Gets or sets the power setting GUID.
        /// </summary>
        public string SettingGuid { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the power setting subgroup GUID.
        /// </summary>
        public string SubgroupGuid { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the power plan GUID to apply the setting to.
        /// </summary>
        public string PowerPlanGuid { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the value to apply for AC power.
        /// </summary>
        public int AcValue { get; set; }

        /// <summary>
        /// Gets or sets the value to apply for DC power (battery).
        /// </summary>
        public int DcValue { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether to apply the AC value.
        /// </summary>
        public bool ApplyAcValue { get; set; } = true;

        /// <summary>
        /// Gets or sets a flag indicating whether to apply the DC value.
        /// </summary>
        public bool ApplyDcValue { get; set; } = true;

        /// <summary>
        /// Gets or sets the display name of the setting.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the alias of the setting.
        /// </summary>
        public string Alias { get; set; } = string.Empty;

        /// <summary>
        /// Gets the powercfg command to apply the AC value.
        /// </summary>
        public string GetAcCommand()
        {
            return $"powercfg /setacvalueindex {PowerPlanGuid} {SubgroupGuid} {SettingGuid} {AcValue}";
        }

        /// <summary>
        /// Gets the powercfg command to apply the DC value.
        /// </summary>
        public string GetDcCommand()
        {
            return $"powercfg /setdcvalueindex {PowerPlanGuid} {SubgroupGuid} {SettingGuid} {DcValue}";
        }
    }
}
