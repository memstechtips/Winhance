using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models;

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
                    // Installed -> iexplore.exe; not-installed fallback -> shell32.dll resource
                    // 512 (shell32 is always present; ieframe.dll,#190 leaves with the capability).
                    IconSources =
                    [
                        @"%ProgramFiles%\Internet Explorer\iexplore.exe",
                        @"%SystemRoot%\System32\shell32.dll,#512",
                    ],
                    CanBeReinstalled = false
                },
                new ItemDefinition
                {
                    Id = "capability-powershell-ise",
                    Name = "PowerShell ISE",
                    Description = "Legacy script editor for Windows PowerShell with debugger and IntelliSense",
                    GroupName = "Development",
                    CapabilityName = "Microsoft.Windows.PowerShell.ISE",
                    IconSources =
                    [
                        @"%SystemRoot%\System32\WindowsPowerShell\v1.0\PowerShell_ISE.exe",
                        @"%SystemRoot%\System32\scrptadm.dll,#7",
                    ],
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "capability-quick-assist",
                    Name = "Quick Assist (Legacy)",
                    Description = "Older Quick Assist remote help app, replaced by the Microsoft Store version",
                    GroupName = "System",
                    CapabilityName = "App.Support.QuickAssist",
                    IconSources =
                    [
                        @"%SystemRoot%\System32\quickassist.exe",
                        // TODO(base64): not-installed fallback — encode PNG from images/ (held per Marco's request)
                    ],
                    CanBeReinstalled = false
                },
                new ItemDefinition
                {
                    Id = "capability-steps-recorder",
                    Name = "Steps Recorder",
                    Description = "Captures screenshots of each click for documenting steps in a problem report",
                    GroupName = "Utilities",
                    CapabilityName = "App.StepsRecorder",
                    IconSources =
                    [
                        @"%SystemRoot%\System32\psr.exe",
                        // TODO(base64): not-installed fallback — encode PNG from images/ (held per Marco's request)
                    ],
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "capability-windows-media-player",
                    Name = "Windows Media Player",
                    Description = "Classic media player for music, video, CDs, and DVDs",
                    GroupName = "Media",
                    CapabilityName = "Media.WindowsMediaPlayer",
                    IconSources =
                    [
                        @"%ProgramFiles%\Windows Media Player\wmplayer.exe",
                        @"%SystemRoot%\System32\wmploc.dll,#102",
                    ],
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "capability-wordpad",
                    Name = "WordPad",
                    Description = "Rich text editor for RTF and DOC files; removed by default in Windows 11 24H2",
                    GroupName = "Productivity",
                    CapabilityName = "Microsoft.Windows.WordPad",
                    IconSources =
                    [
                        @"%ProgramFiles%\Windows NT\Accessories\wordpad.exe",
                        "https://upload.wikimedia.org/wikipedia/en/0/01/Microsoft_Wordpad_logo.png",
                    ],
                    CanBeReinstalled = false
                },
                new ItemDefinition
                {
                    Id = "capability-notepad",
                    Name = "Notepad (Legacy)",
                    Description = "Original Notepad without tabs, AI rewrite, or autosave",
                    GroupName = "Productivity",
                    CapabilityName = "Microsoft.Windows.Notepad",
                    // Legacy Notepad installs the Win32 binary at System32\notepad.exe (the Store
                    // version is appx), so exe-first yields the legacy icon when installed.
                    IconSources =
                    [
                        @"%SystemRoot%\System32\notepad.exe",
                        // TODO(base64): not-installed fallback — encode notepad PNG from images/ (held per Marco's request)
                    ],
                    CanBeReinstalled = false
                },
                new ItemDefinition
                {
                    Id = "capability-paint-legacy",
                    Name = "Paint (Legacy)",
                    Description = "Original Win32 Paint binary, kept for users who prefer the old interface",
                    GroupName = "Graphics",
                    CapabilityName = "Microsoft.Windows.MSPaint",
                    // Legacy Paint installs the Win32 binary at System32\mspaint.exe (the Store
                    // version is appx), so exe-first yields the legacy icon when installed.
                    IconSources =
                    [
                        @"%SystemRoot%\System32\mspaint.exe",
                        // TODO(base64): not-installed fallback — encode PNG from images/ (held per Marco's request)
                    ],
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
