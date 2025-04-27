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
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the GUID of the power plan.
        /// </summary>
        public string Guid { get; set; }

        /// <summary>
        /// Gets or sets the source GUID for plans that need to be created from another plan.
        /// </summary>
        public string SourceGuid { get; set; }

        /// <summary>
        /// Gets or sets the description of the power plan.
        /// </summary>
        public string Description { get; set; }
    }
}