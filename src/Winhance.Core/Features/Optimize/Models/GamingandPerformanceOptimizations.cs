using System.Collections.Generic;
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
                    Id = "gaming-xbox-game-dvr",
                    Name = "Xbox Game DVR",
                    Description = "Controls Xbox Game DVR functionality",
                    GroupName = "Game Recording",
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
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\GameConfigStore",
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
                    Description = "Allow your controller to open Game Bar",
                    GroupName = "Game Bar",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\GameBar",
                            ValueName = "UseNexusForGameBarEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, controller access is enabled
                            DisabledValue = 0, // When toggle is OFF, controller access is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-game-mode",
                    Name = "Game Mode",
                    Description = "Controls Game Mode for optimized gaming performance",
                    GroupName = "Game Mode",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\GameBar",
                            ValueName = "AutoGameModeEnabled",
                            RecommendedValue = 1,
                            EnabledValue = 1, // When toggle is ON, Game Mode is enabled
                            DisabledValue = 0, // When toggle is OFF, Game Mode is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-directx-optimizations",
                    Name = "DirectX Optimizations",
                    Description = "Changes DirectX settings for optimal gaming performance",
                    GroupName = "DirectX",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences",
                            ValueName = "DirectXUserGlobalSettings",
                            RecommendedValue = "SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;",
                            EnabledValue = "SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;", // When toggle is ON, optimizations are enabled
                            DisabledValue = "", // When toggle is OFF, use default settings
                            DefaultValue = "SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;", // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-nvidia-sharpening",
                    Name = "Old Nvidia Sharpening",
                    Description = "Controls Nvidia sharpening for image quality",
                    GroupName = "Nvidia",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\NVIDIA Corporation\Global\FTS",
                            ValueName = "EnableGR535",
                            RecommendedValue = 0,
                            EnabledValue = 0, // When toggle is ON, old Nvidia sharpening is enabled (0 = enabled for this setting)
                            DisabledValue = 1, // When toggle is OFF, old Nvidia sharpening is disabled (1 = disabled for this setting)
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-high-precision-event-timer",
                    Name = "High Precision Event Timer",
                    Description =
                        "Controls the High Precision Event Timer (HPET) for improved system performance",
                    GroupName = "System Performance",
                    InputType = InputType.Toggle,
                    CommandSettings = new List<CommandSetting>
                    {
                        new CommandSetting
                        {
                            Id = "hpet-platform-clock",
                            EnabledCommand = "bcdedit /set useplatformclock true",
                            DisabledCommand = "bcdedit /deletevalue useplatformclock",
                            RequiresElevation = true,
                        },
                        new CommandSetting
                        {
                            Id = "hpet-dynamic-tick",
                            EnabledCommand = "bcdedit /set disabledynamictick no",
                            DisabledCommand = "bcdedit /set disabledynamictick yes",
                            RequiresElevation = true,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-system-responsiveness",
                    Name = "System Responsiveness for Games",
                    Description = "Controls system responsiveness for multimedia applications",
                    GroupName = "System Performance",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                            ValueName = "SystemResponsiveness",
                            RecommendedValue = 0,
                            EnabledValue = 0, // When toggle is ON, system responsiveness is optimized for games (0 = prioritize foreground)
                            DisabledValue = 10, // When toggle is OFF, system responsiveness is balanced (10 = default Windows value)
                            DefaultValue = 10, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-network-throttling",
                    Name = "Network Throttling for Gaming",
                    Description = "Controls network throttling for optimal gaming performance",
                    GroupName = "System Performance",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile",
                            ValueName = "NetworkThrottlingIndex",
                            RecommendedValue = 10,
                            EnabledValue = 10, // When toggle is ON, network throttling is disabled (10 = disabled)
                            DisabledValue = 5, // When toggle is OFF, network throttling is enabled (default Windows value)
                            DefaultValue = 5, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-gpu-priority",
                    Name = "GPU Priority for Gaming",
                    Description = "Controls GPU priority for gaming performance",
                    GroupName = "System Performance",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
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
                    Id = "gaming-cpu-priority",
                    Name = "CPU Priority for Gaming",
                    Description = "Controls CPU priority for gaming performance",
                    GroupName = "System Performance",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games",
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
                    Description = "Controls scheduling category for games",
                    GroupName = "System Performance",
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
                new SettingDefinition
                {
                    Id = "gaming-gpu-scheduling",
                    Name = "Hardware-Accelerated GPU Scheduling",
                    Description = "Controls hardware-accelerated GPU scheduling",
                    GroupName = "System Performance",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\GraphicsDrivers",
                            ValueName = "HwSchMode",
                            RecommendedValue = 2,
                            EnabledValue = 2, // When toggle is ON, hardware-accelerated GPU scheduling is enabled
                            DisabledValue = 1, // When toggle is OFF, hardware-accelerated GPU scheduling is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-win32-priority",
                    Name = "Win32 Priority Separation",
                    Description = "Controls Win32 priority separation for program performance",
                    GroupName = "System Performance",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\PriorityControl",
                            ValueName = "Win32PrioritySeparation",
                            RecommendedValue = 38,
                            EnabledValue = 38, // When toggle is ON, priority is set for best performance of programs
                            DisabledValue = 2, // When toggle is OFF, priority is set to default Windows value
                            DefaultValue = 2, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-storage-sense",
                    Name = "Storage Sense",
                    Description = "Controls Storage Sense functionality",
                    GroupName = "System Performance",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\StorageSense",
                            ValueName = "AllowStorageSenseGlobal",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, Storage Sense is enabled
                            DisabledValue = 0, // When toggle is OFF, Storage Sense is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-animations",
                    Name = "UI Animations",
                    Description = "Controls UI animations for improved performance",
                    GroupName = "Visual Effects",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics",
                            ValueName = "MinAnimate",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, animations are enabled
                            DisabledValue = 0, // When toggle is OFF, animations are disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-autostart-delay",
                    Name = "Startup Delay for Apps",
                    Description = "Controls startup delay for applications",
                    GroupName = "Startup",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\\Serialize",
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
                    Id = "gaming-performance-background-services",
                    Name = "Optimize Background Services",
                    Description = "Controls background services for better performance",
                    GroupName = "System Services",
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
                    Id = "gaming-performance-desktop-composition",
                    Name = "Desktop Composition Effects",
                    Description = "Controls desktop composition effects",
                    GroupName = "Visual Effects",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM",
                            ValueName = "CompositionPolicy",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, desktop composition is enabled
                            DisabledValue = 0, // When toggle is OFF, desktop composition is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-fast-startup",
                    Name = "Fast Startup",
                    Description = "Controls fast startup feature",
                    GroupName = "Startup",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Power",
                            ValueName = "HiberbootEnabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, fast startup is enabled
                            DisabledValue = 0, // When toggle is OFF, fast startup is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-explorer-search",
                    Name = "Optimize File Explorer Search",
                    Description = "Controls file explorer search indexing",
                    GroupName = "File Explorer",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Search\Preferences",
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
                    Id = "gaming-performance-prefetch",
                    Name = "Prefetch Feature",
                    Description = "Controls Windows prefetch feature",
                    GroupName = "System Performance",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters",
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
                    Id = "gaming-performance-remote-assistance",
                    Name = "Remote Assistance",
                    Description = "Controls remote assistance feature",
                    GroupName = "System Services",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Remote Assistance",
                            ValueName = "fAllowToGetHelp",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, remote assistance is enabled
                            DisabledValue = 0, // When toggle is OFF, remote assistance is disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-superfetch",
                    Name = "Superfetch Service",
                    Description = "Controls superfetch/SysMain service",
                    GroupName = "System Performance",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters",
                            ValueName = "EnableSuperfetch",
                            RecommendedValue = 0,
                            EnabledValue = 3, // When toggle is ON, superfetch is enabled (3 = full functionality)
                            DisabledValue = 0, // When toggle is OFF, superfetch is disabled
                            DefaultValue = 3, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-visual-effects",
                    Name = "Optimize Visual Effects",
                    Description = "Controls visual effects for best performance",
                    GroupName = "Visual Effects",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
                            ValueName = "VisualFXSetting",
                            RecommendedValue = 2,
                            EnabledValue = 1, // When toggle is ON, visual effects are set to "best appearance" (1)
                            DisabledValue = 2, // When toggle is OFF, visual effects are set to "best performance" (2)
                            DefaultValue = 2, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-explorer-mouse-precision",
                    Name = "Enhance Pointer Precision",
                    Description = "Controls enhanced pointer precision (mouse acceleration)",
                    GroupName = "Mouse Settings",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Mouse",
                            ValueName = "MouseSpeed",
                            RecommendedValue = "0",
                            EnabledValue = "1", // When toggle is ON, enhanced pointer precision is enabled
                            DisabledValue = "0", // When toggle is OFF, enhanced pointer precision is disabled
                            DefaultValue = "1", // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-explorer-animations",
                    Name = "System Animations",
                    Description = "Controls animations and visual effects",
                    GroupName = "File Explorer Settings",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Control Panel\Desktop",
                            ValueName = "UserPreferencesMask",
                            RecommendedValue = new byte[]
                            {
                                0x90,
                                0x12,
                                0x03,
                                0x80,
                                0x10,
                                0x00,
                                0x00,
                                0x00,
                            },
                            EnabledValue = new byte[]
                            {
                                0x9E,
                                0x3E,
                                0x07,
                                0x80,
                                0x12,
                                0x00,
                                0x00,
                                0x00,
                            }, // When toggle is ON, animations are enabled
                            DisabledValue = new byte[]
                            {
                                0x90,
                                0x12,
                                0x03,
                                0x80,
                                0x10,
                                0x00,
                                0x00,
                                0x00,
                            }, // When toggle is OFF, animations are disabled
                            DefaultValue = new byte[]
                            {
                                0x90,
                                0x12,
                                0x03,
                                0x80,
                                0x10,
                                0x00,
                                0x00,
                                0x00,
                            },
                            ValueType = RegistryValueKind.Binary,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-explorer-menu-show-delay",
                    Name = "Menu Show Delay",
                    Description = "Controls menu show delay",
                    GroupName = "File Explorer Settings",
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
                    Id = "gaming-performance-explorer-set-visual-effects",
                    Name = "Set Visual Effects",
                    Description = "Sets appearance options to custom",
                    GroupName = "File Explorer Settings",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
                            ValueName = "VisualFXSetting",
                            RecommendedValue = 3,
                            EnabledValue = 3,
                            DisabledValue = 0,
                            DefaultValue = 3, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-performance-explorer-taskbar-animations",
                    Name = "Taskbar Animations",
                    Description = "Controls taskbar animations",
                    GroupName = "File Explorer Settings",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TaskbarAnimations",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, taskbar animations are enabled
                            DisabledValue = 0, // When toggle is OFF, taskbar animations are disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-background-apps",
                    Name = "Let Apps Run in Background",
                    Description = "Controls whether apps can run in the background",
                    GroupName = "Background Apps",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy",
                            ValueName = "LetAppsRunInBackground",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, background apps are enabled
                            DisabledValue = 0, // When toggle is OFF, background apps are disabled
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-power-cpu-unpark",
                    Name = "CPU Core Unparking",
                    Description = "Controls CPU core parking for better performance",
                    GroupName = "Performance",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SYSTEM\ControlSet001\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583",
                            ValueName = "ValueMax",
                            RecommendedValue = 0,
                            EnabledValue = 0, // Unpark CPU cores (from Recommended)
                            DisabledValue = 1, // Allow CPU core parking (from Default)
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-power-throttling",
                    Name = "Power Throttling",
                    Description = "Controls power throttling for better performance",
                    GroupName = "Performance",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling",
                            ValueName = "PowerThrottlingOff",
                            RecommendedValue = 1,
                            EnabledValue = 0, // Enable power throttling
                            DisabledValue = 1, // Disable power throttling
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "gaming-explorer-alt-tab-filter",
                    Name = "Alt+Tab Filter",
                    Description = "Sets Alt+Tab to show open windows only",
                    GroupName = "File Explorer Settings",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath =
                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "MultiTaskingAltTabFilter",
                            RecommendedValue = 3,
                            EnabledValue = 3,
                            DisabledValue = 0,
                            DefaultValue = 3, // Default value when registry key exists but no value is set
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
            },
        };
    }
}
