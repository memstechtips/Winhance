using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Models;

public static class GamingandPerformanceOptimizations
{
    public static SettingGroup GetGamingandPerformanceOptimizations()
    {
        return new SettingGroup
        {
            Name = "Gaming and Performance",
            FeatureId = FeatureIds.GamingPerformance,
            Settings = new List<SettingDefinition>
            {
                new SettingDefinition
                {
                    Id = "gaming-game-mode",
                    Name = "Game Mode",
                    Description = "Optimize your PC for play by turning things off in the background",
                    IconPack = "Fluent",
                    Icon = "TopSpeed",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\GameBar",
                            ValueName = "AutoGameModeEnabled",
                            RecommendedValue = 1,
                            EnabledValue = null, // When toggle is ON, Game Mode is enabled
                            DisabledValue = 0, // When toggle is OFF, Game Mode is disabled
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-explorer-mouse-precision",
                    Name = "Enhance Pointer Precision",
                    Description = "Adjust cursor speed based on movement velocity (mouse acceleration). Most competitive gamers disable this for consistent aiming in FPS games",
                    Icon = "Mouse",
                    InputType = InputType.Toggle,
                    RequiresRestart = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Mouse",
                            ValueName = "MouseSpeed",
                            RecommendedValue = "0",
                            EnabledValue = "1",
                            DisabledValue = "0",
                            DefaultValue = "1",
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-autostart-delay",
                    Name = "Startup Delay for Apps",
                    Description = "Delay startup applications by 10 seconds after boot to improve initial system responsiveness. Windows becomes usable faster, but your startup apps take longer to load",
                    Icon = "ClockStart",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize",
                            ValueName = "StartupDelayInMSec",
                            RecommendedValue = 0,
                            EnabledValue = 10000, // When toggle is ON, startup delay is enabled (10 seconds)
                            DisabledValue = 0, // When toggle is OFF, startup delay is disabled
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-background-apps",
                    Name = "Let Apps Run in Background",
                    Description = "Allow apps to receive notifications, update data, and perform tasks even when not actively in use",
                    Icon = "Apps",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
                            ValueName = "LetAppsRunInBackground",
                            RecommendedValue = 0,
                            EnabledValue = null,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
                            ValueName = "LetAppsRunInBackground",
                            RecommendedValue = 0,
                            EnabledValue = null,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-storage-sense",
                    Name = "Storage Sense",
                    Description = "Automatically free up disk space by removing temporary files, emptying the recycle bin, and managing downloads",
                    Icon = "Harddisk",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\StorageSense",
                            ValueName = "AllowStorageSenseGlobal",
                            RecommendedValue = 0,
                            EnabledValue = null,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\StorageSense",
                            ValueName = "AllowStorageSenseGlobal",
                            RecommendedValue = 0,
                            EnabledValue = null,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-explorer-search",
                    Name = "Search Entire File System",
                    Description = "Search your entire file system instead of only indexed locations. This provides more complete results but is significantly slower than indexed search and increases disk activity",
                    Icon = "FolderSearch",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Search\Preferences",
                            ValueName = "WholeFileSystem",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, search includes whole file system
                            DisabledValue = 0, // When toggle is OFF, search is limited to indexed locations
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-wallpaper-compression",
                    Name = "Allow Desktop Wallpaper Compression",
                    Description = "Allow Windows to compress wallpapers to save disk space and improve performance. Only affects images in JPEG format.",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "ResizeImage",
                    RestartProcess = "Explorer",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "JPEGImportQuality",
                            RecommendedValue = 100,
                            EnabledValue = null,
                            DisabledValue = 100,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-explorer-menu-show-delay",
                    Name = "Enable Menu Show Delay",
                    Description = "Add a brief delay before displaying menus, or show them instantly for faster navigation",
                    Icon = "MenuOpen",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "MenuShowDelay",
                            RecommendedValue = 0,
                            EnabledValue = 400, // When toggle is ON, menu show delay is enabled (default value)
                            DisabledValue = 0, // When toggle is OFF, menu show delay is disabled
                            DefaultValue = 400, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-explorer-alt-tab-filter",
                    Name = "Alt+Tab Filter",
                    Description = "Show only traditional open windows in Alt+Tab instead of including Microsoft Edge tabs and other Windows suggestions",
                    Icon = "ViewGrid",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "MultiTaskingAltTabFilter",
                            RecommendedValue = 3,
                            EnabledValue = 3,
                            DisabledValue = 0,
                            DefaultValue = 3, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // Processor Group
                new SettingDefinition
                {
                    Id = "gaming-win32-priority",
                    Name = "Adjust processor for best performance of",
                    Description = "Configure how Windows allocates CPU time between foreground applications and background services",
                    GroupName = "Processor",
                    Icon = "Application",
                    InputType = InputType.Selection,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\PriorityControl",
                            ValueName = "Win32PrioritySeparation",
                            RecommendedValue = 38, // Decimal
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "Programs",
                            "Background Services",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?>
                            {
                                ["Win32PrioritySeparation"] = 38, // Decimal
                            },
                            [1] = new Dictionary<string, object?>
                            {
                                ["Win32PrioritySeparation"] = 24, // Decimal
                            },
                        },
                    }
                },
                new SettingDefinition
                {
                    Id = "gaming-system-responsiveness",
                    Name = "System Responsiveness for Games",
                    Description = "Minimize background task interference by allocating more CPU time to your active game or multimedia application",
                    GroupName = "Processor",
                    Icon = "Speedometer",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                            ValueName = "SystemResponsiveness",
                            RecommendedValue = 10,
                            EnabledValue = 10, // When toggle is ON, system responsiveness is optimized for games (10 = prioritize foreground)
                            DisabledValue = 20, // When toggle is OFF, system responsiveness is balanced (20 = default Windows value)
                            DefaultValue = 20, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-cpu-priority",
                    Name = "CPU Priority for Gaming",
                    Description = "Give games higher CPU scheduling priority to dedicate more processor time to your game",
                    GroupName = "Processor",
                    Icon = "Chip",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            ValueName = "Priority",
                            RecommendedValue = 6,
                            EnabledValue = 6, // When toggle is ON, CPU priority is high (6 = high priority)
                            DisabledValue = 2, // When toggle is OFF, CPU priority is normal (default Windows value)
                            DefaultValue = 2, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-scheduling-category",
                    Name = "High Scheduling Category for Gaming",
                    Description = "Assign high-priority scheduling category to ensure games receive preferential system resource allocation",
                    GroupName = "Processor",
                    Icon = "CalendarClock",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            ValueName = "Scheduling Category",
                            RecommendedValue = "High",
                            EnabledValue = "High", // When toggle is ON, scheduling category is high
                            DisabledValue = "Medium", // When toggle is OFF, scheduling category is medium (default Windows value)
                            DefaultValue = "Medium", // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                // Graphics Group
                new SettingDefinition
                {
                    Id = "gaming-gpu-priority",
                    Name = "GPU Priority for Gaming",
                    Description = "Give games higher GPU scheduling priority to improve graphics performance and frame rates",
                    GroupName = "Graphics",
                    Icon = "Memory",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
                            ValueName = "GPU Priority",
                            RecommendedValue = 8,
                            EnabledValue = 8, // When toggle is ON, GPU priority is high (8 = high priority)
                            DisabledValue = 2, // When toggle is OFF, GPU priority is normal (default Windows value)
                            DefaultValue = 2, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-gpu-scheduling",
                    Name = "Hardware-Accelerated GPU Scheduling",
                    Description = "Let your GPU manage its own memory and scheduling for reduced latency and improved performance",
                    GroupName = "Graphics",
                    Icon = "ExpansionCard",
                    InputType = InputType.Toggle,
                    RequiresRestart = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\GraphicsDrivers",
                            ValueName = "HwSchMode",
                            RecommendedValue = 2,
                            EnabledValue = null,
                            DisabledValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-directx-flip-model",
                    Name = "Optimizations for windowed games",
                    Description = "Reduce latency and use advanced features in compatible games by using DirectX flip presentation model",
                    GroupName = "Graphics",
                    Icon = "ApplicationCog",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences",
                            ValueName = "DirectXUserGlobalSettings",
                            CompositeStringKey = "SwapEffectUpgradeEnable",
                            RecommendedValue = "1",
                            EnabledValue = "1",
                            DisabledValue = "0",
                            DefaultValue = "1",
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-directx-vrr-optimizations",
                    Name = "Variable Refresh Rate",
                    Description = "Enable VRR (G-Sync/FreeSync) optimizations for smoother gameplay. Requires a VRR-compatible monitor; this setting has no effect if your monitor does not support VRR",
                    GroupName = "Graphics",
                    Icon = "MonitorShimmer",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences",
                            ValueName = "DirectXUserGlobalSettings",
                            CompositeStringKey = "VRROptimizeEnable",
                            RecommendedValue = "1",
                            EnabledValue = "1",
                            DisabledValue = "0",
                            DefaultValue = "1",
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-directx-auto-hdr",
                    Name = "Auto HDR",
                    Description = "Automatically convert SDR content to HDR for enhanced colors and brightness. Requires an HDR-capable display with HDR enabled; this setting has no effect if your display does not support HDR",
                    GroupName = "Graphics",
                    Icon = "Hdr",
                    InputType = InputType.Toggle,
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences",
                            ValueName = "DirectXUserGlobalSettings",
                            CompositeStringKey = "AutoHDREnable",
                            RecommendedValue = "0",
                            EnabledValue = "1",
                            DisabledValue = "0",
                            DefaultValue = "0",
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-nvidia-sharpening",
                    Name = "Legacy NVIDIA Sharpening",
                    Description = "Enable legacy NVIDIA image sharpening filter for enhanced visual clarity. Only works on older NVIDIA drivers; newer drivers should use NVIDIA Control Panel sharpening instead",
                    GroupName = "Graphics",
                    Icon = "ImageFilterHdr",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\NVIDIA Corporation\Global\FTS",
                            ValueName = "EnableGR535",
                            RecommendedValue = 0,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-fullscreen-optimizations",
                    Name = "Fullscreen Optimizations",
                    Description = "Allow Windows to optimize games running in fullscreen mode. Disabling can fix performance issues or stuttering in some older games that don't work well with borderless fullscreen optimization",
                    GroupName = "Graphics",
                    Icon = "MonitorScreenshot",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\System\GameConfigStore",
                            ValueName = "GameDVR_FSEBehaviorMode",
                            RecommendedValue = 2,
                            EnabledValue = 0,
                            DisabledValue = 2,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-desktop-composition",
                    Name = "Desktop Composition Effects",
                    Description = "Enable visual effects managed by the Desktop Window Manager. Disabling may provide minor performance gains on older hardware but will break Aero effects",
                    GroupName = "Graphics",
                    Icon = "ViewDashboard",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM",
                            ValueName = "CompositionPolicy",
                            RecommendedValue = 0,
                            EnabledValue = null, // When toggle is ON, desktop composition is enabled
                            DisabledValue = 0, // When toggle is OFF, desktop composition is disabled
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // Network Group
                new SettingDefinition
                {
                    Id = "gaming-network-throttling",
                    Name = "Network Throttling",
                    Description = "Controls network packet rate limiting for multimedia applications. Keeping throttling enabled (default: 10 packets/ms) is recommended as it provides better DPC latency for gaming than disabling it entirely",
                    GroupName = "Network",
                    Icon = "NetworkOffOutline",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                            ValueName = "NetworkThrottlingIndex",
                            RecommendedValue = 10,
                            EnabledValue = 10,
                            DisabledValue = -1,
                            DefaultValue = 10,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-nagle-algorithm",
                    Name = "Nagle's Algorithm",
                    Description = "Buffers small network packets before sending to reduce overhead. Turn off to lower latency in online games, or keep on for general-purpose network efficiency",
                    GroupName = "Network",
                    Icon = "Wan",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces",
                            ValueName = "TcpAckFrequency",
                            RecommendedValue = 1,
                            EnabledValue = null,
                            DisabledValue = 1,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            ApplyPerNetworkInterface = true,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces",
                            ValueName = "TCPNoDelay",
                            RecommendedValue = 1,
                            EnabledValue = null,
                            DisabledValue = 1,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                            ApplyPerNetworkInterface = true,
                        },
                    },
                },
                // Xbox Group
                new SettingDefinition
                {
                    Id = "gaming-xbox-game-dvr",
                    Name = "Xbox Game DVR",
                    Description = "Record gameplay clips and take screenshots using the Xbox Game Bar overlay. Disabling reduces CPU/GPU usage and can improve frame rates",
                    GroupName = "Xbox",
                    Icon = "RecordRec",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\System\GameConfigStore",
                            ValueName = "GameDVR_Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, Game DVR is enabled
                            DisabledValue = 0, // When toggle is OFF, Game DVR is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\GameConfigStore",
                            ValueName = "AllowGameDVR",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, Xbox Game DVR is enabled
                            DisabledValue = 0, // When toggle is OFF, Xbox Game DVR is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-game-bar-controller",
                    Name = "Game Bar Controller Access",
                    Description = "Allow your Xbox/compatible controller to open Game Bar by pressing the Xbox button. Disable to prevent accidental Game Bar activation during gaming",
                    GroupName = "Xbox",
                    IconPack = "Fluent",
                    Icon = "XboxControllerError",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\GameBar",
                            ValueName = "UseNexusForGameBarEnabled",
                            RecommendedValue = 0,
                            EnabledValue = null, // When toggle is ON, controller access is enabled
                            DisabledValue = 0, // When toggle is OFF, controller access is disabled
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-game-bar-tips",
                    Name = "Game Bar Tips and Hints",
                    Description = "Show tips and hints about Game Bar features when opening the overlay. Disabling reduces distractions during gameplay",
                    GroupName = "Xbox",
                    Icon = "LightbulbOff",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\GameBar",
                            ValueName = "ShowStartupPanel",
                            RecommendedValue = 0,
                            EnabledValue = null,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // System Services Group
                new SettingDefinition
                {
                    Id = "gaming-performance-background-services",
                    Name = "Optimize Background Services",
                    Description = "Reduce the startup timeout for Windows services from 60 to 30 seconds. This can speed up boot time slightly",
                    GroupName = "System Services",
                    Icon = "Cog",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control",
                            ValueName = "ServicesPipeTimeout",
                            RecommendedValue = 60000,
                            EnabledValue = 30000, // When toggle is ON, services timeout is reduced (30 seconds)
                            DisabledValue = 60000, // When toggle is OFF, services timeout is default (60 seconds)
                            DefaultValue = 60000, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-sysmain-service",
                    Name = "SysMain Service (Superfetch)",
                    Description = "Preload frequently used applications into RAM for faster launch times. Automatic is recommended for HDDs, Manual or Disabled is preferred for SSDs",
                    GroupName = "System Services",
                    Icon = "Cached",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "Disabled (Recommended for SSD)",
                            "Manual",
                            "Automatic (Recommended for HDD)",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SysMain",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-prefetch",
                    Name = "Prefetch Feature",
                    Description = "Preload frequently used applications and boot files into memory to speed up launches. Generally recommended for HDDs not SSDs",
                    GroupName = "System Services",
                    Icon = "Download",
                    InputType = InputType.Toggle,
                    ParentSettingId = "gaming-sysmain-service",
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresEnabled,
                            DependentSettingId = "gaming-performance-prefetch",
                            RequiredSettingId = "gaming-sysmain-service",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters",
                            ValueName = "EnablePrefetcher",
                            RecommendedValue = 0,
                            EnabledValue = 3, // When toggle is ON, prefetch is enabled (3 = both application and boot prefetching)
                            DisabledValue = 0, // When toggle is OFF, prefetch is disabled
                            DefaultValue = 3, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-windows-search-service",
                    Name = "Windows Search Indexing Service",
                    Description = "Indexes files and folders for faster search results. Disabling reduces background CPU and disk activity but makes Windows Search slower",
                    GroupName = "System Services",
                    Icon = "DatabaseSearch",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WSearch",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-print-spooler-service",
                    Name = "Print Spooler Service",
                    Description = "Manages print jobs sent to printers. If you don't use a printer, set to Manual or Disabled to free up system resources",
                    GroupName = "System Services",
                    Icon = "Printer",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Spooler",
                            ValueName = "Start",
                            RecommendedValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-telemetry-service",
                    Name = "Connected User Experiences and Telemetry Service",
                    Description = "Sends usage data and diagnostics to Microsoft. Setting to Manual or Disabled reduces background network and CPU usage",
                    GroupName = "System Services",
                    Icon = "CloudUpload",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\DiagTrack",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-compatibility-assistant-service",
                    Name = "Program Compatibility Assistant Service",
                    Description = "Monitors programs for compatibility issues and suggests fixes. Disabling prevents compatibility prompts and saves minor system resources",
                    GroupName = "System Services",
                    Icon = "ApplicationCog",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\PcaSvc",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-error-reporting-service",
                    Name = "Windows Error Reporting Service",
                    Description = "Collects and sends crash data to Microsoft. Disabling prevents crash reporting, reduces network traffic, and improves privacy with minimal system impact",
                    GroupName = "System Services",
                    Icon = "AlertOctagon",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WerSvc",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-geolocation-service",
                    Name = "Geolocation Service",
                    Description = "Tracks your physical location for apps and services. Disabling improves privacy and prevents location tracking, but apps won't be able to use location features",
                    GroupName = "System Services",
                    Icon = "MapMarkerOff",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\lfsvc",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-retail-demo-service",
                    Name = "Retail Demo Service",
                    Description = "Controls device activity when in retail demo mode. Safe to disable for personal computers as it only serves retail display purposes",
                    GroupName = "System Services",
                    Icon = "StorefrontOutline",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\RetailDemo",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-insider-service",
                    Name = "Windows Insider Service",
                    Description = "Manages Windows Insider Program features and preview builds. Safe to disable if you're not participating in the Windows Insider Program",
                    GroupName = "System Services",
                    Icon = "TestTube",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\wisvc",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-phone-service",
                    Name = "Phone Service",
                    Description = "Manages telephony state on the device. Safe to disable if you don't use phone connectivity features or make calls from your PC",
                    GroupName = "System Services",
                    Icon = "Cellphone",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\PhoneSvc",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-wallet-service",
                    Name = "Wallet Service",
                    Description = "Provides wallet functionality for payment and NFC scenarios. Safe to disable if you don't use Microsoft Wallet features",
                    GroupName = "System Services",
                    Icon = "Wallet",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WalletService",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-smart-card-services",
                    Name = "Smart Card Services",
                    Description = "Enables smart card reader functionality for security authentication. Safe to disable if you don't use physical smart cards or card readers",
                    GroupName = "System Services",
                    Icon = "SmartCard",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SCardSvr",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\ScDeviceEnum",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SCPolicySvc",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-maps-broker-service",
                    Name = "Downloaded Maps Manager",
                    Description = "Provides access to downloaded maps for applications. Set to Manual to allow map access when needed while preventing unnecessary background activity",
                    GroupName = "System Services",
                    Icon = "MapOutline",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\MapsBroker",
                            ValueName = "Start",
                            RecommendedValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-fax-service",
                    Name = "Fax Service",
                    Description = "Enables sending and receiving faxes. Safe to disable for most users as fax functionality is rarely used on modern systems",
                    GroupName = "System Services",
                    Icon = "Fax",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_DisabledRecommended",
                            "ServiceOption_Manual",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Fax",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-wmp-network-service",
                    Name = "Windows Media Player Network Sharing Service",
                    Description = "Shares Windows Media Player libraries to other networked players and media devices. Safe to disable if you don't share media over your network",
                    GroupName = "System Services",
                    Icon = "ShareOff",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_DisabledRecommended",
                            "ServiceOption_Manual",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WMPNetworkSvc",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-mixed-reality-service",
                    Name = "Windows Mixed Reality OpenXR Service",
                    Description = "Runs OpenXR applications on Windows Mixed Reality devices. Safe to disable if you don't use VR or AR headsets",
                    GroupName = "System Services",
                    Icon = "VirtualReality",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\MixedRealityOpenXRSvc",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-mobile-hotspot-service",
                    Name = "Windows Mobile Hotspot Service",
                    Description = "Provides ability to share internet connection with other devices. Set to Manual to keep functionality available while preventing unnecessary background activity",
                    GroupName = "System Services",
                    Icon = "CellphoneWireless",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\icssvc",
                            ValueName = "Start",
                            RecommendedValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-sms-router-service",
                    Name = "Microsoft Windows SMS Router Service",
                    Description = "Routes SMS messages according to rules. Safe to disable if you don't use SMS features on your PC",
                    GroupName = "System Services",
                    Icon = "MessageText",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SmsRouter",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-parental-controls-service",
                    Name = "Parental Controls Service",
                    Description = "Enables parental controls and family safety features. Safe to disable if you don't use parental control features",
                    GroupName = "System Services",
                    Icon = "ShieldAccount",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WpcMonSvc",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-payments-nfc-service",
                    Name = "Payments and NFC/SE Manager",
                    Description = "Manages payments and Near Field Communication secure elements. Safe to disable if you don't use NFC payment features",
                    GroupName = "System Services",
                    Icon = "Nfc",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SEMgrSvc",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-spot-verifier-service",
                    Name = "Spot Verifier Service",
                    Description = "Verifies potential file system corruptions. Set to Manual to allow verification when needed while reducing background activity",
                    GroupName = "System Services",
                    Icon = "ShieldCheck",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\svsvc",
                            ValueName = "Start",
                            RecommendedValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-remote-access-manager",
                    Name = "Remote Access Connection Manager",
                    Description = "Manages VPN and dial-up connections. Set to Manual to reduce background activity while keeping VPN functionality available when needed.",
                    GroupName = "System Services",
                    Icon = "Vpn",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\RasMan",
                            ValueName = "Start",
                            RecommendedValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-remote-access-auto",
                    Name = "Remote Access Auto Connection Manager",
                    Description = "Automatically connects to remote networks when programs reference remote resources. Safe to disable if you don't use auto-connect VPN features",
                    GroupName = "System Services",
                    Icon = "NetworkOff",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\RasAuto",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-remote-desktop-services",
                    Name = "Remote Desktop Services",
                    Description = "Allows users to connect interactively to a remote computer. Set to Manual to reduce background activity while keeping Remote Desktop available.",
                    GroupName = "System Services",
                    Icon = "RemoteDesktop",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\TermService",
                            ValueName = "Start",
                            RecommendedValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-remote-desktop-configuration",
                    Name = "Remote Desktop Configuration",
                    Description = "Manages Remote Desktop Services and Remote Desktop related configurations. Set to Manual to reduce background activity while keeping Remote Desktop available",
                    GroupName = "System Services",
                    Icon = "MonitorShare",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SessionEnv",
                            ValueName = "Start",
                            RecommendedValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-remote-desktop-port-redirector",
                    Name = "Remote Desktop Services UserMode Port Redirector",
                    Description = "Allows local device redirection for Remote Desktop connections. Safe to disable if you don't need to share local devices during Remote Desktop sessions",
                    GroupName = "System Services",
                    Icon = "TransitConnectionVariant",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\UmRdpService",
                            ValueName = "Start",
                            RecommendedValue = 4,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-bits-service",
                    Name = "Background Intelligent Transfer Service (BITS)",
                    Description = "Transfers Windows Updates and downloads in the background. Setting to Manual allows updates to work while preventing constant background activity during gaming",
                    GroupName = "System Services",
                    Icon = "CloudDownload",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\BITS",
                            ValueName = "Start",
                            RecommendedValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-xbox-auth-manager",
                    Name = "Xbox Live Auth Manager",
                    Description = "Provides authentication and authorization services for Xbox Live. Safe to disable if you don't use Xbox Game Pass, Microsoft Store games, or Xbox features",
                    GroupName = "System Services",
                    Icon = "MicrosoftXbox",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                        [CustomPropertyKeys.OptionWarnings] = new Dictionary<int, string>
                        {
                            [0] = "Disabling will prevent Xbox Game Pass and Microsoft Store games from working",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\XblAuthManager",
                            ValueName = "Start",
                            RecommendedValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-xbox-game-save",
                    Name = "Xbox Live Game Save",
                    Description = "Syncs game saves to Xbox Live cloud. Only needed for Xbox Game Pass and Microsoft Store games with cloud save features",
                    GroupName = "System Services",
                    Icon = "CloudUploadOutline",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\XblGameSave",
                            ValueName = "Start",
                            RecommendedValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-xbox-networking",
                    Name = "Xbox Live Networking Service",
                    Description = "Supports Xbox Live multiplayer networking. Required for Xbox multiplayer gaming but not needed for Steam/Epic/other gaming platforms",
                    GroupName = "System Services",
                    Icon = "NetworkOutline",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\XboxNetApiSvc",
                            ValueName = "Start",
                            RecommendedValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-biometric-service",
                    Name = "Windows Biometric Service",
                    Description = "Enables fingerprint and facial recognition login via Windows Hello. Safe to disable on desktop systems without biometric hardware",
                    GroupName = "System Services",
                    Icon = "Fingerprint",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WbioSrvc",
                            ValueName = "Start",
                            RecommendedValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-touch-keyboard-service",
                    Name = "Touch Keyboard and Handwriting Panel Service",
                    Description = "Enables touch keyboard and pen input functionality. Safe to disable on desktop systems without touchscreen or stylus",
                    GroupName = "System Services",
                    Icon = "KeyboardOutline",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\TabletInputService",
                            ValueName = "Start",
                            RecommendedValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-sensor-monitoring-service",
                    Name = "Sensor Monitoring Service",
                    Description = "Monitors various sensors like ambient light and orientation. Safe to disable on desktop systems without sensor hardware",
                    GroupName = "System Services",
                    Icon = "Radar",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SensrSvc",
                            ValueName = "Start",
                            RecommendedValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-sensor-data-service",
                    Name = "Sensor Data Service",
                    Description = "Delivers data from a variety of sensors to applications. Safe to disable on desktop systems without sensor hardware",
                    GroupName = "System Services",
                    Icon = "ChartBox",
                    InputType = InputType.Selection,
                    RequiresRestart = true,
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "ServiceOption_Disabled",
                            "ServiceOption_ManualRecommended",
                            "ServiceOption_Automatic",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?> { ["Start"] = 4 },
                            [1] = new Dictionary<string, object?> { ["Start"] = 3 },
                            [2] = new Dictionary<string, object?> { ["Start"] = 2 },
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SensorDataService",
                            ValueName = "Start",
                            RecommendedValue = 3,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // Scheduled Tasks - Telemetry & Privacy
                new SettingDefinition
                {
                    Id = "gaming-task-compatibility-appraiser",
                    Name = "Microsoft Compatibility Appraiser Task",
                    Description = "Collects program compatibility telemetry for Windows upgrades. Works alongside the Connected User Experiences and Telemetry Service. Disable to reduce telemetry and background system activity",
                    GroupName = "Scheduled Tasks",
                    Icon = "FileDocumentCheck",
                    InputType = InputType.Toggle,
                    ScheduledTaskSettings = new List<ScheduledTaskSetting>
                    {
                        new ScheduledTaskSetting
                        {
                            Id = "CompatibilityAppraiserTask",
                            TaskPath = @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
                            RecommendedState = false
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "gaming-task-program-data-updater",
                    Name = "Program Data Updater Task",
                    Description = "Updates the program compatibility database with information about installed applications. Disable to reduce telemetry collection",
                    GroupName = "Scheduled Tasks",
                    Icon = "DatabaseSync",
                    InputType = InputType.Toggle,
                    ScheduledTaskSettings = new List<ScheduledTaskSetting>
                    {
                        new ScheduledTaskSetting
                        {
                            Id = "ProgramDataUpdaterTask",
                            TaskPath = @"\Microsoft\Windows\Application Experience\ProgramDataUpdater",
                            RecommendedState = false
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "gaming-task-ceip-consolidator",
                    Name = "Customer Experience Improvement Program Consolidator",
                    Description = "Consolidates and uploads usage data as part of the Customer Experience Improvement Program. Works with the Connected User Experiences and Telemetry Service. Disable to improve privacy",
                    GroupName = "Scheduled Tasks",
                    Icon = "ChartLine",
                    InputType = InputType.Toggle,
                    ScheduledTaskSettings = new List<ScheduledTaskSetting>
                    {
                        new ScheduledTaskSetting
                        {
                            Id = "CEIPConsolidatorTask",
                            TaskPath = @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
                            RecommendedState = false
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "gaming-task-usb-ceip",
                    Name = "USB CEIP Task",
                    Description = "Collects USB device-related telemetry for the Customer Experience Improvement Program. Disable to reduce telemetry",
                    GroupName = "Scheduled Tasks",
                    Icon = "Usb",
                    InputType = InputType.Toggle,
                    ScheduledTaskSettings = new List<ScheduledTaskSetting>
                    {
                        new ScheduledTaskSetting
                        {
                            Id = "UsbCeipTask",
                            TaskPath = @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
                            RecommendedState = false
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "gaming-task-disk-diagnostic",
                    Name = "Disk Diagnostic Data Collector Task",
                    Description = "Collects disk diagnostic information and S.M.A.R.T. data for Microsoft. Disable to reduce background disk activity and telemetry",
                    GroupName = "Scheduled Tasks",
                    Icon = "Harddisk",
                    InputType = InputType.Toggle,
                    ScheduledTaskSettings = new List<ScheduledTaskSetting>
                    {
                        new ScheduledTaskSetting
                        {
                            Id = "DiskDiagnosticTask",
                            TaskPath = @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector",
                            RecommendedState = false
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "gaming-task-feedback-dmclient",
                    Name = "Feedback DmClient Task",
                    Description = "Collects feedback and diagnostic data for Microsoft. Disable to improve privacy and reduce telemetry",
                    GroupName = "Scheduled Tasks",
                    Icon = "MessageAlert",
                    InputType = InputType.Toggle,
                    ScheduledTaskSettings = new List<ScheduledTaskSetting>
                    {
                        new ScheduledTaskSetting
                        {
                            Id = "FeedbackDmClientTask",
                            TaskPath = @"\Microsoft\Windows\Feedback\Siuf\DmClient",
                            RecommendedState = false
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "gaming-task-feedback-dmclient-download",
                    Name = "Feedback DmClient Scenario Download Task",
                    Description = "Downloads feedback scenarios and configuration data from Microsoft. Disable to reduce telemetry and network activity",
                    GroupName = "Scheduled Tasks",
                    Icon = "Download",
                    InputType = InputType.Toggle,
                    ScheduledTaskSettings = new List<ScheduledTaskSetting>
                    {
                        new ScheduledTaskSetting
                        {
                            Id = "FeedbackDmClientDownloadTask",
                            TaskPath = @"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload",
                            RecommendedState = false
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "gaming-task-error-reporting-queue",
                    Name = "Windows Error Reporting Queue Task",
                    Description = "Queues crash reports and error data to send to Microsoft. Works alongside the Windows Error Reporting Service. Disable both to prevent crash data collection",
                    GroupName = "Scheduled Tasks",
                    Icon = "AlertOctagon",
                    InputType = InputType.Toggle,
                    ScheduledTaskSettings = new List<ScheduledTaskSetting>
                    {
                        new ScheduledTaskSetting
                        {
                            Id = "ErrorReportingQueueTask",
                            TaskPath = @"\Microsoft\Windows\Windows Error Reporting\QueueReporting",
                            RecommendedState = false
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "gaming-task-sqm",
                    Name = "Software Quality Metrics Task",
                    Description = "Collects software quality metrics and reliability data for Microsoft telemetry. Disable to improve privacy",
                    GroupName = "Scheduled Tasks",
                    Icon = "ChartBar",
                    InputType = InputType.Toggle,
                    ScheduledTaskSettings = new List<ScheduledTaskSetting>
                    {
                        new ScheduledTaskSetting
                        {
                            Id = "SqmTask",
                            TaskPath = @"\Microsoft\Windows\PI\Sqm-Tasks",
                            RecommendedState = false
                        }
                    }
                },
                // Scheduled Tasks - Application Experience
                new SettingDefinition
                {
                    Id = "gaming-task-mare-backup",
                    Name = "MAR (Malicious Software Removal) Backup Task",
                    Description = "Backs up Microsoft Assisted Recovery data. Disable to reduce background system activity",
                    GroupName = "Scheduled Tasks",
                    Icon = "BackupRestore",
                    InputType = InputType.Toggle,
                    ScheduledTaskSettings = new List<ScheduledTaskSetting>
                    {
                        new ScheduledTaskSetting
                        {
                            Id = "MareBackupTask",
                            TaskPath = @"\Microsoft\Windows\Application Experience\MareBackup",
                            RecommendedState = false
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "gaming-task-startup-app",
                    Name = "Startup App Task",
                    Description = "Tracks and monitors startup applications for telemetry and diagnostics. Disable to reduce telemetry",
                    GroupName = "Scheduled Tasks",
                    Icon = "RocketLaunch",
                    InputType = InputType.Toggle,
                    ScheduledTaskSettings = new List<ScheduledTaskSetting>
                    {
                        new ScheduledTaskSetting
                        {
                            Id = "StartupAppTask",
                            TaskPath = @"\Microsoft\Windows\Application Experience\StartupAppTask",
                            RecommendedState = false
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "gaming-task-pca-patch",
                    Name = "Program Compatibility Assistant Patch Task",
                    Description = "Updates the Program Compatibility Assistant database. Works with the Program Compatibility Assistant Service. Disable both to prevent compatibility checking",
                    GroupName = "Scheduled Tasks",
                    Icon = "Update",
                    InputType = InputType.Toggle,
                    ScheduledTaskSettings = new List<ScheduledTaskSetting>
                    {
                        new ScheduledTaskSetting
                        {
                            Id = "PcaPatchTask",
                            TaskPath = @"\Microsoft\Windows\Application Experience\PcaPatchDbTask",
                            RecommendedState = false
                        }
                    }
                },
                // Scheduled Tasks - Optional
                new SettingDefinition
                {
                    Id = "gaming-task-maps-update",
                    Name = "Maps Update Task",
                    Description = "Updates offline maps data for the Windows Maps app. Disable if you don't use the Maps app to save bandwidth and storage",
                    GroupName = "Scheduled Tasks",
                    Icon = "MapOutline",
                    InputType = InputType.Toggle,
                    ScheduledTaskSettings = new List<ScheduledTaskSetting>
                    {
                        new ScheduledTaskSetting
                        {
                            Id = "MapsUpdateTask",
                            TaskPath = @"\Microsoft\Windows\Maps\MapsUpdateTask",
                            RecommendedState = false
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "gaming-task-autochk-proxy",
                    Name = "AutoChk Proxy Task",
                    Description = "Performs disk checking operations and collects diagnostic data. Consider keeping enabled for disk health monitoring",
                    GroupName = "Scheduled Tasks",
                    Icon = "HarddiskPlus",
                    InputType = InputType.Toggle,
                    ScheduledTaskSettings = new List<ScheduledTaskSetting>
                    {
                        new ScheduledTaskSetting
                        {
                            Id = "AutochkProxyTask",
                            TaskPath = @"\Microsoft\Windows\Autochk\Proxy",
                            RecommendedState = null
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "gaming-task-family-safety",
                    Name = "Family Safety Monitor Task",
                    Description = "Monitors family safety settings and usage. Disable if you don't use family safety features",
                    GroupName = "Scheduled Tasks",
                    Icon = "AccountSupervisor",
                    InputType = InputType.Toggle,
                    ScheduledTaskSettings = new List<ScheduledTaskSetting>
                    {
                        new ScheduledTaskSetting
                        {
                            Id = "FamilySafetyTask",
                            TaskPath = @"\Microsoft\Windows\Shell\FamilySafetyMonitor",
                            RecommendedState = false
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "gaming-task-power-efficiency",
                    Name = "Power Efficiency Diagnostics Task",
                    Description = "Analyzes system power consumption and collects energy efficiency data. Disable to reduce telemetry and background analysis",
                    GroupName = "Scheduled Tasks",
                    Icon = "LightningBolt",
                    InputType = InputType.Toggle,
                    ScheduledTaskSettings = new List<ScheduledTaskSetting>
                    {
                        new ScheduledTaskSetting
                        {
                            Id = "PowerEfficiencyTask",
                            TaskPath = @"\Microsoft\Windows\Power Efficiency Diagnostics\AnalyzeSystem",
                            RecommendedState = false
                        }
                    }
                },
                // Visual Effects Group
                new SettingDefinition
                {
                    Id = "visual-effects-mode",
                    Name = "Visual Effects",
                    Description = "Choose how Windows displays visual effects",
                    GroupName = "Visual Effects",
                    InputType = InputType.Selection,
                    Icon = "MonitorEye",
                    RequiresRestart = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
                            ValueName = "VisualFXSetting",
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0,
                            IsPrimary = true,
                        }
                    },
                    CustomProperties = new Dictionary<string, object>
                    {
                        [CustomPropertyKeys.ComboBoxDisplayNames] = new string[]
                        {
                            "Let Windows choose what's best for my computer",
                            "Adjust for best appearance",
                            "Adjust for best performance",
                            "Custom",
                        },
                        [CustomPropertyKeys.ValueMappings] = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?>
                            {
                                ["VisualFXSetting"] = 0,
                            },
                            [1] = new Dictionary<string, object?>
                            {
                                ["VisualFXSetting"] = 1,
                            },
                            [2] = new Dictionary<string, object?>
                            {
                                ["VisualFXSetting"] = 2,
                            },
                            [3] = new Dictionary<string, object?>
                            {
                                ["VisualFXSetting"] = 3,
                            },
                        },
                        [CustomPropertyKeys.SettingPresets] = new Dictionary<int, Dictionary<string, bool>>
                        {
                            [0] = new Dictionary<string, bool> // Let Windows Decide (Windows changes this preset based on hardware and just setting VisualFXSetting to 0 does not automatically apply the preset) 
                                                               // For this reason, Winhance applies a "Balanced" preset that actually applies the child settings to the system
                                                               // Note: The Visual Effects GUI in Windows will not be accurate after selecting this option in Winhance
                                                               // if you truly want to let Windows decide, toggle the setting in Windows.
                            {
                                ["ui-effects"] = false,
                                ["window-animation"] = false,
                                ["taskbar-animations"] = false,
                                ["enable-peek"] = true,
                                ["menu-animation"] = false,
                                ["fade-tooltip"] = false,
                                ["fade-menu-items"] = false,
                                ["taskbar-thumbnails"] = true,
                                ["mouse-shadow"] = false,
                                ["window-shadows"] = false,
                                ["show-thumbnails"] = true,
                                ["translucent-selection"] = true,
                                ["drag-full-windows"] = true,
                                ["combo-box-animation"] = false,
                                ["font-smoothing"] = true,
                                ["smooth-scroll-listboxes"] = true,
                                ["drop-shadows"] = false,
                            },
                            [1] = new Dictionary<string, bool> // Best Appearance
                            {
                                ["ui-effects"] = true,
                                ["window-animation"] = true,
                                ["taskbar-animations"] = true,
                                ["enable-peek"] = true,
                                ["menu-animation"] = true,
                                ["fade-tooltip"] = true,
                                ["fade-menu-items"] = true,
                                ["taskbar-thumbnails"] = true,
                                ["mouse-shadow"] = true,
                                ["window-shadows"] = true,
                                ["show-thumbnails"] = true,
                                ["translucent-selection"] = true,
                                ["drag-full-windows"] = true,
                                ["combo-box-animation"] = true,
                                ["font-smoothing"] = true,
                                ["smooth-scroll-listboxes"] = true,
                                ["drop-shadows"] = true,
                            },
                            [2] = new Dictionary<string, bool> // Best Performance
                            {
                                ["ui-effects"] = false,
                                ["window-animation"] = false,
                                ["taskbar-animations"] = false,
                                ["enable-peek"] = false,
                                ["menu-animation"] = false,
                                ["fade-tooltip"] = false,
                                ["fade-menu-items"] = false,
                                ["taskbar-thumbnails"] = false,
                                ["mouse-shadow"] = false,
                                ["window-shadows"] = false,
                                ["show-thumbnails"] = false,
                                ["translucent-selection"] = false,
                                ["drag-full-windows"] = false,
                                ["combo-box-animation"] = false,
                                ["font-smoothing"] = false,
                                ["smooth-scroll-listboxes"] = false,
                                ["drop-shadows"] = false,
                            },
                            // No preset for Custom, since it's, you know.... Custom...
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "ui-effects",
                    Name = "Animate controls and elements inside windows",
                    Description = "Enables animation effects for controls and UI elements",
                    GroupName = "Visual Effects",
                    InputType = InputType.Toggle,
                    Icon = "Animation",
                    RequiresRestart = true,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "ui-effects",
                            RequiredSettingId = "visual-effects-mode",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "UserPreferencesMask",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.Binary,
                            BinaryByteIndex = 4,
                            BitMask = 0x02,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "window-animation",
                    Name = "Animate windows when minimizing and maximizing",
                    Description = "Shows smooth animation when windows are minimized or maximized",
                    GroupName = "Visual Effects",
                    InputType = InputType.Toggle,
                    Icon = "WindowRestore",
                    RequiresRestart = true,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "window-animation",
                            RequiredSettingId = "visual-effects-mode",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics",
                            ValueName = "MinAnimate",
                            RecommendedValue = "0",
                            EnabledValue = "1",
                            DisabledValue = "0",
                            DefaultValue = "1",
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-animations",
                    Name = "Animations in the taskbar",
                    Description = "Controls taskbar animation effects for opening, closing, and switching windows",
                    GroupName = "Visual Effects",
                    InputType = InputType.Toggle,
                    Icon = "DockBottom",
                    RequiresRestart = true,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "taskbar-animations",
                            RequiredSettingId = "visual-effects-mode",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarAnimations",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "enable-peek",
                    Name = "Enable Peek",
                    Description = "Allows peeking at desktop when hovering over Show Desktop button",
                    GroupName = "Visual Effects",
                    InputType = InputType.Toggle,
                    Icon = "MonitorEye",
                    RequiresRestart = true,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "enable-peek",
                            RequiredSettingId = "visual-effects-mode",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM",
                            ValueName = "EnableAeroPeek",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "menu-animation",
                    Name = "Fade or slide menus into view",
                    Description = "Animates menus when they appear using fade or slide effects",
                    GroupName = "Visual Effects",
                    InputType = InputType.Toggle,
                    Icon = "MenuOpen",
                    RequiresRestart = true,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "menu-animation",
                            RequiredSettingId = "visual-effects-mode",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "UserPreferencesMask",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.Binary,
                            BinaryByteIndex = 0,
                            BitMask = 0x02,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "fade-tooltip",
                    Name = "Fade or slide ToolTips into view",
                    Description = "Animates tooltips when they appear using fade or slide effects",
                    GroupName = "Visual Effects",
                    InputType = InputType.Toggle,
                    Icon = "TooltipText",
                    RequiresRestart = true,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "fade-tooltip",
                            RequiredSettingId = "visual-effects-mode",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "UserPreferencesMask",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.Binary,
                            BinaryByteIndex = 1,
                            BitMask = 0x08,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "fade-menu-items",
                    Name = "Fade out menu items after clicking",
                    Description = "Fades menu items after selection before closing the menu",
                    GroupName = "Visual Effects",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "SlideTextCursor",
                    RequiresRestart = true,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "fade-menu-items",
                            RequiredSettingId = "visual-effects-mode",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "UserPreferencesMask",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.Binary,
                            BinaryByteIndex = 1,
                            BitMask = 0x04,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "taskbar-thumbnails",
                    Name = "Save taskbar thumbnail previews",
                    Description = "Saves thumbnail previews of taskbar windows for faster display",
                    GroupName = "Visual Effects",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "ImageMultiple",
                    RequiresRestart = true,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "taskbar-thumbnails",
                            RequiredSettingId = "visual-effects-mode",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM",
                            ValueName = "AlwaysHibernateThumbnails",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "mouse-shadow",
                    Name = "Show shadows under mouse pointer",
                    Description = "Displays shadow effect underneath the mouse cursor",
                    GroupName = "Visual Effects",
                    InputType = InputType.Toggle,
                    Icon = "CursorDefault",
                    RequiresRestart = true,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "mouse-shadow",
                            RequiredSettingId = "visual-effects-mode",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "UserPreferencesMask",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.Binary,
                            BinaryByteIndex = 1,
                            BitMask = 0x20,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "window-shadows",
                    Name = "Show shadows under windows",
                    Description = "Displays shadow effects underneath windows",
                    GroupName = "Visual Effects",
                    InputType = InputType.Toggle,
                    Icon = "BoxShadow",
                    RequiresRestart = true,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "window-shadows",
                            RequiredSettingId = "visual-effects-mode",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "UserPreferencesMask",
                            RecommendedValue = 1,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.Binary,
                            BinaryByteIndex = 2,
                            BitMask = 0x04,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "show-thumbnails",
                    Name = "Show thumbnails instead of icons",
                    Description = "Displays image and document previews instead of generic file icons",
                    GroupName = "Visual Effects",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "ImageStack",
                    RequiresRestart = true,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "show-thumbnails",
                            RequiredSettingId = "visual-effects-mode",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "IconsOnly",
                            RecommendedValue = 0,
                            EnabledValue = 0,
                            DisabledValue = 1,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "translucent-selection",
                    Name = "Show translucent selection rectangle",
                    Description = "Display a semi-transparent selection box when dragging to select multiple files or items",
                    GroupName = "Visual Effects",
                    InputType = InputType.Toggle,
                    Icon = "Select",
                    RequiresRestart = true,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "translucent-selection",
                            RequiredSettingId = "visual-effects-mode",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ListviewAlphaSelect",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "drag-full-windows",
                    Name = "Show window contents while dragging",
                    Description = "Displays window contents when dragging instead of just an outline",
                    GroupName = "Visual Effects",
                    InputType = InputType.Toggle,
                    Icon = "SelectionDrag",
                    RequiresRestart = true,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "drag-full-windows",
                            RequiredSettingId = "visual-effects-mode",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "DragFullWindows",
                            RecommendedValue = "0",
                            EnabledValue = "1",
                            DisabledValue = "0",
                            DefaultValue = "1",
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "combo-box-animation",
                    Name = "Slide open combo boxes",
                    Description = "Animates combo boxes when they open with a sliding effect",
                    GroupName = "Visual Effects",
                    InputType = InputType.Toggle,
                    Icon = "FormDropdown",
                    RequiresRestart = true,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "combo-box-animation",
                            RequiredSettingId = "visual-effects-mode",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "UserPreferencesMask",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.Binary,
                            BinaryByteIndex = 0,
                            BitMask = 0x04,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "font-smoothing",
                    Name = "Smooth edges of screen fonts",
                    Description = "Apply anti-aliasing to text for smoother, more readable fonts on screen",
                    GroupName = "Visual Effects",
                    InputType = InputType.Toggle,
                    Icon = "FormatSize",
                    RequiresRestart = true,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "font-smoothing",
                            RequiredSettingId = "visual-effects-mode",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "FontSmoothing",
                            RecommendedValue = "2",
                            EnabledValue = "2",
                            DisabledValue = "0",
                            DefaultValue = "0",
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "smooth-scroll-listboxes",
                    Name = "Smooth-scroll list boxes",
                    Description = "Enables smooth scrolling in list boxes instead of jumping",
                    GroupName = "Visual Effects",
                    InputType = InputType.Toggle,
                    Icon = "ListBox",
                    RequiresRestart = true,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "smooth-scroll-listboxes",
                            RequiredSettingId = "visual-effects-mode",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "UserPreferencesMask",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.Binary,
                            BinaryByteIndex = 0,
                            BitMask = 0x08,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "drop-shadows",
                    Name = "Use drop shadows for icon labels on the desktop",
                    Description = "Add shadow effects behind desktop icon text to improve readability against backgrounds",
                    GroupName = "Visual Effects",
                    InputType = InputType.Toggle,
                    Icon = "TextShadow",
                    RequiresRestart = true,
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresValueBeforeAnyChange,
                            DependentSettingId = "drop-shadows",
                            RequiredSettingId = "visual-effects-mode",
                            RequiredValue = "Custom",
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ListviewShadow",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                // Accessibility Group
                new SettingDefinition
                {
                    Id = "gaming-narrator-hotkey",
                    Name = "Narrator Win+Ctrl+Enter Hotkey",
                    Description = "Enable the Win+Ctrl+Enter keyboard shortcut to quickly launch Windows Narrator screen reader",
                    GroupName = "Accessibility",
                    Icon = "AccountVoice",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Narrator\NoRoam",
                            ValueName = "WinEnterLaunchEnabled",
                            RecommendedValue = 0,
                            EnabledValue = null,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "accessibility-stickykeys-hotkey",
                    Name = "StickyKeys Hotkey (Shift×5)",
                    Description = "Enable the keyboard shortcut to activate StickyKeys by pressing the Shift key five times",
                    GroupName = "Accessibility",
                    Icon = "AppleKeyboardShift",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Accessibility\StickyKeys",
                            ValueName = "Flags",
                            RecommendedValue = "2",
                            EnabledValue = "510",
                            DisabledValue = "2",
                            DefaultValue = "510",
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "accessibility-filterkeys-hotkey",
                    Name = "FilterKeys Hotkey (Right Shift 8s)",
                    Description = "Enable the keyboard shortcut to activate FilterKeys by holding the right Shift key for 8 seconds",
                    GroupName = "Accessibility",
                    Icon = "KeyboardOutline",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Accessibility\Keyboard Response",
                            ValueName = "Flags",
                            RecommendedValue = "2",
                            EnabledValue = "126",
                            DisabledValue = "2",
                            DefaultValue = "126",
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "accessibility-togglekeys-hotkey",
                    Name = "ToggleKeys Hotkey (Num Lock 5s)",
                    Description = "Enable the keyboard shortcut to activate ToggleKeys by holding Num Lock for 5 seconds, which plays sounds when Caps/Num/Scroll Lock are pressed",
                    GroupName = "Accessibility",
                    Icon = "Numeric",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Accessibility\ToggleKeys",
                            ValueName = "Flags",
                            RecommendedValue = "34",
                            EnabledValue = "62",
                            DisabledValue = "34",
                            DefaultValue = "62",
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "accessibility-mousekeys-hotkey",
                    Name = "MouseKeys Hotkey (Alt+Shift+NumLock)",
                    Description = "Enable the keyboard shortcut to activate MouseKeys, which allows using the numeric keypad to control the mouse pointer",
                    GroupName = "Accessibility",
                    Icon = "MouseVariant",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Accessibility\MouseKeys",
                            ValueName = "Flags",
                            RecommendedValue = "130",
                            EnabledValue = "126",
                            DisabledValue = "130",
                            DefaultValue = "126",
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "accessibility-highcontrast-hotkey",
                    Name = "High Contrast Hotkey (Alt+Shift+PrtScn)",
                    Description = "Enable the keyboard shortcut to activate High Contrast mode by pressing Left Alt + Left Shift + Print Screen",
                    GroupName = "Accessibility",
                    Icon = "ContrastCircle",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Accessibility\HighContrast",
                            ValueName = "Flags",
                            RecommendedValue = "4194",
                            EnabledValue = "126",
                            DisabledValue = "4194",
                            DefaultValue = "126",
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
            },
        };
    }
}
