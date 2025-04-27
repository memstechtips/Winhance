using System.Collections.Generic;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Enums;

namespace Winhance.Core.Features.Customize.Models
{
    /// <summary>
    /// Represents a setting that customizes the system by modifying registry values.
    /// </summary>
    public record CustomizationSetting : ApplicationSetting
    {
        /// <summary>
        /// Gets or sets the customization category for this setting.
        /// </summary>
        public required CustomizationCategory Category { get; init; }

        /// <summary>
        /// Gets or sets the linked settings for this setting.
        /// This allows grouping multiple settings together under a parent setting.
        /// </summary>
        public List<CustomizationSetting> LinkedSettings { get; init; } = new List<CustomizationSetting>();
    }
}
