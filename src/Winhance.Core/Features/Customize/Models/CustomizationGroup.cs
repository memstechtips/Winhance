using System.Collections.Generic;
using Winhance.Core.Features.Customize.Enums;

namespace Winhance.Core.Features.Customize.Models
{
    /// <summary>
    /// Represents a group of customization settings.
    /// </summary>
    public record CustomizationGroup
    {
        /// <summary>
        /// Gets or sets the name of the customization group.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets or sets the category of the customization group.
        /// </summary>
        public required CustomizationCategory Category { get; init; }

        /// <summary>
        /// Gets or sets the settings in the customization group.
        /// </summary>
        public required IReadOnlyList<CustomizationSetting> Settings { get; init; }
    }
}
