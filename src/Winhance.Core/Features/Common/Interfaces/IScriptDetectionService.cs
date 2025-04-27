using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Service for detecting the presence of script files used by Winhance to remove applications.
    /// </summary>
    public interface IScriptDetectionService
    {
        /// <summary>
        /// Checks if any removal scripts are present.
        /// </summary>
        /// <returns>True if any removal scripts are present, false otherwise.</returns>
        bool AreRemovalScriptsPresent();
        
        /// <summary>
        /// Gets information about all active removal scripts.
        /// </summary>
        /// <returns>A collection of script information objects.</returns>
        IEnumerable<ScriptInfo> GetActiveScripts();
    }
    
    /// <summary>
    /// Represents information about a script file.
    /// </summary>
    public class ScriptInfo
    {
        /// <summary>
        /// Gets or sets the name of the script file.
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// Gets or sets the description of what the script does.
        /// </summary>
        public string Description { get; set; }
        
        /// <summary>
        /// Gets or sets the full path to the script file.
        /// </summary>
        public string FilePath { get; set; }
    }
}
