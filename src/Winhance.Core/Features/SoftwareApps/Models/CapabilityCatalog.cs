using System.Collections.Generic;

namespace Winhance.Core.Features.SoftwareApps.Models;

/// <summary>
/// Represents a catalog of Windows capabilities that can be enabled or disabled.
/// </summary>
public class CapabilityCatalog
{
    /// <summary>
    /// Gets or sets the collection of Windows capabilities.
    /// </summary>
    public IReadOnlyList<CapabilityInfo> Capabilities { get; init; } = new List<CapabilityInfo>();

    /// <summary>
    /// Creates a default capability catalog with predefined Windows capabilities.
    /// </summary>
    /// <returns>A new CapabilityCatalog instance with default capabilities.</returns>
    public static CapabilityCatalog CreateDefault()
    {
        return new CapabilityCatalog { Capabilities = CreateDefaultCapabilities() };
    }

    private static IReadOnlyList<CapabilityInfo> CreateDefaultCapabilities()
    {
        return new List<CapabilityInfo>
        {
            // Browser capabilities
            new CapabilityInfo
            {
                Name = "Internet Explorer",
                Description = "Legacy web browser",
                PackageName = "Browser.InternetExplorer",
                Category = "Browser",
                CanBeReenabled = false,
            },
            // Development capabilities
            new CapabilityInfo
            {
                Name = "PowerShell ISE",
                Description = "PowerShell Integrated Scripting Environment",
                PackageName = "Microsoft.Windows.PowerShell.ISE",
                Category = "Development",
                CanBeReenabled = true,
            },
            // System capabilities
            new CapabilityInfo
            {
                Name = "Quick Assist",
                Description = "Remote assistance app",
                PackageName = "App.Support.QuickAssist",
                Category = "System",
                CanBeReenabled = false,
            },
            // Utilities capabilities
            new CapabilityInfo
            {
                Name = "Steps Recorder",
                Description = "Screen recording tool",
                PackageName = "App.StepsRecorder",
                Category = "Utilities",
                CanBeReenabled = true,
            },
            // Media capabilities
            new CapabilityInfo
            {
                Name = "Windows Media Player",
                Description = "Classic media player",
                PackageName = "Media.WindowsMediaPlayer",
                Category = "Media",
                CanBeReenabled = true,
            },
            // Productivity capabilities
            new CapabilityInfo
            {
                Name = "WordPad",
                Description = "Rich text editor",
                PackageName = "Microsoft.Windows.WordPad",
                Category = "Productivity",
                CanBeReenabled = false,
            },
            new CapabilityInfo
            {
                Name = "Paint (Legacy)",
                Description = "Classic Paint app",
                PackageName = "Microsoft.Windows.MSPaint",
                Category = "Graphics",
                CanBeReenabled = false,
            },
            // OpenSSH capabilities
            new CapabilityInfo
            {
                Name = "OpenSSH Client",
                Description = "Secure Shell client for remote connections",
                PackageName = "OpenSSH.Client",
                Category = "Networking",
                IsSystemProtected = false,
                CanBeReenabled = true,
            },
            new CapabilityInfo
            {
                Name = "OpenSSH Server",
                Description = "Secure Shell server for remote connections",
                PackageName = "OpenSSH.Server",
                Category = "Networking",
                IsSystemProtected = false,
                CanBeReenabled = true,
            },
        };
    }
}
