using System.Collections.Generic;
using Microsoft.Win32;

namespace Winhance.Core.Features.SoftwareApps.Models;

/// <summary>
/// Represents a catalog of Windows built-in applications that can be removed.
/// </summary>
public class WindowsAppCatalog
{
    /// <summary>
    /// Gets or sets the collection of removable Windows applications.
    /// </summary>
    public IReadOnlyList<AppInfo> WindowsApps { get; init; } = new List<AppInfo>();

    /// <summary>
    /// Creates a default Windows app catalog with predefined removable apps.
    /// </summary>
    /// <returns>A new WindowsAppCatalog instance with default apps.</returns>
    public static WindowsAppCatalog CreateDefault()
    {
        return new WindowsAppCatalog { WindowsApps = CreateDefaultWindowsApps() };
    }

    private static List<AppInfo> CreateDefaultWindowsApps()
    {
        return new List<AppInfo>
        {
            // 3D/Mixed Reality
            new AppInfo
            {
                Name = "3D Viewer",
                Description = "View 3D models and animations",
                PackageName = "Microsoft.Microsoft3DViewer",
                PackageID = "9NBLGGH42THS",
                Category = "3D/Mixed Reality",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Mixed Reality Portal",
                Description = "Portal for Windows Mixed Reality experiences",
                PackageName = "Microsoft.MixedReality.Portal",
                PackageID = "9NG1H8B3ZC7M",
                Category = "3D/Mixed Reality",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            // Bing/Search
            new AppInfo
            {
                Name = "Bing Search",
                Description = "Bing search integration for Windows",
                PackageName = "Microsoft.BingSearch",
                PackageID = "9NZBF4GT040C",
                Category = "Bing",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Microsoft News",
                Description = "Microsoft News app",
                PackageName = "Microsoft.BingNews",
                PackageID = "9WZDNCRFHVFW",
                Category = "Bing",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "MSN Weather",
                Description = "Weather forecasts and information",
                PackageName = "Microsoft.BingWeather",
                PackageID = "9WZDNCRFJ3Q2",
                Category = "Bing/Search",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            // Camera/Media
            new AppInfo
            {
                Name = "Camera",
                Description = "Windows Camera app",
                PackageName = "Microsoft.WindowsCamera",
                PackageID = "9WZDNCRFJBBG",
                Category = "Camera/Media",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Clipchamp",
                Description = "Video editor app",
                PackageName = "Clipchamp.Clipchamp",
                PackageID = "9P1J8S7CCWWT",
                Category = "Camera/Media",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            // System Utilities
            new AppInfo
            {
                Name = "Alarms & Clock",
                Description = "Clock, alarms, timer, and stopwatch app",
                PackageName = "Microsoft.WindowsAlarms",
                PackageID = "9WZDNCRFJ3PR",
                Category = "System Utilities",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Cortana",
                Description = "Microsoft's virtual assistant",
                PackageName = "Microsoft.549981C3F5F10",
                PackageID = "9NFFX4SZZ23L",
                Category = "System Utilities",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Get Help",
                Description = "Microsoft support app",
                PackageName = "Microsoft.GetHelp",
                PackageID = "9PKDZBMV1H3T",
                Category = "System Utilities",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Calculator",
                Description = "Calculator app with standard, scientific, and programmer modes",
                PackageName = "Microsoft.WindowsCalculator",
                PackageID = "9WZDNCRFHVN5",
                Category = "System Utilities",
                IsSystemProtected = true,
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            // Development
            new AppInfo
            {
                Name = "Dev Home",
                Description = "Development environment for Windows",
                PackageName = "Microsoft.Windows.DevHome",
                PackageID = "9WZDNCRFHVN5",
                Category = "Development",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            // Communication & Family
            new AppInfo
            {
                Name = "Microsoft Family Safety",
                Description = "Family safety and screen time management",
                PackageName = "MicrosoftCorporationII.MicrosoftFamily",
                PackageID = "9PDJDJS743XF",
                Category = "Communication",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Mail and Calendar",
                Description = "Microsoft Mail and Calendar apps",
                PackageName = "microsoft.windowscommunicationsapps",
                PackageID = "9WZDNCRFHVQM",
                Category = "Communication",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Skype",
                Description = "Video calling and messaging app",
                PackageName = "Microsoft.SkypeApp",
                PackageID = "9WZDNCRFJ364",
                Category = "Communication",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Microsoft Teams",
                Description = "Team collaboration and communication app",
                PackageName = "MSTeams",
                PackageID = "XP8BT8DW290MPQ",
                Category = "Communication",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            // System Tools
            new AppInfo
            {
                Name = "Feedback Hub",
                Description = "App for sending feedback to Microsoft",
                PackageName = "Microsoft.WindowsFeedbackHub",
                PackageID = "9NBLGGH4R32N",
                Category = "System Tools",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Maps",
                Description = "Microsoft Maps app",
                PackageName = "Microsoft.WindowsMaps",
                PackageID = "9WZDNCRDTBVB",
                Category = "System Tools",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Terminal",
                Description = "Modern terminal application for Windows",
                PackageName = "Microsoft.WindowsTerminal",
                PackageID = "9N0DX20HK701",
                Category = "System Tools",
                IsSystemProtected = true,
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            // Office & Productivity
            new AppInfo
            {
                Name = "Office Hub",
                Description = "Microsoft Office app hub",
                PackageName = "Microsoft.MicrosoftOfficeHub",
                Category = "Office",
                CanBeReinstalled = false,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "OneNote",
                Description = "Microsoft note-taking app",
                PackageName = "Microsoft.Office.OneNote",
                PackageID = "XPFFZHVGQWWLHB",
                Category = "Office",
                CanBeReinstalled = true,
                RequiresSpecialHandling = true,
                SpecialHandlerType = "OneNote",
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Outlook for Windows",
                Description = "Reimagined Outlook app for Windows",
                PackageName = "Microsoft.OutlookForWindows",
                PackageID = "9NRX63209R7B",
                Category = "Office",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            // Graphics & Images
            new AppInfo
            {
                Name = "Paint 3D",
                Description = "3D modeling and editing app",
                PackageName = "Microsoft.MSPaint",
                Category = "Graphics",
                CanBeReinstalled = false,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Paint",
                Description = "Traditional image editing app",
                PackageName = "Microsoft.Paint",
                PackageID = "9PCFS5B6T72H",
                Category = "Graphics",
                IsSystemProtected = true,
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Photos",
                Description = "Photo viewing and editing app",
                PackageName = "Microsoft.Windows.Photos",
                PackageID = "9WZDNCRFJBH4",
                Category = "Graphics",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Snipping Tool",
                Description = "Screen capture and annotation tool",
                PackageName = "Microsoft.ScreenSketch",
                PackageID = "9MZ95KL8MR0L",
                Category = "Graphics",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            // Social & People
            new AppInfo
            {
                Name = "People",
                Description = "Contact management app",
                PackageName = "Microsoft.People",
                PackageID = "9NBLGGH10PG8",
                Category = "Social",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            // Automation
            new AppInfo
            {
                Name = "Power Automate",
                Description = "Desktop automation tool",
                PackageName = "Microsoft.PowerAutomateDesktop",
                PackageID = "9NFTCH6J7FHV",
                Category = "Automation",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            // Support Tools
            new AppInfo
            {
                Name = "Quick Assist",
                Description = "Remote assistance tool",
                PackageName = "MicrosoftCorporationII.QuickAssist",
                PackageID = "9P7BP5VNWKX5",
                Category = "Support",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            // Games & Entertainment
            new AppInfo
            {
                Name = "Solitaire Collection",
                Description = "Microsoft Solitaire Collection games",
                PackageName = "Microsoft.MicrosoftSolitaireCollection",
                PackageID = "9WZDNCRFHWD2",
                Category = "Games",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Xbox",
                Description = "Xbox App for Windows",
                PackageName = "Microsoft.GamingApp", // New Xbox Package Name (Windows 11)
                PackageID = "9MV0B5HZVK9Z",
                Category = "Games",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
                SubPackages = new string[]
                {
                    "Microsoft.XboxApp", // Microsoft.XboxApp is deprecated but still on Windows 10 22H2 ISO's
                },
                RegistrySettings = new AppRegistrySetting[]
                {
                    new AppRegistrySetting
                    {
                        Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR",
                        Name = "AppCaptureEnabled",
                        Value = 0,
                        ValueKind = Microsoft.Win32.RegistryValueKind.DWord,
                        Description =
                            "Disables the Get an app to open this 'ms-gamingoverlay' popup",
                    },
                },
            },
            new AppInfo
            {
                Name = "Xbox Identity Provider",
                Description =
                    "Authentication service for Xbox Live and related Microsoft gaming services",
                PackageName = "Microsoft.XboxIdentityProvider",
                PackageID = "9WZDNCRD1HKW",
                Category = "Games",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Xbox Game Bar Plugin",
                Description =
                    "Extension component for Xbox Game Bar providing additional functionality",
                PackageName = "Microsoft.XboxGameOverlay",
                PackageID = "9NBLGGH537C2",
                Category = "Games",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Xbox Live In-Game Experience",
                Description = "Core component for Xbox Live services within games",
                PackageName = "Microsoft.Xbox.TCUI",
                PackageID = "9NKNC0LD5NN6",
                Category = "Games",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Xbox Game Bar",
                Description =
                    "Gaming overlay with screen capture, performance monitoring, and social features",
                PackageName = "Microsoft.XboxGamingOverlay",
                PackageID = "9NZKPSTSNW4P",
                Category = "Games",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            // Windows Store
            new AppInfo
            {
                Name = "Microsoft Store",
                Description = "App store for Windows",
                PackageName = "Microsoft.WindowsStore",
                PackageID = "9WZDNCRFJBMP",
                Category = "Store",
                IsSystemProtected = true,
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            // Media Players
            new AppInfo
            {
                Name = "Media Player",
                Description = "Music player app",
                PackageName = "Microsoft.ZuneMusic",
                PackageID = "9WZDNCRFJ3PT",
                Category = "Media",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Movies & TV",
                Description = "Video player app",
                PackageName = "Microsoft.ZuneVideo",
                PackageID = "9WZDNCRFJ3P2",
                Category = "Media",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Sound Recorder",
                Description = "Audio recording app",
                PackageName = "Microsoft.WindowsSoundRecorder",
                PackageID = "9WZDNCRFHWKN",
                Category = "Media",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            // Productivity Tools
            new AppInfo
            {
                Name = "Sticky Notes",
                Description = "Note-taking app",
                PackageName = "Microsoft.MicrosoftStickyNotes",
                PackageID = "9NBLGGH4QGHW",
                Category = "Productivity",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Tips",
                Description = "Windows tutorial app",
                PackageName = "Microsoft.Getstarted",
                Category = "Productivity",
                CanBeReinstalled = false,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "To Do",
                Description = "Task management app",
                PackageName = "Microsoft.Todos",
                PackageID = "9NBLGGH5R558",
                Category = "Productivity",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Notepad",
                Description = "Text editing app",
                PackageName = "Microsoft.WindowsNotepad",
                PackageID = "9MSMLRH6LZF3",
                Category = "Productivity",
                IsSystemProtected = true,
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            // Phone Integration
            new AppInfo
            {
                Name = "Phone Link",
                Description = "Connect your Android or iOS device to Windows",
                PackageName = "Microsoft.YourPhone",
                PackageID = "9NMPJ99VJBWV",
                Category = "Phone",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
            },
            // AI & Copilot
            new AppInfo
            {
                Name = "Copilot",
                Description =
                    "AI assistant for Windows, includes Copilot provider and Store components",
                PackageName = "Microsoft.Copilot",
                PackageID = "9NHT9RB2F4HD",
                Category = "AI",
                CanBeReinstalled = true,
                Type = AppType.StandardApp,
                SubPackages = new string[]
                {
                    "Microsoft.Windows.Ai.Copilot.Provider",
                    "Microsoft.Copilot_8wekyb3d8bbwe",
                },
                RegistrySettings = new AppRegistrySetting[]
                {
                    new AppRegistrySetting
                    {
                        Path = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                        Name = "ShowCopilotButton",
                        Value = 0,
                        ValueKind = Microsoft.Win32.RegistryValueKind.DWord,
                        Description = "Hide Copilot button from taskbar",
                    },
                    new AppRegistrySetting
                    {
                        Path = @"HKLM\Software\Policies\Microsoft\Windows\CloudContent",
                        Name = "TurnOffWindowsCopilot",
                        Value = 1,
                        ValueKind = Microsoft.Win32.RegistryValueKind.DWord,
                        Description = "Disable Windows Copilot system-wide",
                    },
                },
            },
            // Special Items that require special handling
            new AppInfo
            {
                Name = "Microsoft Edge",
                Description = "Microsoft's web browser (requires special removal process)",
                PackageName = "Edge",
                PackageID = "XPFFTQ037JWMHS",
                Category = "Browsers",
                CanBeReinstalled = true,
                RequiresSpecialHandling = true,
                SpecialHandlerType = "Edge",
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "OneDrive",
                Description =
                    "Microsoft's cloud storage service (requires special removal process)",
                PackageName = "OneDrive",
                PackageID = "Microsoft.OneDrive",
                Category = "System",
                CanBeReinstalled = true,
                RequiresSpecialHandling = true,
                SpecialHandlerType = "OneDrive",
                Type = AppType.StandardApp,
            },
        };
    }
}
