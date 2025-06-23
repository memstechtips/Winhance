using System;

namespace Winhance.Core.Features.Optimize.Models
{
    /// <summary>
    /// Represents a Windows power plan.
    /// </summary>
    public class PowerPlan
    {
        /// <summary>
        /// Gets or sets the name of the power plan.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the GUID of the power plan.
        /// </summary>
        public string Guid { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the source GUID for plans that need to be created from another plan.
        /// </summary>
        public string SourceGuid { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the power plan.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this power plan is currently active.
        /// </summary>
        public bool IsActive { get; set; }
    }
}