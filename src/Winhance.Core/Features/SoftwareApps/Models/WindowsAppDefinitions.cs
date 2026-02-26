using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Models;

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
                    MsStoreId = "9NBLGGH42THS",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-mixed-reality-portal",
                    Name = "Mixed Reality Portal",
                    Description = "Portal for Windows Mixed Reality experiences",
                    GroupName = "3D/Mixed Reality",
                    AppxPackageName = "Microsoft.MixedReality.Portal",
                    MsStoreId = "9NG1H8B3ZC7M",
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
                    MsStoreId = "9NZBF4GT040C",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-microsoft-news",
                    Name = "Microsoft News",
                    Description = "Microsoft News app",
                    GroupName = "Bing/Search",
                    AppxPackageName = "Microsoft.BingNews",
                    MsStoreId = "9WZDNCRFHVFW",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-msn-weather",
                    Name = "MSN Weather",
                    Description = "Weather forecasts and information",
                    GroupName = "Bing/Search",
                    AppxPackageName = "Microsoft.BingWeather",
                    MsStoreId = "9WZDNCRFJ3Q2",
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
                    MsStoreId = "9WZDNCRFJBBG",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-clipchamp",
                    Name = "Clipchamp",
                    Description = "Video editor app",
                    GroupName = "Camera/Media",
                    AppxPackageName = "Clipchamp.Clipchamp",
                    MsStoreId = "9P1J8S7CCWWT",
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
                    MsStoreId = "9WZDNCRFJ3PR",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-cortana",
                    Name = "Cortana",
                    Description = "Microsoft's virtual assistant",
                    GroupName = "System Utilities",
                    AppxPackageName = "Microsoft.549981C3F5F10",
                    MsStoreId = "9NFFX4SZZ23L", // Package is deprecated
                    CanBeReinstalled = false
                },
                new ItemDefinition
                {
                    Id = "windows-app-get-help",
                    Name = "Get Help",
                    Description = "Microsoft support app",
                    GroupName = "System Utilities",
                    AppxPackageName = "Microsoft.GetHelp",
                    MsStoreId = "9PKDZBMV1H3T",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-calculator",
                    Name = "Calculator",
                    Description = "Calculator app with standard, scientific, and programmer modes",
                    GroupName = "System Utilities",
                    AppxPackageName = "Microsoft.WindowsCalculator",
                    MsStoreId = "9WZDNCRFHVN5",
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
                    MsStoreId = "9N8MHTPHNGVV", // not available in your market
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
                    MsStoreId = "9PDJDJS743XF",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-mail-calendar",
                    Name = "Mail and Calendar",
                    Description = "Microsoft Mail and Calendar apps",
                    GroupName = "Communication",
                    AppxPackageName = "microsoft.windowscommunicationsapps",
                    MsStoreId = "9WZDNCRFHVQM",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-skype",
                    Name = "Skype",
                    Description = "Video calling and messaging app",
                    GroupName = "Communication",
                    AppxPackageName = "Microsoft.SkypeApp",
                    MsStoreId = "9WZDNCRFJ364", // Skype is retired
                    CanBeReinstalled = false
                },
                new ItemDefinition
                {
                    Id = "windows-app-teams",
                    Name = "Microsoft Teams",
                    Description = "Team collaboration and communication app",
                    GroupName = "Communication",
                    AppxPackageName = "MSTeams",
                    MsStoreId = "XP8BT8DW290MPQ",
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
                    MsStoreId = "9NBLGGH4R32N",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-maps",
                    Name = "Maps",
                    Description = "Microsoft Maps app",
                    GroupName = "System Tools",
                    AppxPackageName = "Microsoft.WindowsMaps",
                    MsStoreId = "9WZDNCRDTBVB", // unavailable in your market
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-terminal",
                    Name = "Terminal",
                    Description = "Modern terminal application for Windows",
                    GroupName = "System Tools",
                    AppxPackageName = "Microsoft.WindowsTerminal",
                    MsStoreId = "9N0DX20HK701",
                    CanBeReinstalled = true
                },

                // Office & Productivity
                new ItemDefinition
                {
                    Id = "windows-app-office-hub",
                    Name = "MS 365 Copilot (Office Hub)",
                    Description = "Microsoft 365 Copilot (formerly known as Office hub)",
                    GroupName = "Office",
                    AppxPackageName = "Microsoft.MicrosoftOfficeHub",
                    MsStoreId = "9WZDNCRD29V9",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-outlook",
                    Name = "Outlook for Windows",
                    Description = "Reimagined Outlook app for Windows",
                    GroupName = "Office",
                    AppxPackageName = "Microsoft.OutlookForWindows",
                    MsStoreId = "9NRX63209R7B",
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
                    CanBeReinstalled = false
                },
                new ItemDefinition
                {
                    Id = "windows-app-paint",
                    Name = "Paint",
                    Description = "Traditional image editing app",
                    GroupName = "Graphics",
                    AppxPackageName = "Microsoft.Paint",
                    MsStoreId = "9PCFS5B6T72H",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-photos",
                    Name = "Photos",
                    Description = "Photo viewing and editing app",
                    GroupName = "Graphics",
                    AppxPackageName = "Microsoft.Windows.Photos",
                    MsStoreId = "9WZDNCRFJBH4",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-snipping-tool",
                    Name = "Snipping Tool",
                    Description = "Screen capture and annotation tool",
                    GroupName = "Graphics",
                    AppxPackageName = "Microsoft.ScreenSketch",
                    MsStoreId = "9MZ95KL8MR0L",
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
                    MsStoreId = "9NBLGGH10PG8", // unavailable in your market
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
                    MsStoreId = "9NFTCH6J7FHV",
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
                    MsStoreId = "9P7BP5VNWKX5",
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
                    MsStoreId = "9WZDNCRFHWD2", // https://apps.microsoft.com/detail/9wzdncrfhwd2?hl=en-US&gl=ZA
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-xbox",
                    Name = "Xbox",
                    Description = "Xbox App for Windows",
                    GroupName = "Games",
                    AppxPackageName = "Microsoft.GamingApp",
                    MsStoreId = "9MV0B5HZVK9Z",
                    CanBeReinstalled = true,
                    SubPackages = new string[] { "Microsoft.XboxApp" },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR",
                            ValueName = "AppCaptureEnabled",
                            EnabledValue = null,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\System\GameConfigStore",
                            ValueName = "GameDVR_Enabled",
                            EnabledValue = null,
                            DisabledValue = 0,
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
                    MsStoreId = "9WZDNCRD1HKW",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-xbox-game-bar-plugin",
                    Name = "Xbox Game Bar Plugin",
                    Description = "Extension component for Xbox Game Bar providing additional functionality",
                    GroupName = "Games",
                    AppxPackageName = "Microsoft.XboxGameOverlay",
                    MsStoreId = "9NBLGGH537C2", // unavailable in market
                    CanBeReinstalled = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR",
                            ValueName = "AppCaptureEnabled",
                            EnabledValue = null,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\System\GameConfigStore",
                            ValueName = "GameDVR_Enabled",
                            EnabledValue = null,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        }
                    }
                },
                new ItemDefinition
                {
                    Id = "windows-app-xbox-live-ingame",
                    Name = "Xbox Live In-Game Experience",
                    Description = "Core component for Xbox Live services within games",
                    GroupName = "Games",
                    AppxPackageName = "Microsoft.Xbox.TCUI",
                    MsStoreId = "9NKNC0LD5NN6",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-xbox-game-bar",
                    Name = "Xbox Game Bar",
                    Description = "Gaming overlay with screen capture, performance monitoring, and social features",
                    GroupName = "Games",
                    AppxPackageName = "Microsoft.XboxGamingOverlay",
                    MsStoreId = "9NZKPSTSNW4P",
                    CanBeReinstalled = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR",
                            ValueName = "AppCaptureEnabled",
                            EnabledValue = null,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\System\GameConfigStore",
                            ValueName = "GameDVR_Enabled",
                            EnabledValue = null,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        }
                    }
                },

                // Windows Store
                new ItemDefinition
                {
                    Id = "windows-app-store",
                    Name = "Microsoft Store",
                    Description = "App store for Windows",
                    GroupName = "Store",
                    AppxPackageName = "Microsoft.WindowsStore",
                    MsStoreId = "9WZDNCRFJBMP",
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
                    MsStoreId = "9WZDNCRFJ3PT",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-movies-tv",
                    Name = "Movies & TV",
                    Description = "Video player app",
                    GroupName = "Media",
                    AppxPackageName = "Microsoft.ZuneVideo",
                    MsStoreId = "9WZDNCRFJ3P2",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-sound-recorder",
                    Name = "Sound Recorder",
                    Description = "Audio recording app",
                    GroupName = "Media",
                    AppxPackageName = "Microsoft.WindowsSoundRecorder",
                    MsStoreId = "9WZDNCRFHWKN",
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
                    MsStoreId = "9NBLGGH4QGHW",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-tips",
                    Name = "Tips",
                    Description = "Windows tutorial app",
                    GroupName = "Productivity",
                    AppxPackageName = "Microsoft.Getstarted",
                    CanBeReinstalled = false
                },
                new ItemDefinition
                {
                    Id = "windows-app-todo",
                    Name = "To Do: Lists, Tasks & Reminders",
                    Description = "Task management app",
                    GroupName = "Productivity",
                    AppxPackageName = "Microsoft.Todos",
                    MsStoreId = "9NBLGGH5R558",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-notepad",
                    Name = "Notepad",
                    Description = "Text editing app",
                    GroupName = "Productivity",
                    AppxPackageName = "Microsoft.WindowsNotepad",
                    MsStoreId = "9MSMLRH6LZF3",
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
                    MsStoreId = "9NMPJ99VJBWV",
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
                    MsStoreId = "9NHT9RB2F4HD",
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
                    MsStoreId = "XPFFTQ037JWMHS",
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
                    WinGetPackageId = ["Microsoft.OneDrive"],
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
                    MsStoreId = "XPFFZHVGQWWLHB",
                    CanBeReinstalled = true,
                    RegistryUninstallSearchPattern = "OneNote*",
                    ProcessesToStop = ["OneNote", "ONENOTE", "ONENOTEM"]
                }
            }
        };
    }
}
