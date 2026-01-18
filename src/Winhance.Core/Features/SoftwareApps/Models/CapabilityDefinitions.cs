using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models
{
    public static class CapabilityDefinitions
    {
        public static ItemGroup GetWindowsCapabilities()
        {
            return new ItemGroup
            {
                Name = "Windows Capabilities",
                FeatureId = FeatureIds.WindowsCapabilities,
                Items = new List<ItemDefinition>
                {
                    new ItemDefinition
                    {
                        Id = "capability-internet-explorer",
                        Name = "Internet Explorer",
                        Description = "Legacy web browser",
                        GroupName = "Browser",
                        CapabilityName = "Browser.InternetExplorer",
                        CanBeReinstalled = false
                    },
                    new ItemDefinition
                    {
                        Id = "capability-powershell-ise",
                        Name = "PowerShell ISE",
                        Description = "PowerShell Integrated Scripting Environment",
                        GroupName = "Development",
                        CapabilityName = "Microsoft.Windows.PowerShell.ISE",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "capability-quick-assist",
                        Name = "Quick Assist",
                        Description = "Remote assistance app",
                        GroupName = "System",
                        CapabilityName = "App.Support.QuickAssist",
                        CanBeReinstalled = false
                    },
                    new ItemDefinition
                    {
                        Id = "capability-steps-recorder",
                        Name = "Steps Recorder",
                        Description = "Screen recording tool",
                        GroupName = "Utilities",
                        CapabilityName = "App.StepsRecorder",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "capability-windows-media-player",
                        Name = "Windows Media Player",
                        Description = "Classic media player",
                        GroupName = "Media",
                        CapabilityName = "Media.WindowsMediaPlayer",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "capability-wordpad",
                        Name = "WordPad",
                        Description = "Rich text editor",
                        GroupName = "Productivity",
                        CapabilityName = "Microsoft.Windows.WordPad",
                        CanBeReinstalled = false
                    },
                    new ItemDefinition
                    {
                        Id = "capability-paint-legacy",
                        Name = "Paint (Legacy)",
                        Description = "Classic Paint app",
                        GroupName = "Graphics",
                        CapabilityName = "Microsoft.Windows.MSPaint",
                        CanBeReinstalled = false
                    },
                    new ItemDefinition
                    {
                        Id = "capability-openssh-client",
                        Name = "OpenSSH Client",
                        Description = "Secure Shell client for remote connections",
                        GroupName = "Networking",
                        CapabilityName = "OpenSSH.Client",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "capability-openssh-server",
                        Name = "OpenSSH Server",
                        Description = "Secure Shell server for remote connections",
                        GroupName = "Networking",
                        CapabilityName = "OpenSSH.Server",
                        CanBeReinstalled = true
                    }
                }
            };
        }
    }
}