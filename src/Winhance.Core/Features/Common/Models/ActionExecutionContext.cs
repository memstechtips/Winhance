using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Context object that contains all the parameters needed for executing ActionCommands.
    /// Supports additional parameters for future extensibility while maintaining clean architecture.
    /// </summary>
    public class ActionExecutionContext
    {
        /// <summary>
        /// The ID of the setting that triggered the action.
        /// </summary>
        public string SettingId { get; set; } = string.Empty;

        /// <summary>
        /// The command string (method name) to execute.
        /// </summary>
        public string CommandString { get; set; } = string.Empty;

        /// <summary>
        /// Whether to apply recommended settings after executing the main action.
        /// </summary>
        public bool ApplyRecommendedSettings { get; set; }

        /// <summary>
        /// Additional parameters that can be used for future extensibility.
        /// </summary>
        public Dictionary<string, object> AdditionalParameters { get; set; } = new();
    }
}
