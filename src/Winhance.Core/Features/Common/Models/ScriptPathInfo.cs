using System;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Immutable model containing script path detection information.
    /// </summary>
    public sealed class ScriptPathInfo
    {
        /// <summary>
        /// Gets the scripts directory path.
        /// </summary>
        public string ScriptsDirectory { get; }

        /// <summary>
        /// Gets the application directory path.
        /// </summary>
        public string ApplicationDirectory { get; }

        /// <summary>
        /// Gets whether the application is running from external media.
        /// </summary>
        public bool IsRunningFromExternalMedia { get; }

        /// <summary>
        /// Gets the fallback scripts directory path.
        /// </summary>
        public string FallbackScriptsPath { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScriptPathInfo"/> class.
        /// </summary>
        /// <param name="scriptsDirectory">The scripts directory path.</param>
        /// <param name="applicationDirectory">The application directory path.</param>
        /// <param name="isRunningFromExternalMedia">Whether running from external media.</param>
        /// <param name="fallbackScriptsPath">The fallback scripts directory path.</param>
        public ScriptPathInfo(string scriptsDirectory, string applicationDirectory, bool isRunningFromExternalMedia, string fallbackScriptsPath)
        {
            ScriptsDirectory = scriptsDirectory ?? throw new ArgumentNullException(nameof(scriptsDirectory));
            ApplicationDirectory = applicationDirectory ?? throw new ArgumentNullException(nameof(applicationDirectory));
            IsRunningFromExternalMedia = isRunningFromExternalMedia;
            FallbackScriptsPath = fallbackScriptsPath ?? throw new ArgumentNullException(nameof(fallbackScriptsPath));
        }
    }
}
