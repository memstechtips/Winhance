using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Optimize.Models;

public static class GamingandPerformanceOptimizations
{
    public static OptimizationGroup GetGamingandPerformanceOptimizations()
    {
        return new OptimizationGroup
        {
            Name = "Gaming and Performance",
            Category = OptimizationCategory.GamingandPerformance,
            Settings = new List<OptimizationSetting>
            {
                new OptimizationSetting
                {
                    Id = "gaming-xbox-game-dvr",
                    Name = "Xbox Game DVR",
                    Description = "Controls Xbox Game DVR functionality",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "Game Recording",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Gaming",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "System\\GameConfigStore",
                            Name = "GameDVR_Enabled",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, Game DVR is enabled
                            DisabledValue = 0, // When toggle is OFF, Game DVR is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls Game Bar and Game DVR functionality",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                        new RegistrySetting
                        {
                            Category = "Gaming",
                            Hive = RegistryHive.LocalMachine,
                            SubKey =
                                "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\GameConfigStore",
                            Name = "AllowGameDVR",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, Xbox Game DVR is enabled
                            DisabledValue = 0, // When toggle is OFF, Xbox Game DVR is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls Xbox GameDVR functionality",
                            IsPrimary = false,
                            AbsenceMeansEnabled = true,
                        },
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All,
                },
                new OptimizationSetting
                {
                    Id = "gaming-game-bar-controller",
                    Name = "Game Bar Controller Access",
                    Description = "Allow your controller to open Game Bar",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "Game Bar",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Gaming",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\GameBar",
                            Name = "UseNexusForGameBarEnabled",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, controller access is enabled
                            DisabledValue = 0, // When toggle is OFF, controller access is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls Xbox Game Bar access via game controller",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "gaming-game-mode",
                    Name = "Game Mode",
                    Description = "Controls Game Mode for optimized gaming performance",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "Game Mode",
                    IsEnabled = true,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Gaming",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\GameBar",
                            Name = "AutoGameModeEnabled",
                            RecommendedValue = 1, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, Game Mode is enabled
                            DisabledValue = 0, // When toggle is OFF, Game Mode is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls Game Mode for optimized gaming performance",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "gaming-directx-optimizations",
                    Name = "DirectX Optimizations",
                    Description = "Changes DirectX settings for optimal gaming performance",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "DirectX",
                    IsEnabled = true,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Gaming",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\DirectX\\UserGpuPreferences",
                            Name = "DirectXUserGlobalSettings",
                            RecommendedValue = "SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;", // For backward compatibility
                            EnabledValue = "SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;", // When toggle is ON, optimizations are enabled
                            DisabledValue = "", // When toggle is OFF, use default settings
                            ValueType = RegistryValueKind.String,
                            DefaultValue = "SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;", // Default value when registry key exists but no value is set
                            Description =
                                "Controls DirectX settings for optimal gaming performance",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "gaming-nvidia-sharpening",
                    Name = "Old Nvidia Sharpening",
                    Description = "Controls Nvidia sharpening for image quality",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "Nvidia",
                    IsEnabled = true,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Gaming",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "Software\\NVIDIA Corporation\\Global\\FTS",
                            Name = "EnableGR535",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 0, // When toggle is ON, old Nvidia sharpening is enabled (0 = enabled for this setting)
                            DisabledValue = 1, // When toggle is OFF, old Nvidia sharpening is disabled (1 = disabled for this setting)
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls Nvidia sharpening for image quality",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "gaming-high-precision-event-timer",
                    Name = "High Precision Event Timer",
                    Description = "Controls the High Precision Event Timer (HPET) for improved system performance",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "System Performance",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    CommandSettings = new List<CommandSetting>
                    {
                        new CommandSetting
                        {
                            Id = "hpet-platform-clock",
                            Category = "Gaming",
                            Description = "Controls the platform clock setting for HPET",
                            EnabledCommand = "bcdedit /set useplatformclock true",
                            DisabledCommand = "bcdedit /deletevalue useplatformclock",
                            RequiresElevation = true,
                            IsPrimary = true
                        },
                        new CommandSetting
                        {
                            Id = "hpet-dynamic-tick",
                            Category = "Gaming",
                            Description = "Controls the dynamic tick setting for HPET",
                            EnabledCommand = "bcdedit /set disabledynamictick no",
                            DisabledCommand = "bcdedit /set disabledynamictick yes",
                            RequiresElevation = true,
                            IsPrimary = false
                        }
                    },
                    LinkedSettingsLogic = LinkedSettingsLogic.All,
                },
                new OptimizationSetting
                {
                    Id = "gaming-system-responsiveness",
                    Name = "System Responsiveness for Games",
                    Description = "Controls system responsiveness for multimedia applications",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "System Performance",
                    IsEnabled = true,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Gaming",
                            Hive = RegistryHive.LocalMachine,
                            SubKey =
                                "Software\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile",
                            Name = "SystemResponsiveness",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 0, // When toggle is ON, system responsiveness is optimized for games (0 = prioritize foreground)
                            DisabledValue = 10, // When toggle is OFF, system responsiveness is balanced (10 = default Windows value)
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 10, // Default value when registry key exists but no value is set
                            Description =
                                "Controls system responsiveness for multimedia applications",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "gaming-network-throttling",
                    Name = "Network Throttling for Gaming",
                    Description = "Controls network throttling for optimal gaming performance",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "System Performance",
                    IsEnabled = true,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Gaming",
                            Hive = RegistryHive.LocalMachine,
                            SubKey =
                                "Software\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile",
                            Name = "NetworkThrottlingIndex",
                            RecommendedValue = 10, // For backward compatibility
                            EnabledValue = 10, // When toggle is ON, network throttling is disabled (10 = disabled)
                            DisabledValue = 5, // When toggle is OFF, network throttling is enabled (default Windows value)
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 5, // Default value when registry key exists but no value is set
                            Description =
                                "Controls network throttling for optimal gaming performance",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "gaming-gpu-priority",
                    Name = "GPU Priority for Gaming",
                    Description = "Controls GPU priority for gaming performance",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "System Performance",
                    IsEnabled = true,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Gaming",
                            Hive = RegistryHive.LocalMachine,
                            SubKey =
                                "Software\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\Games",
                            Name = "GPU Priority",
                            RecommendedValue = 8, // For backward compatibility
                            EnabledValue = 8, // When toggle is ON, GPU priority is high (8 = high priority)
                            DisabledValue = 2, // When toggle is OFF, GPU priority is normal (default Windows value)
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 2, // Default value when registry key exists but no value is set
                            Description = "Controls GPU priority for gaming performance",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "gaming-cpu-priority",
                    Name = "CPU Priority for Gaming",
                    Description = "Controls CPU priority for gaming performance",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "System Performance",
                    IsEnabled = true,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Gaming",
                            Hive = RegistryHive.LocalMachine,
                            SubKey =
                                "Software\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\Games",
                            Name = "Priority",
                            RecommendedValue = 6, // For backward compatibility
                            EnabledValue = 6, // When toggle is ON, CPU priority is high (6 = high priority)
                            DisabledValue = 2, // When toggle is OFF, CPU priority is normal (default Windows value)
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 2, // Default value when registry key exists but no value is set
                            Description = "Controls CPU priority for gaming performance",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "gaming-scheduling-category",
                    Name = "High Scheduling Category for Gaming",
                    Description = "Controls scheduling category for games",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "System Performance",
                    IsEnabled = true,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Gaming",
                            Hive = RegistryHive.LocalMachine,
                            SubKey =
                                "Software\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\Games",
                            Name = "Scheduling Category",
                            RecommendedValue = "High", // For backward compatibility
                            EnabledValue = "High", // When toggle is ON, scheduling category is high
                            DisabledValue = "Medium", // When toggle is OFF, scheduling category is medium (default Windows value)
                            ValueType = RegistryValueKind.String,
                            DefaultValue = "Medium", // Default value when registry key exists but no value is set
                            Description = "Controls scheduling category for games",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "gaming-gpu-scheduling",
                    Name = "Hardware-Accelerated GPU Scheduling",
                    Description = "Controls hardware-accelerated GPU scheduling",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "System Performance",
                    IsEnabled = true,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Gaming",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "System\\CurrentControlSet\\Control\\GraphicsDrivers",
                            Name = "HwSchMode",
                            RecommendedValue = 2, // For backward compatibility
                            EnabledValue = 2, // When toggle is ON, hardware-accelerated GPU scheduling is enabled
                            DisabledValue = 1, // When toggle is OFF, hardware-accelerated GPU scheduling is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls hardware-accelerated GPU scheduling",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "gaming-win32-priority",
                    Name = "Win32 Priority Separation",
                    Description = "Controls Win32 priority separation for program performance",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "System Performance",
                    IsEnabled = true,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Gaming",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "System\\CurrentControlSet\\Control\\PriorityControl",
                            Name = "Win32PrioritySeparation",
                            RecommendedValue = 38, // For backward compatibility
                            EnabledValue = 38, // When toggle is ON, priority is set for best performance of programs
                            DisabledValue = 2, // When toggle is OFF, priority is set to default Windows value
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 2, // Default value when registry key exists but no value is set
                            Description =
                                "Controls Win32 priority separation for program performance",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "gaming-storage-sense",
                    Name = "Storage Sense",
                    Description = "Controls Storage Sense functionality",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "System Performance",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Gaming",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\StorageSense",
                            Name = "AllowStorageSenseGlobal",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, Storage Sense is enabled
                            DisabledValue = 0, // When toggle is OFF, Storage Sense is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls Storage Sense functionality",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "performance-animations",
                    Name = "UI Animations",
                    Description = "Controls UI animations for improved performance",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "Visual Effects",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Performance",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Control Panel\\Desktop\\WindowMetrics",
                            Name = "MinAnimate",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, animations are enabled
                            DisabledValue = 0, // When toggle is OFF, animations are disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls UI animations for improved performance",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "performance-autostart-delay",
                    Name = "Startup Delay for Apps",
                    Description = "Controls startup delay for applications",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "Startup",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Performance",
                            Hive = RegistryHive.CurrentUser,
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Serialize",
                            Name = "StartupDelayInMSec",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 10000, // When toggle is ON, startup delay is enabled (10 seconds)
                            DisabledValue = 0, // When toggle is OFF, startup delay is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Controls startup delay for applications",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "performance-background-services",
                    Name = "Optimize Background Services",
                    Description = "Controls background services for better performance",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "System Services",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Performance",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SYSTEM\\CurrentControlSet\\Control",
                            Name = "ServicesPipeTimeout",
                            RecommendedValue = 60000, // For backward compatibility
                            EnabledValue = 30000, // When toggle is ON, services timeout is reduced (30 seconds)
                            DisabledValue = 60000, // When toggle is OFF, services timeout is default (60 seconds)
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 60000, // Default value when registry key exists but no value is set
                            Description = "Controls background services for better performance",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "performance-desktop-composition",
                    Name = "Desktop Composition Effects",
                    Description = "Controls desktop composition effects",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "Visual Effects",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Performance",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\DWM",
                            Name = "CompositionPolicy",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, desktop composition is enabled
                            DisabledValue = 0, // When toggle is OFF, desktop composition is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls desktop composition effects",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "performance-fast-startup",
                    Name = "Fast Startup",
                    Description = "Controls fast startup feature",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "Startup",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Performance",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power",
                            Name = "HiberbootEnabled",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, fast startup is enabled
                            DisabledValue = 0, // When toggle is OFF, fast startup is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls fast startup feature",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "performance-explorer-search",
                    Name = "Optimize File Explorer Search",
                    Description = "Controls file explorer search indexing",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "File Explorer",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Performance",
                            Hive = RegistryHive.CurrentUser,
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Search\\Preferences",
                            Name = "WholeFileSystem",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, search includes whole file system
                            DisabledValue = 0, // When toggle is OFF, search is limited to indexed locations
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Controls file explorer search indexing",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "performance-menu-animations",
                    Name = "Menu Animations",
                    Description = "Controls menu animations",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "Visual Effects",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Performance",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Control Panel\\Desktop",
                            Name = "MenuShowDelay",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 400, // When toggle is ON, menu animations are enabled (default delay)
                            DisabledValue = 0, // When toggle is OFF, menu animations are disabled
                            ValueType = RegistryValueKind.String,
                            DefaultValue = 400, // Default value when registry key exists but no value is set
                            Description = "Controls menu animations",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "performance-prefetch",
                    Name = "Prefetch Feature",
                    Description = "Controls Windows prefetch feature",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "System Performance",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Performance",
                            Hive = RegistryHive.LocalMachine,
                            SubKey =
                                "SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management\\PrefetchParameters",
                            Name = "EnablePrefetcher",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 3, // When toggle is ON, prefetch is enabled (3 = both application and boot prefetching)
                            DisabledValue = 0, // When toggle is OFF, prefetch is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 3, // Default value when registry key exists but no value is set
                            Description = "Controls Windows prefetch feature",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "performance-remote-assistance",
                    Name = "Remote Assistance",
                    Description = "Controls remote assistance feature",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "System Services",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Performance",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SYSTEM\\CurrentControlSet\\Control\\Remote Assistance",
                            Name = "fAllowToGetHelp",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, remote assistance is enabled
                            DisabledValue = 0, // When toggle is OFF, remote assistance is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls remote assistance feature",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "performance-superfetch",
                    Name = "Superfetch Service",
                    Description = "Controls superfetch/SysMain service",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "System Performance",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Performance",
                            Hive = RegistryHive.LocalMachine,
                            SubKey =
                                "SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management\\PrefetchParameters",
                            Name = "EnableSuperfetch",
                            RecommendedValue = 0, // For backward compatibility
                            EnabledValue = 3, // When toggle is ON, superfetch is enabled (3 = full functionality)
                            DisabledValue = 0, // When toggle is OFF, superfetch is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 3, // Default value when registry key exists but no value is set
                            Description = "Controls superfetch/SysMain service",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "performance-visual-effects",
                    Name = "Optimize Visual Effects",
                    Description = "Controls visual effects for best performance",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "Visual Effects",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Performance",
                            Hive = RegistryHive.CurrentUser,
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects",
                            Name = "VisualFXSetting",
                            RecommendedValue = 2, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, visual effects are set to "best appearance" (1)
                            DisabledValue = 2, // When toggle is OFF, visual effects are set to "best performance" (2)
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 2, // Default value when registry key exists but no value is set
                            Description = "Controls visual effects for best performance",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "explorer-mouse-precision",
                    Name = "Enhance Pointer Precision",
                    Description = "Controls enhanced pointer precision (mouse acceleration)",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "Mouse Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Control Panel\\Mouse",
                            Name = "MouseSpeed",
                            RecommendedValue = "0",
                            EnabledValue = "1", // When toggle is ON, enhanced pointer precision is enabled
                            DisabledValue = "0", // When toggle is OFF, enhanced pointer precision is disabled
                            ValueType = RegistryValueKind.String,
                            DefaultValue = "1", // Default value when registry key exists but no value is set
                            Description =
                                "Controls enhanced pointer precision (mouse acceleration)",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "explorer-animations",
                    Name = "System Animations",
                    Description = "Controls animations and visual effects",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Control Panel\\Desktop",
                            Name = "UserPreferencesMask",
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
                            ValueType = RegistryValueKind.Binary,
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
                            Description = "Controls animations and visual effects",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "explorer-menu-show-delay",
                    Name = "Menu Show Delay",
                    Description = "Controls menu show delay",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Control Panel\\Desktop",
                            Name = "MenuShowDelay",
                            RecommendedValue = 0,
                            EnabledValue = 400, // When toggle is ON, menu show delay is enabled (default value)
                            DisabledValue = 0, // When toggle is OFF, menu show delay is disabled
                            ValueType = RegistryValueKind.String,
                            DefaultValue = 400, // Default value when registry key exists but no value is set
                            Description = "Controls menu show delay",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "explorer-set-visual-effects",
                    Name = "Set Visual Effects",
                    Description = "Sets appearance options to custom",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects",
                            Name = "VisualFXSetting",
                            RecommendedValue = 3,
                            EnabledValue = 3,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 3, // Default value when registry key exists but no value is set
                            Description = "Sets appearance options to custom",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "explorer-taskbar-animations",
                    Name = "Taskbar Animations",
                    Description = "Controls taskbar animations",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "TaskbarAnimations",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, taskbar animations are enabled
                            DisabledValue = 0, // When toggle is OFF, taskbar animations are disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls taskbar animations",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "background-apps",
                    Name = "Let Apps Run in Background",
                    Description = "Controls whether apps can run in the background",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "Background Apps",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Performance",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\AppPrivacy",
                            Name = "LetAppsRunInBackground",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, background apps are enabled
                            DisabledValue = 0, // When toggle is OFF, background apps are disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls whether apps can run in the background",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new OptimizationSetting
                {
                    Id = "explorer-alt-tab-filter",
                    Name = "Alt+Tab Filter",
                    Description = "Sets Alt+Tab to show open windows only",
                    Category = OptimizationCategory.GamingandPerformance,
                    GroupName = "File Explorer Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "MultiTaskingAltTabFilter",
                            RecommendedValue = 3,
                            EnabledValue = 3,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 3, // Default value when registry key exists but no value is set
                            Description = "Sets Alt+Tab to show open windows only",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
            },
        };
    }
}
