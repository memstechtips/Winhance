using System;

namespace Winhance.Core.Features.Common.Models
{
    /// <summary>
    /// Immutable model containing PowerShell detection information.
    /// </summary>
    public sealed class PowerShellInfo
    {
        /// <summary>
        /// Gets whether Windows PowerShell 5.1 should be used.
        /// </summary>
        public bool UseWindowsPowerShell { get; }

        /// <summary>
        /// Gets the path to the PowerShell executable.
        /// </summary>
        public string PowerShellPath { get; }

        /// <summary>
        /// Gets the detected PowerShell version string.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Gets the operating system information.
        /// </summary>
        public string OSVersion { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerShellInfo"/> class.
        /// </summary>
        /// <param name="useWindowsPowerShell">Whether to use Windows PowerShell 5.1.</param>
        /// <param name="powerShellPath">The path to the PowerShell executable.</param>
        /// <param name="version">The PowerShell version string.</param>
        /// <param name="osVersion">The operating system version information.</param>
        public PowerShellInfo(bool useWindowsPowerShell, string powerShellPath, string version, string osVersion)
        {
            UseWindowsPowerShell = useWindowsPowerShell;
            PowerShellPath = powerShellPath ?? throw new ArgumentNullException(nameof(powerShellPath));
            Version = version ?? throw new ArgumentNullException(nameof(version));
            OSVersion = osVersion ?? throw new ArgumentNullException(nameof(osVersion));
        }
    }
}
