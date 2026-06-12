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
                    Description = "View 3D models, animations, and AR scenes in glTF, FBX, and OBJ formats",
                    GroupName = "3D/Mixed Reality",
                    AppxPackageName = ["Microsoft.Microsoft3DViewer"],
                    MsStoreId = "9NBLGGH42THS",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-mixed-reality-portal",
                    Name = "Mixed Reality Portal",
                    Description = "Setup and launcher for Windows VR headsets (deprecated in Windows 11 24H2)",
                    GroupName = "3D/Mixed Reality",
                    AppxPackageName = ["Microsoft.MixedReality.Portal"],
                    MsStoreId = "9NG1H8B3ZC7M",
                    CanBeReinstalled = true
                },

                // Bing/Search
                new ItemDefinition
                {
                    Id = "windows-app-bing-search",
                    Name = "Bing Search",
                    Description = "Web search results delivered inside Windows Search and the Start menu",
                    GroupName = "Bing/Search",
                    AppxPackageName = ["Microsoft.BingSearch"],
                    MsStoreId = "9NZBF4GT040C",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-microsoft-news",
                    Name = "Microsoft News",
                    Description = "News reader powered by Microsoft Start with personalised headlines",
                    GroupName = "Bing/Search",
                    AppxPackageName = ["Microsoft.BingNews"],
                    MsStoreId = "9WZDNCRFHVFW",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-msn-weather",
                    Name = "MSN Weather",
                    Description = "Weather forecasts, radar, and 10-day outlook from MSN",
                    GroupName = "Bing/Search",
                    AppxPackageName = ["Microsoft.BingWeather"],
                    MsStoreId = "9WZDNCRFJ3Q2",
                    CanBeReinstalled = true
                },

                // Camera/Media
                new ItemDefinition
                {
                    Id = "windows-app-camera",
                    Name = "Camera",
                    Description = "Take photos and record video using your webcam or built-in camera",
                    GroupName = "Camera/Media",
                    AppxPackageName = ["Microsoft.WindowsCamera"],
                    MsStoreId = "9WZDNCRFJBBG",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-clipchamp",
                    Name = "Clipchamp",
                    Description = "Microsoft's video editor with templates, effects, and screen recording",
                    GroupName = "Camera/Media",
                    AppxPackageName = ["Clipchamp.Clipchamp"],
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
                    AppxPackageName = ["Microsoft.WindowsAlarms"],
                    MsStoreId = "9WZDNCRFJ3PR",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-cortana",
                    Name = "Cortana",
                    Description = "Microsoft's voice assistant; deprecated and replaced by Copilot in Windows 11",
                    GroupName = "System Utilities",
                    AppxPackageName = ["Microsoft.549981C3F5F10"],
                    MsStoreId = "9NFFX4SZZ23L", // Package is deprecated
                    CanBeReinstalled = false
                },
                new ItemDefinition
                {
                    Id = "windows-app-get-help",
                    Name = "Get Help",
                    Description = "Microsoft's built-in help and support assistant for Windows issues",
                    GroupName = "System Utilities",
                    AppxPackageName = ["Microsoft.GetHelp"],
                    MsStoreId = "9PKDZBMV1H3T",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-calculator",
                    Name = "Calculator",
                    Description = "Calculator app with standard, scientific, and programmer modes",
                    GroupName = "System Utilities",
                    AppxPackageName = ["Microsoft.WindowsCalculator"],
                    MsStoreId = "9WZDNCRFHVN5",
                    CanBeReinstalled = true
                },

                // Development
                new ItemDefinition
                {
                    Id = "windows-app-dev-home",
                    Name = "Windows Advanced Settings",
                    Description = "Power-user Settings page: sudo, long paths, Dev Drive, Git in Explorer",
                    GroupName = "Development",
                    AppxPackageName = ["Microsoft.Windows.DevHome"],
                    // Successor to Dev Home (same MSIX family); backs System > Advanced page on Win11 25H2.
                    MsStoreId = "9N8MHTPHNGVV",
                    CanBeReinstalled = true
                },

                // Communication
                new ItemDefinition
                {
                    Id = "windows-app-family-safety",
                    Name = "Microsoft Family Safety",
                    Description = "Family safety and screen time management",
                    GroupName = "Communication",
                    AppxPackageName = ["MicrosoftCorporationII.MicrosoftFamily"],
                    MsStoreId = "9PDJDJS743XF",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-mail-calendar",
                    Name = "Mail and Calendar",
                    Description = "Built-in email client and calendar (replaced by new Outlook in Windows 11)",
                    GroupName = "Communication",
                    AppxPackageName = ["microsoft.windowscommunicationsapps"],
                    MsStoreId = "9WZDNCRFHVQM",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-skype",
                    Name = "Skype",
                    Description = "Video calling and messaging app; retired by Microsoft in May 2025",
                    GroupName = "Communication",
                    AppxPackageName = ["Microsoft.SkypeApp"],
                    MsStoreId = "9WZDNCRFJ364", // Skype is retired
                    CanBeReinstalled = false
                },
                new ItemDefinition
                {
                    Id = "windows-app-teams",
                    Name = "Microsoft Teams",
                    Description = "Microsoft's team chat, meetings, and collaboration app",
                    GroupName = "Communication",
                    AppxPackageName = ["MSTeams"],
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
                    AppxPackageName = ["Microsoft.WindowsFeedbackHub"],
                    MsStoreId = "9NBLGGH4R32N",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-maps",
                    Name = "Maps",
                    Description = "Offline maps and turn-by-turn directions (removed from Store July 2025)",
                    GroupName = "System Tools",
                    AppxPackageName = ["Microsoft.WindowsMaps"],
                    MsStoreId = "9WZDNCRDTBVB", // globally Accessible=false; Microsoft removed July 2025
                    CanBeReinstalled = false
                },
                new ItemDefinition
                {
                    Id = "windows-app-terminal",
                    Name = "Terminal",
                    Description = "Tabbed terminal that hosts PowerShell, Command Prompt, and WSL shells",
                    GroupName = "System Tools",
                    AppxPackageName = ["Microsoft.WindowsTerminal"],
                    MsStoreId = "9N0DX20HK701",
                    CanBeReinstalled = true
                },

                // Office & Productivity
                new ItemDefinition
                {
                    Id = "windows-app-office-hub",
                    Name = "MS 365 Copilot (Office Hub)",
                    Description = "Launcher and recent-files dashboard for Microsoft 365 apps and Copilot",
                    GroupName = "Office",
                    AppxPackageName = ["Microsoft.MicrosoftOfficeHub"],
                    MsStoreId = "9WZDNCRD29V9",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-outlook",
                    Name = "Outlook for Windows",
                    Description = "New web-based Outlook client replacing the classic Mail and Calendar apps",
                    GroupName = "Office",
                    AppxPackageName = ["Microsoft.OutlookForWindows"],
                    MsStoreId = "9NRX63209R7B",
                    CanBeReinstalled = true
                },

                // Graphics & Images
                new ItemDefinition
                {
                    Id = "windows-app-paint-3d",
                    Name = "Paint 3D",
                    Description = "3D modeling app for creating and editing scenes; deprecated by Microsoft",
                    GroupName = "Graphics",
                    AppxPackageName = ["Microsoft.MSPaint"],
                    CanBeReinstalled = false
                },
                new ItemDefinition
                {
                    Id = "windows-app-paint",
                    Name = "Paint",
                    Description = "Classic image editor with brushes, layers, and AI Cocreator generation",
                    GroupName = "Graphics",
                    AppxPackageName = ["Microsoft.Paint"],
                    MsStoreId = "9PCFS5B6T72H",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-photos",
                    Name = "Photos",
                    Description = "Default photo viewer and editor with crop, markup, and slideshow tools",
                    GroupName = "Graphics",
                    AppxPackageName = ["Microsoft.Windows.Photos"],
                    MsStoreId = "9WZDNCRFJBH4",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-snipping-tool",
                    Name = "Snipping Tool",
                    Description = "Screen capture and annotation tool",
                    GroupName = "Graphics",
                    AppxPackageName = ["Microsoft.ScreenSketch"],
                    MsStoreId = "9MZ95KL8MR0L",
                    CanBeReinstalled = true
                },

                // Social & People
                new ItemDefinition
                {
                    Id = "windows-app-people",
                    Name = "People",
                    Description = "Contacts hub for Outlook, Skype, and other accounts (no longer installable)",
                    GroupName = "Social",
                    AppxPackageName = ["Microsoft.People"],
                    MsStoreId = "9NBLGGH10PG8", // globally Accessible=false; Microsoft deprecated
                    CanBeReinstalled = false
                },

                // Automation
                new ItemDefinition
                {
                    Id = "windows-app-power-automate",
                    Name = "Power Automate",
                    Description = "Desktop automation builder for recording and scheduling repetitive tasks",
                    GroupName = "Automation",
                    AppxPackageName = ["Microsoft.PowerAutomateDesktop"],
                    MsStoreId = "9NFTCH6J7FHV",
                    CanBeReinstalled = true
                },

                // Support Tools
                new ItemDefinition
                {
                    Id = "windows-app-quick-assist",
                    Name = "Quick Assist",
                    Description = "Remote assistance app for sharing your screen with someone helping you",
                    GroupName = "Support",
                    AppxPackageName = ["MicrosoftCorporationII.QuickAssist"],
                    MsStoreId = "9P7BP5VNWKX5",
                    CanBeReinstalled = true
                },

                // Games & Entertainment
                new ItemDefinition
                {
                    Id = "windows-app-solitaire",
                    Name = "Solitaire Collection",
                    Description = "Bundle of card games including Klondike, Spider, and FreeCell (with ads)",
                    GroupName = "Games",
                    AppxPackageName = ["Microsoft.MicrosoftSolitaireCollection"],
                    MsStoreId = "9WZDNCRFHWD2", // https://apps.microsoft.com/detail/9wzdncrfhwd2?hl=en-US&gl=ZA
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-xbox",
                    Name = "Xbox",
                    Description = "Game library, Game Pass storefront, and PC game launcher",
                    GroupName = "Games",
                    AppxPackageName = ["Microsoft.GamingApp", "Microsoft.XboxApp"],
                    MsStoreId = "9MV0B5HZVK9Z",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-xbox-identity-provider",
                    Name = "Xbox Identity Provider",
                    Description = "Authentication service for Xbox Live and related Microsoft gaming services",
                    GroupName = "Games",
                    AppxPackageName = ["Microsoft.XboxIdentityProvider"],
                    MsStoreId = "9WZDNCRD1HKW",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-xbox-game-bar-plugin",
                    Name = "Xbox Game Bar Plugin",
                    Description = "Game Bar overlay component (Microsoft unpublished; auto-installed with Game Bar)",
                    GroupName = "Games",
                    AppxPackageName = ["Microsoft.XboxGameOverlay"],
                    MsStoreId = "9NBLGGH537C2", // globally Accessible=false since 2020-06; bundled with Xbox Game Bar
                    CanBeReinstalled = false
                },
                new ItemDefinition
                {
                    Id = "windows-app-xbox-live-ingame",
                    Name = "Xbox Live In-Game Experience",
                    Description = "Title Callable UI for Xbox Live overlays and dialogs shown inside games",
                    GroupName = "Games",
                    AppxPackageName = ["Microsoft.Xbox.TCUI"],
                    MsStoreId = "9NKNC0LD5NN6",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-xbox-game-bar",
                    Name = "Xbox Game Bar",
                    Description = "Gaming overlay with screen capture, performance monitoring, and social features",
                    GroupName = "Games",
                    AppxPackageName = ["Microsoft.XboxGamingOverlay"],
                    MsStoreId = "9NZKPSTSNW4P",
                    CanBeReinstalled = true
                },

                // Windows Store
                new ItemDefinition
                {
                    Id = "windows-app-store",
                    Name = "Microsoft Store",
                    Description = "Official storefront for installing apps, games, and updates on Windows",
                    GroupName = "Store",
                    AppxPackageName = ["Microsoft.WindowsStore"],
                    MsStoreId = "9WZDNCRFJBMP",
                    CanBeReinstalled = true
                },

                // Media Players
                new ItemDefinition
                {
                    Id = "windows-app-media-player",
                    Name = "Media Player",
                    Description = "Plays local music and video files; replaces Groove Music",
                    GroupName = "Media",
                    AppxPackageName = ["Microsoft.ZuneMusic"],
                    MsStoreId = "9WZDNCRFJ3PT",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-movies-tv",
                    Name = "Movies & TV",
                    Description = "Plays local video files and rented or purchased Microsoft Store content",
                    GroupName = "Media",
                    AppxPackageName = ["Microsoft.ZuneVideo"],
                    MsStoreId = "9WZDNCRFJ3P2",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-sound-recorder",
                    Name = "Sound Recorder",
                    Description = "Voice and audio recording app with playback, trim, and export",
                    GroupName = "Media",
                    AppxPackageName = ["Microsoft.WindowsSoundRecorder"],
                    MsStoreId = "9WZDNCRFHWKN",
                    CanBeReinstalled = true
                },

                // Productivity Tools
                new ItemDefinition
                {
                    Id = "windows-app-sticky-notes",
                    Name = "Sticky Notes",
                    Description = "On-screen notes that sync across devices via your Microsoft account",
                    GroupName = "Productivity",
                    AppxPackageName = ["Microsoft.MicrosoftStickyNotes"],
                    MsStoreId = "9NBLGGH4QGHW",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-tips",
                    Name = "Tips",
                    Description = "Walk-through tutorials and tips for new Windows features",
                    GroupName = "Productivity",
                    AppxPackageName = ["Microsoft.Getstarted"],
                    CanBeReinstalled = false
                },
                new ItemDefinition
                {
                    Id = "windows-app-todo",
                    Name = "To Do: Lists, Tasks & Reminders",
                    Description = "Cloud-synced task list and reminders, integrated with Outlook tasks",
                    GroupName = "Productivity",
                    AppxPackageName = ["Microsoft.Todos"],
                    MsStoreId = "9NBLGGH5R558",
                    CanBeReinstalled = true
                },
                new ItemDefinition
                {
                    Id = "windows-app-notepad",
                    Name = "Notepad",
                    Description = "Default text editor with tabs, autosave, and optional AI rewrite",
                    GroupName = "Productivity",
                    AppxPackageName = ["Microsoft.WindowsNotepad"],
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
                    AppxPackageName = ["Microsoft.YourPhone"],
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
                    AppxPackageName = ["Microsoft.Copilot", "Microsoft.Windows.Ai.Copilot.Provider", "Microsoft.Copilot_8wekyb3d8bbwe"],
                    MsStoreId = "XP9CXNGPPJ97XX",
                    CanBeReinstalled = true,
                },
                new ItemDefinition
                {
                    Id = "windows-app-client-aix",
                    Name = "Windows AI Experience",
                    Description = "Core Windows AI platform package powering Copilot and Click to Do features",
                    GroupName = "AI",
                    AppxPackageName = ["MicrosoftWindows.Client.AIX"],
                    CanBeReinstalled = false,
                },
                new ItemDefinition
                {
                    Id = "windows-app-client-copilot",
                    Name = "Windows Copilot Client",
                    Description = "System Copilot client powering the Copilot button and shell integrations",
                    GroupName = "AI",
                    AppxPackageName = ["MicrosoftWindows.Client.CoPilot"],
                    CanBeReinstalled = false,
                },
                new ItemDefinition
                {
                    Id = "windows-app-edge-game-assist",
                    Name = "Edge Game Assist",
                    Description = "Browser-based overlay for guides and search while playing PC games",
                    GroupName = "AI",
                    AppxPackageName = ["Microsoft.Edge.GameAssist"],
                    CanBeReinstalled = false,
                },
                new ItemDefinition
                {
                    Id = "windows-app-office-actions-server",
                    Name = "Office Actions Server",
                    Description = "Background server that runs Office's AI-powered automated actions",
                    GroupName = "AI",
                    AppxPackageName = ["Microsoft.Office.ActionsServer"],
                    CanBeReinstalled = false,
                },
                new ItemDefinition
                {
                    Id = "windows-app-ai-manager",
                    Name = "AI Manager",
                    Description = "Office AI Manager that brokers local AI requests for Office apps",
                    GroupName = "AI",
                    AppxPackageName = ["aimgr"],
                    CanBeReinstalled = false,
                },
                new ItemDefinition
                {
                    Id = "windows-app-writing-assistant",
                    Name = "Writing Assistant",
                    Description = "Office AI helper for grammar, rewriting, and content suggestions",
                    GroupName = "AI",
                    AppxPackageName = ["Microsoft.WritingAssistant"],
                    CanBeReinstalled = false,
                },
                new ItemDefinition
                {
                    Id = "windows-app-ai-workloads",
                    Name = "AI Workload Packages",
                    Description = "On-device AI packages for OCR, image search, super-resolution, and summarization",
                    GroupName = "AI",
                    AppxPackageName = [
                        "WindowsWorkload.OnnxRuntimeGenAI",
                        "WindowsWorkload.SemanticTextSimilarity",
                        "WindowsWorkload.ImageSearch",
                        "WindowsWorkload.ContentExtraction",
                        "WindowsWorkload.ScreenRegionDetection",
                        "WindowsWorkload.TextRecognition",
                        "WindowsWorkload.ImageContentModeration",
                        "WindowsWorkload.ScreenSemanticsOCR",
                        "WindowsWorkload.ImageSuperResolution",
                        "WindowsWorkload.ObjectErase",
                        "WindowsWorkload.ImageDescriptor",
                        "WindowsWorkload.PortraitBlur",
                        "WindowsWorkload.TextToSpeech",
                        "WindowsWorkload.SpeechToText",
                        "WindowsWorkload.ImageSegmentation",
                        "WindowsWorkload.CreativeFilter",
                        "WindowsWorkload.StudioEffects",
                        "WindowsWorkload.SubtitleTranslation",
                        "WindowsWorkload.Summarization",
                        "WindowsWorkload.RewriteTextSuggestion",
                    ],
                    CanBeReinstalled = false,
                },
                new ItemDefinition
                {
                    Id = "windows-app-copilot-plus-pc",
                    Name = "Copilot+ PC AI Packages",
                    Description = "AI voice, speech, live typing, input, and file operation packages for NPU-equipped Copilot+ PCs",
                    GroupName = "AI",
                    AppxPackageName = [
                        "MicrosoftWindows.Voiess",
                        "MicrosoftWindows.Speion",
                        "MicrosoftWindows.Livtop",
                        "MicrosoftWindows.InpApp",
                        "MicrosoftWindows.Filons",
                    ],
                    CanBeReinstalled = false,
                },

                // Special Items that require dedicated scripts or carry instability warnings
                new ItemDefinition
                {
                    Id = "windows-app-edge",
                    Name = "Microsoft Edge",
                    Description = "Microsoft's Chromium-based web browser bundled with Windows",
                    HasInstabilityWarning = true,
                    GroupName = "Browsers",
                    AppxPackageName = ["Microsoft.MicrosoftEdge.Stable"],
                    WinGetPackageId = ["Microsoft.Edge"],
                    MsStoreId = "XPFFTQ037JWMHS",
                    CanBeReinstalled = true,
                    RemovalScript = () => EdgeRemovalScript.GetScript()
                },
                new ItemDefinition
                {
                    Id = "windows-app-app-installer",
                    Name = "App Installer",
                    Description = "Microsoft's MSIX/AppX installer and winget command-line package manager",
                    HasInstabilityWarning = true,
                    GroupName = "System",
                    AppxPackageName = ["Microsoft.DesktopAppInstaller"],
                    WinGetPackageId = ["Microsoft.AppInstaller"],
                    MsStoreId = "9NBLGGH4NNS1",
                    CanBeReinstalled = true,
                },
                new ItemDefinition
                {
                    Id = "windows-app-onedrive",
                    Name = "OneDrive",
                    Description = "Microsoft's built-in cloud storage and file sync client",
                    GroupName = "System",
                    AppxPackageName = ["Microsoft.OneDriveSync"],
                    WinGetPackageId = ["Microsoft.OneDrive"],
                    // OneDrive isn't an AppX on a clean install (the
                    // Microsoft.OneDriveSync entry is just a Sync stub) and isn't
                    // listed on the Microsoft Store either, so Layer 1 and Layer 2a
                    // both come up empty. Layer 2b sources, in order:
                    //  1. The OneDrive.ico file Windows leaves in System32 even
                    //     after the OneDrive client is uninstalled — works for
                    //     most users including post-debloat installs.
                    //  2. Wikimedia Commons rasterization of the current (2025+)
                    //     Microsoft OneDrive brand mark, for users whose
                    //     debloater removed the .ico file too.
                    CanBeReinstalled = true,
                    RemovalScript = () => OneDriveRemovalScript.GetScript()
                },
                new ItemDefinition
                {
                    Id = "windows-app-onenote",
                    Name = "OneNote",
                    Description = "Free-form notebook app for typed, handwritten, and clipped notes",
                    GroupName = "Office",
                    AppxPackageName = ["Microsoft.Office.OneNote"],
                    MsStoreId = "XPFFZHVGQWWLHB",
                    CanBeReinstalled = true,
                    RegistrySubKeyName = "OneNoteFreeRetail - {locale}",
                    RegistryDisplayName = "Microsoft OneNote - {locale}",
                    ProcessesToStop = ["OneNote", "ONENOTE", "ONENOTEM"]
                }
            }
        };
    }
}
