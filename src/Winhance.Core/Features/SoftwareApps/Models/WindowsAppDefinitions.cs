using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Models
{
    public static class WindowsAppDefinitions
    {
        public static ItemGroup GetWindowsApps()
        {
            return new ItemGroup
            {
                Name = "Windows Apps",
                FeatureId = FeatureIds.WindowsApps,
                Items = new List<ItemDefinition>
                {
                    // 3D/Mixed Reality
                    new ItemDefinition
                    {
                        Id = "windows-app-3d-viewer",
                        Name = "3D Viewer",
                        Description = "View 3D models and animations",
                        GroupName = "3D/Mixed Reality",
                        AppxPackageName = "Microsoft.Microsoft3DViewer",
                        WinGetPackageId = "9NBLGGH42THS",
                        Category = "3D/Mixed Reality",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-mixed-reality-portal",
                        Name = "Mixed Reality Portal",
                        Description = "Portal for Windows Mixed Reality experiences",
                        GroupName = "3D/Mixed Reality",
                        AppxPackageName = "Microsoft.MixedReality.Portal",
                        WinGetPackageId = "9NG1H8B3ZC7M",
                        Category = "3D/Mixed Reality",
                        CanBeReinstalled = true
                    },

                    // Bing/Search
                    new ItemDefinition
                    {
                        Id = "windows-app-bing-search",
                        Name = "Bing Search",
                        Description = "Bing search integration for Windows",
                        GroupName = "Bing/Search",
                        AppxPackageName = "Microsoft.BingSearch",
                        WinGetPackageId = "9NZBF4GT040C",
                        Category = "Bing",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-microsoft-news",
                        Name = "Microsoft News",
                        Description = "Microsoft News app",
                        GroupName = "Bing/Search",
                        AppxPackageName = "Microsoft.BingNews",
                        WinGetPackageId = "9WZDNCRFHVFW",
                        Category = "Bing",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-msn-weather",
                        Name = "MSN Weather",
                        Description = "Weather forecasts and information",
                        GroupName = "Bing/Search",
                        AppxPackageName = "Microsoft.BingWeather",
                        WinGetPackageId = "9WZDNCRFJ3Q2",
                        Category = "Bing/Search",
                        CanBeReinstalled = true
                    },

                    // Camera/Media
                    new ItemDefinition
                    {
                        Id = "windows-app-camera",
                        Name = "Camera",
                        Description = "Windows Camera app",
                        GroupName = "Camera/Media",
                        AppxPackageName = "Microsoft.WindowsCamera",
                        WinGetPackageId = "9WZDNCRFJBBG",
                        Category = "Camera/Media",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-clipchamp",
                        Name = "Clipchamp",
                        Description = "Video editor app",
                        GroupName = "Camera/Media",
                        AppxPackageName = "Clipchamp.Clipchamp",
                        WinGetPackageId = "9P1J8S7CCWWT",
                        Category = "Camera/Media",
                        CanBeReinstalled = true
                    },

                    // System Utilities
                    new ItemDefinition
                    {
                        Id = "windows-app-alarms-clock",
                        Name = "Alarms & Clock",
                        Description = "Clock, alarms, timer, and stopwatch app",
                        GroupName = "System Utilities",
                        AppxPackageName = "Microsoft.WindowsAlarms",
                        WinGetPackageId = "9WZDNCRFJ3PR",
                        Category = "System Utilities",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-cortana",
                        Name = "Cortana",
                        Description = "Microsoft's virtual assistant",
                        GroupName = "System Utilities",
                        AppxPackageName = "Microsoft.549981C3F5F10",
                        WinGetPackageId = "9NFFX4SZZ23L",
                        Category = "System Utilities",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-get-help",
                        Name = "Get Help",
                        Description = "Microsoft support app",
                        GroupName = "System Utilities",
                        AppxPackageName = "Microsoft.GetHelp",
                        WinGetPackageId = "9PKDZBMV1H3T",
                        Category = "System Utilities",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-calculator",
                        Name = "Calculator",
                        Description = "Calculator app with standard, scientific, and programmer modes",
                        GroupName = "System Utilities",
                        AppxPackageName = "Microsoft.WindowsCalculator",
                        WinGetPackageId = "9WZDNCRFHVN5",
                        Category = "System Utilities",
                        CanBeReinstalled = true
                    },

                    // Development
                    new ItemDefinition
                    {
                        Id = "windows-app-dev-home",
                        Name = "Dev Home",
                        Description = "Development environment for Windows",
                        GroupName = "Development",
                        AppxPackageName = "Microsoft.Windows.DevHome",
                        WinGetPackageId = "9WZDNCRFHVN5",
                        Category = "Development",
                        CanBeReinstalled = true
                    },

                    // Communication
                    new ItemDefinition
                    {
                        Id = "windows-app-family-safety",
                        Name = "Microsoft Family Safety",
                        Description = "Family safety and screen time management",
                        GroupName = "Communication",
                        AppxPackageName = "MicrosoftCorporationII.MicrosoftFamily",
                        WinGetPackageId = "9PDJDJS743XF",
                        Category = "Communication",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-mail-calendar",
                        Name = "Mail and Calendar",
                        Description = "Microsoft Mail and Calendar apps",
                        GroupName = "Communication",
                        AppxPackageName = "microsoft.windowscommunicationsapps",
                        WinGetPackageId = "9WZDNCRFHVQM",
                        Category = "Communication",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-skype",
                        Name = "Skype",
                        Description = "Video calling and messaging app",
                        GroupName = "Communication",
                        AppxPackageName = "Microsoft.SkypeApp",
                        WinGetPackageId = "9WZDNCRFJ364",
                        Category = "Communication",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-teams",
                        Name = "Microsoft Teams",
                        Description = "Team collaboration and communication app",
                        GroupName = "Communication",
                        AppxPackageName = "MSTeams",
                        WinGetPackageId = "XP8BT8DW290MPQ",
                        Category = "Communication",
                        CanBeReinstalled = true
                    },

                    // System Tools
                    new ItemDefinition
                    {
                        Id = "windows-app-feedback-hub",
                        Name = "Feedback Hub",
                        Description = "App for sending feedback to Microsoft",
                        GroupName = "System Tools",
                        AppxPackageName = "Microsoft.WindowsFeedbackHub",
                        WinGetPackageId = "9NBLGGH4R32N",
                        Category = "System Tools",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-maps",
                        Name = "Maps",
                        Description = "Microsoft Maps app",
                        GroupName = "System Tools",
                        AppxPackageName = "Microsoft.WindowsMaps",
                        WinGetPackageId = "9WZDNCRDTBVB",
                        Category = "System Tools",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-terminal",
                        Name = "Terminal",
                        Description = "Modern terminal application for Windows",
                        GroupName = "System Tools",
                        AppxPackageName = "Microsoft.WindowsTerminal",
                        WinGetPackageId = "9N0DX20HK701",
                        Category = "System Tools",
                        CanBeReinstalled = true
                    },

                    // Office & Productivity
                    new ItemDefinition
                    {
                        Id = "windows-app-office-hub",
                        Name = "Office Hub",
                        Description = "Microsoft Office app hub",
                        GroupName = "Office",
                        AppxPackageName = "Microsoft.MicrosoftOfficeHub",
                        Category = "Office",
                        CanBeReinstalled = false
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-outlook",
                        Name = "Outlook for Windows",
                        Description = "Reimagined Outlook app for Windows",
                        GroupName = "Office",
                        AppxPackageName = "Microsoft.OutlookForWindows",
                        WinGetPackageId = "9NRX63209R7B",
                        Category = "Office",
                        CanBeReinstalled = true
                    },

                    // Graphics & Images
                    new ItemDefinition
                    {
                        Id = "windows-app-paint-3d",
                        Name = "Paint 3D",
                        Description = "3D modeling and editing app",
                        GroupName = "Graphics",
                        AppxPackageName = "Microsoft.MSPaint",
                        Category = "Graphics",
                        CanBeReinstalled = false
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-paint",
                        Name = "Paint",
                        Description = "Traditional image editing app",
                        GroupName = "Graphics",
                        AppxPackageName = "Microsoft.Paint",
                        WinGetPackageId = "9PCFS5B6T72H",
                        Category = "Graphics",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-photos",
                        Name = "Photos",
                        Description = "Photo viewing and editing app",
                        GroupName = "Graphics",
                        AppxPackageName = "Microsoft.Windows.Photos",
                        WinGetPackageId = "9WZDNCRFJBH4",
                        Category = "Graphics",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-snipping-tool",
                        Name = "Snipping Tool",
                        Description = "Screen capture and annotation tool",
                        GroupName = "Graphics",
                        AppxPackageName = "Microsoft.ScreenSketch",
                        WinGetPackageId = "9MZ95KL8MR0L",
                        Category = "Graphics",
                        CanBeReinstalled = true
                    },

                    // Social & People
                    new ItemDefinition
                    {
                        Id = "windows-app-people",
                        Name = "People",
                        Description = "Contact management app",
                        GroupName = "Social",
                        AppxPackageName = "Microsoft.People",
                        WinGetPackageId = "9NBLGGH10PG8",
                        Category = "Social",
                        CanBeReinstalled = true
                    },

                    // Automation
                    new ItemDefinition
                    {
                        Id = "windows-app-power-automate",
                        Name = "Power Automate",
                        Description = "Desktop automation tool",
                        GroupName = "Automation",
                        AppxPackageName = "Microsoft.PowerAutomateDesktop",
                        WinGetPackageId = "9NFTCH6J7FHV",
                        Category = "Automation",
                        CanBeReinstalled = true
                    },

                    // Support Tools
                    new ItemDefinition
                    {
                        Id = "windows-app-quick-assist",
                        Name = "Quick Assist",
                        Description = "Remote assistance tool",
                        GroupName = "Support",
                        AppxPackageName = "MicrosoftCorporationII.QuickAssist",
                        WinGetPackageId = "9P7BP5VNWKX5",
                        Category = "Support",
                        CanBeReinstalled = true
                    },

                    // Games & Entertainment
                    new ItemDefinition
                    {
                        Id = "windows-app-solitaire",
                        Name = "Solitaire Collection",
                        Description = "Microsoft Solitaire Collection games",
                        GroupName = "Games",
                        AppxPackageName = "Microsoft.MicrosoftSolitaireCollection",
                        WinGetPackageId = "9WZDNCRFHWD2",
                        Category = "Games",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-xbox",
                        Name = "Xbox",
                        Description = "Xbox App for Windows",
                        GroupName = "Games",
                        AppxPackageName = "Microsoft.GamingApp",
                        WinGetPackageId = "9MV0B5HZVK9Z",
                        Category = "Games",
                        CanBeReinstalled = true,
                        SubPackages = new string[] { "Microsoft.XboxApp" },
                        RegistrySettings = new List<RegistrySetting>
                        {
                            new RegistrySetting
                            {
                                KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR",
                                ValueName = "AppCaptureEnabled",
                                RecommendedValue = 0,
                                ValueType = RegistryValueKind.DWord,
                            }
                        }
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-xbox-identity-provider",
                        Name = "Xbox Identity Provider",
                        Description = "Authentication service for Xbox Live and related Microsoft gaming services",
                        GroupName = "Games",
                        AppxPackageName = "Microsoft.XboxIdentityProvider",
                        WinGetPackageId = "9WZDNCRD1HKW",
                        Category = "Games",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-xbox-game-bar-plugin",
                        Name = "Xbox Game Bar Plugin",
                        Description = "Extension component for Xbox Game Bar providing additional functionality",
                        GroupName = "Games",
                        AppxPackageName = "Microsoft.XboxGameOverlay",
                        WinGetPackageId = "9NBLGGH537C2",
                        Category = "Games",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-xbox-live-ingame",
                        Name = "Xbox Live In-Game Experience",
                        Description = "Core component for Xbox Live services within games",
                        GroupName = "Games",
                        AppxPackageName = "Microsoft.Xbox.TCUI",
                        WinGetPackageId = "9NKNC0LD5NN6",
                        Category = "Games",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-xbox-game-bar",
                        Name = "Xbox Game Bar",
                        Description = "Gaming overlay with screen capture, performance monitoring, and social features",
                        GroupName = "Games",
                        AppxPackageName = "Microsoft.XboxGamingOverlay",
                        WinGetPackageId = "9NZKPSTSNW4P",
                        Category = "Games",
                        CanBeReinstalled = true
                    },

                    // Windows Store
                    new ItemDefinition
                    {
                        Id = "windows-app-store",
                        Name = "Microsoft Store",
                        Description = "App store for Windows",
                        GroupName = "Store",
                        AppxPackageName = "Microsoft.WindowsStore",
                        WinGetPackageId = "9WZDNCRFJBMP",
                        Category = "Store",
                        CanBeReinstalled = true
                    },

                    // Media Players
                    new ItemDefinition
                    {
                        Id = "windows-app-media-player",
                        Name = "Media Player",
                        Description = "Music player app",
                        GroupName = "Media",
                        AppxPackageName = "Microsoft.ZuneMusic",
                        WinGetPackageId = "9WZDNCRFJ3PT",
                        Category = "Media",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-movies-tv",
                        Name = "Movies & TV",
                        Description = "Video player app",
                        GroupName = "Media",
                        AppxPackageName = "Microsoft.ZuneVideo",
                        WinGetPackageId = "9WZDNCRFJ3P2",
                        Category = "Media",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-sound-recorder",
                        Name = "Sound Recorder",
                        Description = "Audio recording app",
                        GroupName = "Media",
                        AppxPackageName = "Microsoft.WindowsSoundRecorder",
                        WinGetPackageId = "9WZDNCRFHWKN",
                        Category = "Media",
                        CanBeReinstalled = true
                    },

                    // Productivity Tools
                    new ItemDefinition
                    {
                        Id = "windows-app-sticky-notes",
                        Name = "Sticky Notes",
                        Description = "Note-taking app",
                        GroupName = "Productivity",
                        AppxPackageName = "Microsoft.MicrosoftStickyNotes",
                        WinGetPackageId = "9NBLGGH4QGHW",
                        Category = "Productivity",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-tips",
                        Name = "Tips",
                        Description = "Windows tutorial app",
                        GroupName = "Productivity",
                        AppxPackageName = "Microsoft.Getstarted",
                        Category = "Productivity",
                        CanBeReinstalled = false
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-todo",
                        Name = "To Do",
                        Description = "Task management app",
                        GroupName = "Productivity",
                        AppxPackageName = "Microsoft.Todos",
                        WinGetPackageId = "9NBLGGH5R558",
                        Category = "Productivity",
                        CanBeReinstalled = true
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-notepad",
                        Name = "Notepad",
                        Description = "Text editing app",
                        GroupName = "Productivity",
                        AppxPackageName = "Microsoft.WindowsNotepad",
                        WinGetPackageId = "9MSMLRH6LZF3",
                        Category = "Productivity",
                        CanBeReinstalled = true
                    },

                    // Phone Integration
                    new ItemDefinition
                    {
                        Id = "windows-app-phone-link",
                        Name = "Phone Link",
                        Description = "Connect your Android or iOS device to Windows",
                        GroupName = "Phone",
                        AppxPackageName = "Microsoft.YourPhone",
                        WinGetPackageId = "9NMPJ99VJBWV",
                        Category = "Phone",
                        CanBeReinstalled = true
                    },

                    // AI & Copilot
                    new ItemDefinition
                    {
                        Id = "windows-app-copilot",
                        Name = "Copilot",
                        Description = "AI assistant for Windows, includes Copilot provider and Store components",
                        GroupName = "AI",
                        AppxPackageName = "Microsoft.Copilot",
                        WinGetPackageId = "9NHT9RB2F4HD",
                        Category = "AI",
                        CanBeReinstalled = true,
                        SubPackages = new string[]
                        {
                            "Microsoft.Windows.Ai.Copilot.Provider",
                            "Microsoft.Copilot_8wekyb3d8bbwe"
                        },
                    },

                    // Special Items that require dedicated scripts
                    new ItemDefinition
                    {
                        Id = "windows-app-edge",
                        Name = "Microsoft Edge",
                        Description = "Microsoft's web browser",
                        GroupName = "Browsers",
                        AppxPackageName = "Microsoft.MicrosoftEdge.Stable",
                        WinGetPackageId = "XPFFTQ037JWMHS",
                        Category = "Browsers",
                        CanBeReinstalled = true,
                        RemovalScript = () => EdgeRemovalScript.GetScript()
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-onedrive",
                        Name = "OneDrive",
                        Description = "Microsoft's cloud storage service",
                        GroupName = "System",
                        AppxPackageName = "Microsoft.OneDriveSync",
                        WinGetPackageId = "Microsoft.OneDrive",
                        Category = "System",
                        CanBeReinstalled = true,
                        RemovalScript = () => OneDriveRemovalScript.GetScript()
                    },
                    new ItemDefinition
                    {
                        Id = "windows-app-onenote",
                        Name = "OneNote",
                        Description = "Microsoft note-taking app",
                        GroupName = "Office",
                        AppxPackageName = "Microsoft.Office.OneNote",
                        WinGetPackageId = "XPFFZHVGQWWLHB",
                        Category = "Office",
                        CanBeReinstalled = true
                    }
                }
            };
        }
    }
}