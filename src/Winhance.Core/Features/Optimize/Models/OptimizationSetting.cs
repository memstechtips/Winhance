using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Models
{
    /// <summary>
    /// Represents a setting that optimizes the system by modifying registry values.
    /// </summary>
    public record OptimizationSetting : ApplicationSetting
    {
        /// <summary>
        /// Gets or sets the optimization category for this setting.
        /// </summary>
        public required OptimizationCategory Category { get; init; }
        
        /// <summary>
        /// Gets or sets custom properties for this setting.
        /// This can be used to store additional data specific to certain optimization types,
        /// such as PowerCfg settings.
        /// </summary>
        public Dictionary<string, object> CustomProperties { get; init; } = new Dictionary<string, object>();
    }
}
