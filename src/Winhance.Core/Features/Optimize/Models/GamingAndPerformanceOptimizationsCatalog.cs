using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using static Winhance.Core.Features.Common.Catalog.StateValue;

namespace Winhance.Core.Features.Optimize.Models;

public static class GamingAndPerformanceOptimizationsCatalog
{
    public const string FeatureId = FeatureIds.GamingPerformance;
    public const string FeatureName = "Gaming and Performance";

    public static IReadOnlyList<Setting> All { get; } = new Setting[]
    {
        new()
        {
            Id = "gaming-game-mode",
            Display = new()
            {
                Name = "Game Mode",
                Description = "Optimize your PC for play by turning things off in the background",
                Icon = FluentIcons.TopSpeed,
            },
            Targets = new Target[] { new RegTarget("AutoGameModeEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\GameBar" }, "AutoGameModeEnabled", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AutoGameModeEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AutoGameModeEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "gaming-performance-explorer-mouse-precision",
            Display = new()
            {
                Name = "Enhance Pointer Precision",
                Description = "Adjust cursor speed based on movement velocity (mouse acceleration). Most competitive gamers disable this for consistent aiming in FPS games",
                Icon = MaterialIcons.Mouse,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("MouseSpeed", new[] { @"HKEY_CURRENT_USER\Control Panel\Mouse" }, "MouseSpeed", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["MouseSpeed"] = Of("1") },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MouseSpeed"] = Of("0") },
                },
            },
        },
        new()
        {
            Id = "gaming-performance-mouse-hover-time",
            Display = new()
            {
                Name = "Mouse Hover Time",
                Description = "Controls how long you must hover over an element before it activates (in milliseconds). Lower values make tooltips, menus, and hover effects appear faster. Default is 400ms",
                Icon = MaterialIcons.Mouse,
                AddedInVersion = "26.04.03",
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("MouseHoverTime", new[] { @"HKEY_CURRENT_USER\Control Panel\Mouse" }, "MouseHoverTime", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = "1ms (Instant)",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["MouseHoverTime"] = Of("1") },
                },
                new SettingState
                {
                    Label = "10ms (Very Fast)",
                    Set = new Dictionary<string, StateValue> { ["MouseHoverTime"] = Of("10") },
                },
                new SettingState
                {
                    Label = "50ms (Fast)",
                    Set = new Dictionary<string, StateValue> { ["MouseHoverTime"] = Of("50") },
                },
                new SettingState
                {
                    Label = "100ms (Moderate)",
                    Set = new Dictionary<string, StateValue> { ["MouseHoverTime"] = Of("100") },
                },
                new SettingState
                {
                    Label = "200ms",
                    Set = new Dictionary<string, StateValue> { ["MouseHoverTime"] = Of("200") },
                },
                new SettingState
                {
                    Label = "400ms (Default)",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["MouseHoverTime"] = Of("400").OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "gaming-performance-autostart-delay",
            Display = new()
            {
                Name = "Startup Delay for Apps",
                Description = "Delay startup applications by 10 seconds after boot to improve initial system responsiveness. Windows becomes usable faster, but your startup apps take longer to load",
                Icon = MaterialIcons.ClockStart,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("StartupDelayInMSec", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize" }, "StartupDelayInMSec", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["StartupDelayInMSec"] = Of(10000) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["StartupDelayInMSec"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "gaming-background-apps",
            Display = new()
            {
                Name = "Background App Permissions",
                Description = "Control whether apps can run in the background via Group Policy. Force Deny removes per-app background settings from Windows Settings. Use User in Control if you need apps like Teams, Zoom, or WhatsApp",
                Icon = MaterialIcons.Apps,
                AddedInVersion = "26.04.08",
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("LetAppsRunInBackground", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy" }, "LetAppsRunInBackground", RegistryValueKind.DWord) { IsGroupPolicy = true } },
            States = new[]
            {
                new SettingState
                {
                    Label = "User in Control (Default)",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["LetAppsRunInBackground"] = Absent },
                },
                new SettingState
                {
                    Label = "Force Allow",
                    Set = new Dictionary<string, StateValue> { ["LetAppsRunInBackground"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Force Deny",
                    Warning = "WARNING: Force Deny removes background app permissions from Windows Settings entirely. Apps requiring background access (Teams, Zoom, WhatsApp, etc.) may not function correctly.",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["LetAppsRunInBackground"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-storage-sense",
            Display = new()
            {
                Name = "Storage Sense",
                Description = "Automatically free up disk space by removing temporary files, emptying the recycle bin, and managing downloads",
                Icon = MaterialIcons.Harddisk,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("AllowStorageSenseGlobal", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\StorageSense", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\StorageSense" }, "AllowStorageSenseGlobal", RegistryValueKind.DWord) { IsGroupPolicy = true } },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AllowStorageSenseGlobal"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AllowStorageSenseGlobal"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "gaming-performance-explorer-search",
            Display = new()
            {
                Name = "Search Entire File System",
                Description = "Search your entire file system instead of only indexed locations. This provides more complete results but is significantly slower than indexed search and increases disk activity",
                Icon = MaterialIcons.FolderSearch,
            },
            Targets = new Target[] { new RegTarget("WholeFileSystem", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Search\Preferences" }, "WholeFileSystem", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["WholeFileSystem"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["WholeFileSystem"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "gaming-performance-search-webview2",
            Display = new()
            {
                Name = "WebView2 in Windows Search",
                Description = "Allow Windows Search to use WebView2 (Edge) for rendering search results. Disabling removes Edge processes spawned by SearchHost.exe, reducing resource usage. Uses an undocumented Windows Feature Management override (feature ID 37926450) that may change in future Windows updates",
                Icon = FluentIcons.GlobeSearch,
                AddedInVersion = "26.04.03",
            },
            Targets = new Target[]
            {
                new RegTarget("EnabledState", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\8\1694661260" }, "EnabledState", RegistryValueKind.DWord),
                new RegTarget("EnabledStateOptions", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\8\1694661260" }, "EnabledStateOptions", RegistryValueKind.DWord),
                new RegTarget("Variant", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\8\1694661260" }, "Variant", RegistryValueKind.DWord),
                new RegTarget("VariantPayload", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\8\1694661260" }, "VariantPayload", RegistryValueKind.DWord),
                new RegTarget("VariantPayloadKind", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FeatureManagement\Overrides\8\1694661260" }, "VariantPayloadKind", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["EnabledState"] = Of(2),
                        ["EnabledStateOptions"] = Absent,
                        ["Variant"] = Absent,
                        ["VariantPayload"] = Absent,
                        ["VariantPayloadKind"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["EnabledState"] = Of(1),
                        ["EnabledStateOptions"] = Of(0),
                        ["Variant"] = Of(0),
                        ["VariantPayload"] = Of(0),
                        ["VariantPayloadKind"] = Of(0),
                    },
                },
            },
        },
        new()
        {
            Id = "gaming-performance-wallpaper-compression",
            Display = new()
            {
                Name = "Allow Desktop Wallpaper Compression",
                Description = "Allow Windows to compress wallpapers to save disk space and improve performance. Only affects images in JPEG format.",
                Icon = FluentIcons.ResizeImage,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[] { new RegTarget("JPEGImportQuality", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "JPEGImportQuality", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["JPEGImportQuality"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["JPEGImportQuality"] = Of(100) },
                },
            },
        },
        new()
        {
            Id = "gaming-performance-explorer-menu-show-delay",
            Display = new()
            {
                Name = "Menu Show Delay",
                Description = "Add a brief delay before displaying menus (400ms - Windows default), or show them instantly (0ms) for faster navigation",
                Icon = MaterialIcons.MenuOpen,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("MenuShowDelay", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "MenuShowDelay", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["MenuShowDelay"] = Of("400") },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MenuShowDelay"] = Of("0") },
                },
            },
        },
        new()
        {
            Id = "gaming-explorer-alt-tab-filter",
            Display = new()
            {
                Name = "Alt+Tab Filter",
                Description = "Show only traditional open windows in Alt+Tab instead of including Microsoft Edge tabs and other Windows suggestions",
                Icon = MaterialIcons.ViewGrid,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("MultiTaskingAltTabFilter", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "MultiTaskingAltTabFilter", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["MultiTaskingAltTabFilter"] = Of(3) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MultiTaskingAltTabFilter"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "gaming-win32-priority",
            Display = new()
            {
                Name = "Adjust processor for best performance of",
                Description = "Configure how Windows allocates CPU time between foreground applications and background services",
                GroupName = "Processor",
                Icon = MaterialIcons.Application,
            },
            Targets = new Target[] { new RegTarget("Win32PrioritySeparation", new[] { @"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\PriorityControl" }, "Win32PrioritySeparation", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Programs",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Win32PrioritySeparation"] = Of(38) },
                },
                new SettingState
                {
                    Label = "Background Services",
                    Set = new Dictionary<string, StateValue> { ["Win32PrioritySeparation"] = Of(24) },
                },
            },
        },
        new()
        {
            Id = "gaming-system-responsiveness",
            Display = new()
            {
                Name = "System Responsiveness for Games",
                Description = "Minimize background task interference by allocating more CPU time to your active game or multimedia application",
                GroupName = "Processor",
                Icon = MaterialIcons.Speedometer,
            },
            Targets = new Target[] { new RegTarget("SystemResponsiveness", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" }, "SystemResponsiveness", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["SystemResponsiveness"] = Of(10) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["SystemResponsiveness"] = Of(20) },
                },
            },
        },
        new()
        {
            Id = "gaming-cpu-priority",
            Display = new()
            {
                Name = "CPU Priority for Gaming",
                Description = "Give games higher CPU scheduling priority to dedicate more processor time to your game",
                GroupName = "Processor",
                Icon = MaterialIcons.Chip,
            },
            Targets = new Target[] { new RegTarget("Priority", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games" }, "Priority", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Priority"] = Of(6) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Priority"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-scheduling-category",
            Display = new()
            {
                Name = "High Scheduling Category for Gaming",
                Description = "Assign high-priority scheduling category to ensure games receive preferential system resource allocation",
                GroupName = "Processor",
                Icon = MaterialIcons.CalendarClock,
            },
            Targets = new Target[] { new RegTarget("Scheduling Category", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games" }, "Scheduling Category", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Scheduling Category"] = Of("High") },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Scheduling Category"] = Of("Medium") },
                },
            },
        },
        new()
        {
            Id = "gaming-performance-svchost-split-threshold",
            Display = new()
            {
                Name = "Svchost Split Threshold",
                Description = "Set the memory threshold that determines when Windows splits services into separate svchost.exe processes. Higher values group more services together, reducing process count. Select the value matching your system RAM",
                GroupName = "Processor",
                Icon = FluentIcons.BranchCompare,
                AddedInVersion = "25.04.03",
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("SvcHostSplitThresholdInKB", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control" }, "SvcHostSplitThresholdInKB", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Default",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SvcHostSplitThresholdInKB"] = Of(3670016).OrAbsent() },
                },
                new SettingState
                {
                    Label = "4 GB",
                    Set = new Dictionary<string, StateValue> { ["SvcHostSplitThresholdInKB"] = Of(4194304) },
                },
                new SettingState
                {
                    Label = "6 GB",
                    Set = new Dictionary<string, StateValue> { ["SvcHostSplitThresholdInKB"] = Of(6291456) },
                },
                new SettingState
                {
                    Label = "8 GB",
                    Set = new Dictionary<string, StateValue> { ["SvcHostSplitThresholdInKB"] = Of(8388608) },
                },
                new SettingState
                {
                    Label = "12 GB",
                    Set = new Dictionary<string, StateValue> { ["SvcHostSplitThresholdInKB"] = Of(12582912) },
                },
                new SettingState
                {
                    Label = "16 GB",
                    Set = new Dictionary<string, StateValue> { ["SvcHostSplitThresholdInKB"] = Of(16777216) },
                },
                new SettingState
                {
                    Label = "24 GB",
                    Set = new Dictionary<string, StateValue> { ["SvcHostSplitThresholdInKB"] = Of(25165824) },
                },
                new SettingState
                {
                    Label = "32 GB",
                    Set = new Dictionary<string, StateValue> { ["SvcHostSplitThresholdInKB"] = Of(33554432) },
                },
                new SettingState
                {
                    Label = "64 GB",
                    Set = new Dictionary<string, StateValue> { ["SvcHostSplitThresholdInKB"] = Of(67108864) },
                },
                new SettingState
                {
                    Label = "128 GB",
                    Set = new Dictionary<string, StateValue> { ["SvcHostSplitThresholdInKB"] = Of(134217728) },
                },
            },
        },
        new()
        {
            Id = "gaming-gpu-priority",
            Display = new()
            {
                Name = "GPU Priority for Gaming",
                Description = "Give games higher GPU scheduling priority to improve graphics performance and frame rates",
                GroupName = "Graphics",
                Icon = MaterialIcons.Memory,
            },
            Targets = new Target[] { new RegTarget("GPU Priority", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games" }, "GPU Priority", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["GPU Priority"] = Of(8) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["GPU Priority"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-gpu-scheduling",
            Display = new()
            {
                Name = "Hardware-Accelerated GPU Scheduling",
                Description = "Let your GPU manage its own memory and scheduling for reduced latency and improved performance",
                GroupName = "Graphics",
                Icon = MaterialIcons.ExpansionCard,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("HwSchMode", new[] { @"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\GraphicsDrivers" }, "HwSchMode", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["HwSchMode"] = Of(2).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["HwSchMode"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "gaming-directx-flip-model",
            Display = new()
            {
                Name = "Optimizations for windowed games",
                Description = "Reduce latency and use advanced features in compatible games by using DirectX flip presentation model",
                GroupName = "Graphics",
                Icon = MaterialIcons.ApplicationCog,
            },
            Targets = new Target[] { new RegTarget("DirectXUserGlobalSettings", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences" }, "DirectXUserGlobalSettings", RegistryValueKind.String) { CompositeStringKey = "SwapEffectUpgradeEnable" } },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DirectXUserGlobalSettings"] = Of("1").OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DirectXUserGlobalSettings"] = Of("0") },
                },
            },
        },
        new()
        {
            Id = "gaming-directx-vrr-optimizations",
            Display = new()
            {
                Name = "Variable Refresh Rate",
                Description = "Enable VRR (G-Sync/FreeSync) optimizations for smoother gameplay. Requires a VRR-compatible monitor; this setting has no effect if your monitor does not support VRR",
                GroupName = "Graphics",
                Icon = MaterialIcons.MonitorShimmer,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("DirectXUserGlobalSettings", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences" }, "DirectXUserGlobalSettings", RegistryValueKind.String) { CompositeStringKey = "VRROptimizeEnable" } },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DirectXUserGlobalSettings"] = Of("1").OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DirectXUserGlobalSettings"] = Of("0") },
                },
            },
        },
        new()
        {
            Id = "gaming-directx-auto-hdr",
            Display = new()
            {
                Name = "Auto HDR",
                Description = "Automatically convert SDR content to HDR for enhanced colors and brightness. Requires an HDR-capable display with HDR enabled; this setting has no effect if your display does not support HDR",
                GroupName = "Graphics",
                Icon = MaterialIcons.Hdr,
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[] { new RegTarget("DirectXUserGlobalSettings", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences" }, "DirectXUserGlobalSettings", RegistryValueKind.String) { CompositeStringKey = "AutoHDREnable" } },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["DirectXUserGlobalSettings"] = Of("1") },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DirectXUserGlobalSettings"] = Of("0") },
                },
            },
        },
        new()
        {
            Id = "gaming-nvidia-sharpening",
            Display = new()
            {
                Name = "Legacy NVIDIA Sharpening",
                Description = "Enable legacy NVIDIA image sharpening filter for enhanced visual clarity. Only works on older NVIDIA drivers; newer drivers should use NVIDIA Control Panel sharpening instead",
                GroupName = "Graphics",
                Icon = MaterialIcons.ImageFilterHdr,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("EnableGR535", new[] { @"HKEY_LOCAL_MACHINE\Software\NVIDIA Corporation\Global\FTS" }, "EnableGR535", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["EnableGR535"] = Of(0) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["EnableGR535"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "gaming-fullscreen-optimizations",
            Display = new()
            {
                Name = "Fullscreen Optimizations",
                Description = "Allow Windows to optimize games running in fullscreen mode. Disabling can fix performance issues or stuttering in some older games that don't work well with borderless fullscreen optimization",
                GroupName = "Graphics",
                Icon = MaterialIcons.MonitorScreenshot,
            },
            Targets = new Target[] { new RegTarget("GameDVR_FSEBehaviorMode", new[] { @"HKEY_CURRENT_USER\System\GameConfigStore" }, "GameDVR_FSEBehaviorMode", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["GameDVR_FSEBehaviorMode"] = Of(0) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["GameDVR_FSEBehaviorMode"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-performance-desktop-composition",
            Display = new()
            {
                Name = "Desktop Composition Effects",
                Description = "Enable visual effects managed by the Desktop Window Manager. Disabling may provide minor performance gains on older hardware but will break Aero effects",
                GroupName = "Graphics",
                Icon = MaterialIcons.ViewDashboard,
            },
            Targets = new Target[] { new RegTarget("CompositionPolicy", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM" }, "CompositionPolicy", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["CompositionPolicy"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["CompositionPolicy"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "gaming-auto-color-management",
            Display = new()
            {
                Name = "Automatically manage color for apps",
                Description = "Allow Windows to automatically manage color profiles for all connected displays that support it",
                GroupName = "Graphics",
                Icon = MaterialIcons.Color,
                AddedInVersion = "26.03.27",
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("AutoColorManagementEnabled", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers\MonitorDataStore" }, "AutoColorManagementEnabled", RegistryValueKind.DWord) { PerMonitor = true } },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["AutoColorManagementEnabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AutoColorManagementEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "gaming-disable-mpo",
            Display = new()
            {
                Name = "Multi-Plane Overlay (MPO)",
                Description = "Composite multiple display layers in hardware using the GPU. Disabling can fix screen flickering, black screens, and stuttering on multi-monitor setups",
                GroupName = "Graphics",
                Icon = MaterialIcons.MonitorDashboard,
                AddedInVersion = "26.04.03",
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("OverlayTestMode", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Dwm" }, "OverlayTestMode", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["OverlayTestMode"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["OverlayTestMode"] = Of(5) },
                },
            },
        },
        new()
        {
            Id = "gaming-disable-all-overlays",
            Display = new()
            {
                Name = "Hardware Overlays",
                Description = "Allow the graphics driver to use hardware overlay surfaces for compositing. Disabling forces software composition for all overlays and is known to break the Steam, Discord, and RTSS in-game overlays — leave enabled unless you specifically need this",
                GroupName = "Graphics",
                Icon = MaterialIcons.MonitorDashboard,
                AddedInVersion = "26.05.08",
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("DisableOverlays", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" }, "DisableOverlays", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableOverlays"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DisableOverlays"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "gaming-disable-mpo-min-fps",
            Display = new()
            {
                Name = "MPO Minimum Frame Rate Requirement",
                Description = "Allow Desktop Window Manager to dynamically switch apps between overlay modes based on frame rate. Disabling can fix stuttering in browsers and Discord without fully disabling MPO",
                GroupName = "Graphics",
                Icon = MaterialIcons.MonitorDashboard,
                AddedInVersion = "26.04.03",
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("OverlayMinFPS", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Dwm" }, "OverlayMinFPS", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["OverlayMinFPS"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["OverlayMinFPS"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "gaming-network-throttling",
            Display = new()
            {
                Name = "Network Throttling",
                Description = "Controls network packet rate limiting for multimedia applications. Keeping throttling enabled (default: 10 packets/ms) is recommended as it provides better DPC latency for gaming than disabling it entirely",
                GroupName = "Network",
                Icon = MaterialIcons.NetworkOffOutline,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("NetworkThrottlingIndex", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" }, "NetworkThrottlingIndex", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["NetworkThrottlingIndex"] = Of(10) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["NetworkThrottlingIndex"] = Of(-1) },
                },
            },
        },
        new()
        {
            Id = "gaming-nagle-algorithm",
            Display = new()
            {
                Name = "Nagle's Algorithm",
                Description = "Buffers small network packets before sending to reduce overhead. Turn off to lower latency in online games, or keep on for general-purpose network efficiency",
                GroupName = "Network",
                Icon = MaterialIcons.Wan,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("TcpAckFrequency", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces" }, "TcpAckFrequency", RegistryValueKind.DWord) { PerNetworkInterface = true },
                new RegTarget("TCPNoDelay", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces" }, "TCPNoDelay", RegistryValueKind.DWord) { PerNetworkInterface = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["TcpAckFrequency"] = Absent,
                        ["TCPNoDelay"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["TcpAckFrequency"] = Of(1),
                        ["TCPNoDelay"] = Of(1),
                    },
                },
            },
        },
        new()
        {
            Id = "gaming-dns-server",
            Display = new()
            {
                Name = "DNS Server",
                Description = "Select a DNS server for all network adapters. Changes apply to every adapter on your system (Wi-Fi and Ethernet). Use Automatic to restore your default ISP/router DNS",
                GroupName = "Network",
                Icon = MaterialIcons.Dns,
                AddedInVersion = "26.04.08",
                IsSubjectivePreference = true,
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Setting_gaming-dns-server_Option_0",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Effects = new Effect[]
                    {
                        new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ResetServerAddresses }", RunContext.User),
                        new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }", RunContext.User),
                    },
                },
                new SettingState
                {
                    Label = "Setting_gaming-dns-server_Option_1",
                    Effects = new Effect[]
                    {
                        new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('1.1.1.1','1.0.0.1') }", RunContext.User),
                        new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; $t = '{{dohtemplate}}'; if ($t -and $t -notmatch '^\{\{') { netsh dns add encryption server=1.1.1.1 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null; netsh dns add encryption server=1.0.0.1 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null }", RunContext.User),
                    },
                },
                new SettingState
                {
                    Label = "Setting_gaming-dns-server_Option_2",
                    Effects = new Effect[]
                    {
                        new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('1.1.1.2','1.0.0.2') }", RunContext.User),
                        new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; $t = '{{dohtemplate}}'; if ($t -and $t -notmatch '^\{\{') { netsh dns add encryption server=1.1.1.2 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null; netsh dns add encryption server=1.0.0.2 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null }", RunContext.User),
                    },
                },
                new SettingState
                {
                    Label = "Setting_gaming-dns-server_Option_3",
                    Effects = new Effect[]
                    {
                        new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('1.1.1.3','1.0.0.3') }", RunContext.User),
                        new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; $t = '{{dohtemplate}}'; if ($t -and $t -notmatch '^\{\{') { netsh dns add encryption server=1.1.1.3 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null; netsh dns add encryption server=1.0.0.3 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null }", RunContext.User),
                    },
                },
                new SettingState
                {
                    Label = "Setting_gaming-dns-server_Option_4",
                    Effects = new Effect[]
                    {
                        new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('8.8.8.8','8.8.4.4') }", RunContext.User),
                        new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; $t = '{{dohtemplate}}'; if ($t -and $t -notmatch '^\{\{') { netsh dns add encryption server=8.8.8.8 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null; netsh dns add encryption server=8.8.4.4 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null }", RunContext.User),
                    },
                },
                new SettingState
                {
                    Label = "Setting_gaming-dns-server_Option_5",
                    Effects = new Effect[]
                    {
                        new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('9.9.9.9','149.112.112.112') }", RunContext.User),
                        new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; $t = '{{dohtemplate}}'; if ($t -and $t -notmatch '^\{\{') { netsh dns add encryption server=9.9.9.9 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null; netsh dns add encryption server=149.112.112.112 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null }", RunContext.User),
                    },
                },
                new SettingState
                {
                    Label = "Setting_gaming-dns-server_Option_6",
                    Effects = new Effect[]
                    {
                        new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('208.67.222.222','208.67.220.220') }", RunContext.User),
                        new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; $t = '{{dohtemplate}}'; if ($t -and $t -notmatch '^\{\{') { netsh dns add encryption server=208.67.222.222 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null; netsh dns add encryption server=208.67.220.220 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null }", RunContext.User),
                    },
                },
                new SettingState
                {
                    Label = "Setting_gaming-dns-server_Option_7",
                    Effects = new Effect[]
                    {
                        new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('1.1.1.1','1.0.0.1') }", RunContext.User),
                        new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; $t = 'https://cloudflare-dns.com/dns-query'; if ($t -and $t -notmatch '^\{\{') { netsh dns add encryption server=1.1.1.1 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null; netsh dns add encryption server=1.0.0.1 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null }", RunContext.User),
                    },
                },
                new SettingState
                {
                    Label = "Setting_gaming-dns-server_Option_8",
                    Effects = new Effect[]
                    {
                        new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('8.8.8.8','8.8.4.4') }", RunContext.User),
                        new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; $t = 'https://dns.google/dns-query'; if ($t -and $t -notmatch '^\{\{') { netsh dns add encryption server=8.8.8.8 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null; netsh dns add encryption server=8.8.4.4 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null }", RunContext.User),
                    },
                },
                new SettingState
                {
                    Label = "Setting_gaming-dns-server_Option_9",
                    Effects = new Effect[]
                    {
                        new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('9.9.9.9','149.112.112.112') }", RunContext.User),
                        new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; $t = 'https://dns.quad9.net/dns-query'; if ($t -and $t -notmatch '^\{\{') { netsh dns add encryption server=9.9.9.9 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null; netsh dns add encryption server=149.112.112.112 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null }", RunContext.User),
                    },
                },
            },
            Detector = new DnsServerDetector("Setting_gaming-dns-server_Option_0", new Dictionary<string, string>
            {
                ["1.1.1.1"] = "Setting_gaming-dns-server_Option_1",
                ["1.1.1.2"] = "Setting_gaming-dns-server_Option_2",
                ["1.1.1.3"] = "Setting_gaming-dns-server_Option_3",
                ["8.8.8.8"] = "Setting_gaming-dns-server_Option_4",
                ["9.9.9.9"] = "Setting_gaming-dns-server_Option_5",
                ["208.67.222.222"] = "Setting_gaming-dns-server_Option_6",
            }),
        },
        new()
        {
            Id = "gaming-virtualization-based-security",
            Display = new()
            {
                Name = "Virtualization Based Security (VBS)",
                Description = "Isolates parts of memory to protect the system from vulnerabilities. Disabling can improve gaming performance but reduces system security",
                GroupName = "Security",
                Icon = MaterialIcons.ShieldLock,
                AddedInVersion = "26.04.01",
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[]
            {
                new RegTarget("EnableVirtualizationBasedSecurity", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard" }, "EnableVirtualizationBasedSecurity", RegistryValueKind.DWord),
                new RegTarget("RequirePlatformSecurityFeatures", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard" }, "RequirePlatformSecurityFeatures", RegistryValueKind.DWord),
                new RegTarget("Locked", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard" }, "Locked", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["EnableVirtualizationBasedSecurity"] = Of(1),
                        ["RequirePlatformSecurityFeatures"] = Of(1),
                        ["Locked"] = Of(1),
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["EnableVirtualizationBasedSecurity"] = Of(0),
                        ["RequirePlatformSecurityFeatures"] = Of(0),
                        ["Locked"] = Of(0),
                    },
                },
            },
        },
        new()
        {
            Id = "gaming-memory-integrity",
            Display = new()
            {
                Name = "Memory Integrity (HVCI)",
                Description = "Prevents malicious code from being inserted into high-security processes. Disabling can improve gaming performance but reduces system security",
                GroupName = "Security",
                Icon = MaterialIcons.MemoryArrowDown,
                AddedInVersion = "26.04.01",
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            UiParentId = "gaming-virtualization-based-security",
            Targets = new Target[]
            {
                new RegTarget("Enabled", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity" }, "Enabled", RegistryValueKind.DWord),
                new RegTarget("Locked", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity" }, "Locked", RegistryValueKind.DWord),
                new RegTarget("WasEnabledBy", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity" }, "WasEnabledBy", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Enabled"] = Of(1),
                        ["Locked"] = Of(1),
                        ["WasEnabledBy"] = Of(2),
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Links = new[] { new Link("gaming-virtualization-based-security", LinkKind.Requires, "Enabled") },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Enabled"] = Of(0),
                        ["Locked"] = Of(0),
                        ["WasEnabledBy"] = Of(0),
                    },
                },
            },
        },
        new()
        {
            Id = "gaming-xbox-game-dvr",
            Display = new()
            {
                Name = "Xbox Game DVR",
                Description = "Record gameplay clips and take screenshots using the Xbox Game Bar overlay. Disabling reduces CPU/GPU usage and can improve frame rates",
                GroupName = "Xbox",
                Icon = MaterialIcons.RecordRec,
            },
            Targets = new Target[]
            {
                new RegTarget("GameDVR_Enabled", new[] { @"HKEY_CURRENT_USER\System\GameConfigStore" }, "GameDVR_Enabled", RegistryValueKind.DWord),
                new RegTarget("AppCaptureEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR" }, "AppCaptureEnabled", RegistryValueKind.DWord),
                new RegTarget("AllowGameDVR", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\GameDVR" }, "AllowGameDVR", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["GameDVR_Enabled"] = Of(1),
                        ["AppCaptureEnabled"] = Of(1),
                        ["AllowGameDVR"] = Of(1),
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["GameDVR_Enabled"] = Of(0),
                        ["AppCaptureEnabled"] = Of(0),
                        ["AllowGameDVR"] = Of(0),
                    },
                },
            },
        },
        new()
        {
            Id = "gaming-game-bar-controller",
            Display = new()
            {
                Name = "Game Bar Controller Access",
                Description = "Allow your Xbox/compatible controller to open Game Bar by pressing the Xbox button. Disable to prevent accidental Game Bar activation during gaming",
                GroupName = "Xbox",
                Icon = FluentIcons.XboxControllerError,
            },
            Targets = new Target[] { new RegTarget("UseNexusForGameBarEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\GameBar" }, "UseNexusForGameBarEnabled", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["UseNexusForGameBarEnabled"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["UseNexusForGameBarEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "gaming-game-bar-tips",
            Display = new()
            {
                Name = "Game Bar Tips and Hints",
                Description = "Show tips and hints about Game Bar features when opening the overlay. Disabling reduces distractions during gameplay",
                GroupName = "Xbox",
                Icon = MaterialIcons.LightbulbOff,
            },
            Targets = new Target[] { new RegTarget("ShowStartupPanel", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\GameBar" }, "ShowStartupPanel", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowStartupPanel"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ShowStartupPanel"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "gaming-performance-background-services",
            Display = new()
            {
                Name = "Optimize Background Services",
                Description = "Reduce the startup timeout for Windows services from 60 to 30 seconds. This can speed up boot time slightly",
                GroupName = "System Services",
                Icon = MaterialIcons.Cog,
            },
            Targets = new Target[] { new RegTarget("ServicesPipeTimeout", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control" }, "ServicesPipeTimeout", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["ServicesPipeTimeout"] = Of(30000) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ServicesPipeTimeout"] = Of(60000) },
                },
            },
        },
        new()
        {
            Id = "gaming-sysmain-service",
            Display = new()
            {
                Name = "SysMain Service (Superfetch)",
                Description = "Preload frequently used applications into RAM for faster launch times. Automatic is recommended for HDD or mixed-storage systems; Manual or Disabled is only suitable for SSD-only systems",
                GroupName = "System Services",
                Icon = MaterialIcons.Cached,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SysMain" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Disabled (Recommended for SSD)",
                    Warning = "WARNING: Disabling SysMain on systems with a traditional hard drive (HDD) can noticeably reduce responsiveness and slow app launches. Recommended only for SSD-only systems.",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "Manual",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3) },
                },
                new SettingState
                {
                    Label = "Automatic (Recommended for HDD)",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "gaming-performance-prefetch",
            Display = new()
            {
                Name = "Prefetch Feature",
                Description = "Preload frequently used applications and boot files into memory to speed up launches. Generally recommended for HDDs not SSDs",
                GroupName = "System Services",
                Icon = MaterialIcons.Download,
                IsSubjectivePreference = true,
            },
            UiParentId = "gaming-sysmain-service",
            Targets = new Target[] { new RegTarget("EnablePrefetcher", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters" }, "EnablePrefetcher", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["EnablePrefetcher"] = Of(3) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Links = new[] { new Link("gaming-sysmain-service", LinkKind.Requires, "Enabled") },
                    Set = new Dictionary<string, StateValue> { ["EnablePrefetcher"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "gaming-windows-search-service",
            Display = new()
            {
                Name = "Windows Search Indexing Service",
                Description = "Indexes files and folders for faster search results. Disabling reduces background CPU and disk activity but breaks Outlook search and makes Start Menu and File Explorer search slow or unreliable",
                GroupName = "System Services",
                Icon = MaterialIcons.DatabaseSearch,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WSearch" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Warning = "WARNING: Disabling WSearch stops file content indexing. Outlook search, Start Menu search, and File Explorer search will become slow or return no results until re-enabled.",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3) },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "gaming-print-spooler-service",
            Display = new()
            {
                Name = "Print Spooler Service",
                Description = "Manages print jobs sent to printers. If you don't use a printer, set to Manual or Disabled to free up system resources",
                GroupName = "System Services",
                Icon = MaterialIcons.Printer,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Spooler" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3) },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "gaming-telemetry-service",
            Display = new()
            {
                Name = "Connected User Experiences and Telemetry Service",
                Description = "Sends usage data and diagnostics to Microsoft. Setting to Manual or Disabled reduces background network and CPU usage",
                GroupName = "System Services",
                Icon = MaterialIcons.CloudUpload,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\DiagTrack" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3) },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "gaming-connected-devices-platform-service",
            Display = new()
            {
                Name = "Connected Devices Platform Service",
                Description = "Enables cross-device experiences like phone linking and nearby sharing. Disabling reduces background activity and device interaction logging",
                GroupName = "System Services",
                Icon = MaterialIcons.CellphoneLink,
                AddedInVersion = "26.03.27",
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\CDPSvc", @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\CDPUserSvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Warning = "Manual or Disabled startup can break Windows Night Light and delay cross-device features (Phone Link, Nearby Sharing, clipboard sync). Choose Automatic if you use Night Light.",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Warning = "Manual or Disabled startup can break Windows Night Light and delay cross-device features (Phone Link, Nearby Sharing, clipboard sync). Choose Automatic if you use Night Light.",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3) },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "gaming-compatibility-assistant-service",
            Display = new()
            {
                Name = "Program Compatibility Assistant Service",
                Description = "Monitors programs for compatibility issues and suggests fixes. Disabling prevents compatibility prompts and saves minor system resources",
                GroupName = "System Services",
                Icon = MaterialIcons.ApplicationCog,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\PcaSvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3) },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "gaming-error-reporting-service",
            Display = new()
            {
                Name = "Windows Error Reporting Service",
                Description = "Collects and sends crash data to Microsoft. Disabling prevents crash reporting, reduces network traffic, and improves privacy with minimal system impact",
                GroupName = "System Services",
                Icon = MaterialIcons.AlertOctagon,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WerSvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-geolocation-service",
            Display = new()
            {
                Name = "Geolocation Service",
                Description = "Tracks your physical location for apps and services. Disabling improves privacy and prevents location tracking, but apps won't be able to use location features",
                GroupName = "System Services",
                Icon = MaterialIcons.MapMarkerOff,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\lfsvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-retail-demo-service",
            Display = new()
            {
                Name = "Retail Demo Service",
                Description = "Controls device activity when in retail demo mode. Safe to disable for personal computers as it only serves retail display purposes",
                GroupName = "System Services",
                Icon = MaterialIcons.StorefrontOutline,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\RetailDemo" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-insider-service",
            Display = new()
            {
                Name = "Windows Insider Service",
                Description = "Manages Windows Insider Program features and preview builds. Safe to disable if you're not participating in the Windows Insider Program",
                GroupName = "System Services",
                Icon = MaterialIcons.TestTube,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\wisvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-phone-service",
            Display = new()
            {
                Name = "Phone Service",
                Description = "Manages telephony state on the device. Safe to disable if you don't use phone connectivity features or make calls from your PC",
                GroupName = "System Services",
                Icon = MaterialIcons.Cellphone,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\PhoneSvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-wallet-service",
            Display = new()
            {
                Name = "Wallet Service",
                Description = "Provides wallet functionality for payment and NFC scenarios. Safe to disable if you don't use Microsoft Wallet features",
                GroupName = "System Services",
                Icon = MaterialIcons.Wallet,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WalletService" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-smart-card-services",
            Display = new()
            {
                Name = "Smart Card Services",
                Description = "Enables smart card reader functionality for security authentication. Safe to disable if you don't use physical smart cards or card readers",
                GroupName = "System Services",
                Icon = MaterialIcons.SmartCard,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SCardSvr", @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\ScDeviceEnum", @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SCPolicySvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-maps-broker-service",
            Display = new()
            {
                Name = "Downloaded Maps Manager",
                Description = "Provides access to downloaded maps for applications. Set to Manual to allow map access when needed while preventing unnecessary background activity",
                GroupName = "System Services",
                Icon = MaterialIcons.MapOutline,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\MapsBroker" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3) },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "gaming-fax-service",
            Display = new()
            {
                Name = "Fax Service",
                Description = "Enables sending and receiving faxes. Safe to disable for most users as fax functionality is rarely used on modern systems",
                GroupName = "System Services",
                Icon = MaterialIcons.Fax,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Fax" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_DisabledRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Manual",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3) },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-wmp-network-service",
            Display = new()
            {
                Name = "Windows Media Player Network Sharing Service",
                Description = "Shares Windows Media Player libraries to other networked players and media devices. Safe to disable if you don't share media over your network",
                GroupName = "System Services",
                Icon = MaterialIcons.ShareOff,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WMPNetworkSvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_DisabledRecommended",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_Manual",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-mixed-reality-service",
            Display = new()
            {
                Name = "Windows Mixed Reality OpenXR Service",
                Description = "Runs OpenXR applications on Windows Mixed Reality devices. Safe to disable if you don't use VR or AR headsets",
                GroupName = "System Services",
                Icon = MaterialIcons.VirtualReality,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\MixedRealityOpenXRSvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3) },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-mobile-hotspot-service",
            Display = new()
            {
                Name = "Windows Mobile Hotspot Service",
                Description = "Provides ability to share internet connection with other devices. Set to Manual to keep functionality available while preventing unnecessary background activity",
                GroupName = "System Services",
                Icon = MaterialIcons.CellphoneWireless,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\icssvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-sms-router-service",
            Display = new()
            {
                Name = "Microsoft Windows SMS Router Service",
                Description = "Routes SMS messages according to rules. Safe to disable if you don't use SMS features on your PC",
                GroupName = "System Services",
                Icon = MaterialIcons.MessageText,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SmsRouter" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-parental-controls-service",
            Display = new()
            {
                Name = "Parental Controls Service",
                Description = "Enables parental controls and family safety features. Safe to disable if you don't use parental control features",
                GroupName = "System Services",
                Icon = MaterialIcons.ShieldAccount,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WpcMonSvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-payments-nfc-service",
            Display = new()
            {
                Name = "Payments and NFC/SE Manager",
                Description = "Manages payments and Near Field Communication secure elements. Safe to disable if you don't use NFC payment features",
                GroupName = "System Services",
                Icon = MaterialIcons.Nfc,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SEMgrSvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-spot-verifier-service",
            Display = new()
            {
                Name = "Spot Verifier Service",
                Description = "Verifies potential file system corruptions. Set to Manual to allow verification when needed while reducing background activity",
                GroupName = "System Services",
                Icon = MaterialIcons.ShieldCheck,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\svsvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-remote-access-manager",
            Display = new()
            {
                Name = "Remote Access Connection Manager",
                Description = "Manages VPN and dial-up connections. Set to Manual to reduce background activity while keeping VPN functionality available when needed.",
                GroupName = "System Services",
                Icon = MaterialIcons.Vpn,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\RasMan" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-remote-access-auto",
            Display = new()
            {
                Name = "Remote Access Auto Connection Manager",
                Description = "Automatically connects to remote networks when programs reference remote resources. Safe to disable if you don't use auto-connect VPN features",
                GroupName = "System Services",
                Icon = MaterialIcons.NetworkOff,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\RasAuto" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-remote-desktop-services",
            Display = new()
            {
                Name = "Remote Desktop Services",
                Description = "Allows users to connect interactively to a remote computer. Set to Manual to reduce background activity while keeping Remote Desktop available.",
                GroupName = "System Services",
                Icon = MaterialIcons.RemoteDesktop,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\TermService" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-remote-desktop-configuration",
            Display = new()
            {
                Name = "Remote Desktop Configuration",
                Description = "Manages Remote Desktop Services and Remote Desktop related configurations. Set to Manual to reduce background activity while keeping Remote Desktop available",
                GroupName = "System Services",
                Icon = MaterialIcons.MonitorShare,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SessionEnv" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-remote-desktop-port-redirector",
            Display = new()
            {
                Name = "Remote Desktop Services UserMode Port Redirector",
                Description = "Allows local device redirection for Remote Desktop connections. Safe to disable if you don't need to share local devices during Remote Desktop sessions",
                GroupName = "System Services",
                Icon = MaterialIcons.TransitConnectionVariant,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\UmRdpService" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-xbox-auth-manager",
            Display = new()
            {
                Name = "Xbox Live Auth Manager",
                Description = "Provides authentication and authorization services for Xbox Live. Safe to disable if you don't use Xbox Game Pass, Microsoft Store games, or Xbox features",
                GroupName = "System Services",
                Icon = MaterialIcons.MicrosoftXbox,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\XblAuthManager" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Warning = "Disabling will prevent Xbox Game Pass and Microsoft Store games from working",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-xbox-game-save",
            Display = new()
            {
                Name = "Xbox Live Game Save",
                Description = "Syncs game saves to Xbox Live cloud. Only needed for Xbox Game Pass and Microsoft Store games with cloud save features",
                GroupName = "System Services",
                Icon = MaterialIcons.CloudUploadOutline,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\XblGameSave" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-xbox-networking",
            Display = new()
            {
                Name = "Xbox Live Networking Service",
                Description = "Supports Xbox Live multiplayer networking. Required for Xbox multiplayer gaming but not needed for Steam/Epic/other gaming platforms",
                GroupName = "System Services",
                Icon = MaterialIcons.NetworkOutline,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\XboxNetApiSvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-biometric-service",
            Display = new()
            {
                Name = "Windows Biometric Service",
                Description = "Enables fingerprint and facial recognition login via Windows Hello. Safe to disable on desktop systems without biometric hardware",
                GroupName = "System Services",
                Icon = MaterialIcons.Fingerprint,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WbioSrvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-touch-keyboard-service",
            Display = new()
            {
                Name = "Touch Keyboard and Handwriting Panel Service",
                Description = "Manages the Windows Input Experience including touch keyboard, pen/stylus input, handwriting panel, emoji panel (Win+.), and Xbox controller keyboard. Disabling will break all virtual/software keyboard input but is safe on desktop systems without touchscreen, pen, or gamepad",
                GroupName = "System Services",
                Icon = MaterialIcons.KeyboardOutline,
                AddedInVersion = "26.04.03",
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\TabletInputService" }, "Start", RegistryValueKind.DWord) { LockWhenValue = 4 },
                new RegTarget("IsInputAppPreloadEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\input" }, "IsInputAppPreloadEnabled", RegistryValueKind.DWord) { ApplyOnly = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_DisabledRecommended",
                    Roles = new[] { StateRole.Recommended },
                    Effects = new Effect[] { new ScriptEffect(@"if([Environment]::OSVersion.Version.Build -ge 22000 -and -not(Get-WinUserLanguageList|?{$_.LanguageTag-match'^(zh|ja|ko)'})){$f='C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\TextInputHost.exe'; $o=$f-replace'\.exe$','.old.exe'; if(Test-Path $f){takeown /f $f /a | Out-Null; icacls $f /grant Administrators:F | Out-Null; if(Test-Path $o){Remove-Item $o -Force}; Rename-Item $f $o -Force}; Stop-Process -Name TextInputHost -Force -ErrorAction SilentlyContinue}", RunContext.System) },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Start"] = Of(4),
                        ["IsInputAppPreloadEnabled"] = Of(0),
                    },
                },
                new SettingState
                {
                    Label = "ServiceOption_Manual",
                    Roles = new[] { StateRole.WindowsDefault },
                    Effects = new Effect[] { new ScriptEffect(@"$f='C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\TextInputHost.exe'; $o=$f-replace'\.exe$','.old.exe'; if(Test-Path $o){if(Test-Path $f){Remove-Item $f -Force}; Rename-Item $o $f -Force}; Start-Process $f -ErrorAction SilentlyContinue", RunContext.System) },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Start"] = Of(3).OrAbsent(),
                        ["IsInputAppPreloadEnabled"] = Of(1),
                    },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Effects = new Effect[] { new ScriptEffect(@"$f='C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\TextInputHost.exe'; $o=$f-replace'\.exe$','.old.exe'; if(Test-Path $o){if(Test-Path $f){Remove-Item $f -Force}; Rename-Item $o $f -Force}; Start-Process $f -ErrorAction SilentlyContinue", RunContext.System) },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Start"] = Of(2),
                        ["IsInputAppPreloadEnabled"] = Of(1),
                    },
                },
            },
        },
        new()
        {
            Id = "gaming-telephony-service",
            Display = new()
            {
                Name = "Telephony Service",
                Description = "Manages telephony (TAPI) for Phone Link audio relay, modems, fax, and VoIP softphones. Leave at Manual (Windows default) unless you use no telephony software",
                GroupName = "System Services",
                Icon = MaterialIcons.PhoneClassic,
                AddedInVersion = "26.05.18",
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\TapiSrv" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Warning = "Disabling Telephony breaks Phone Link audio relay, fax software, dial-up modems, and VoIP softphones (e.g. 3CX, Cisco Jabber).",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-sensor-monitoring-service",
            Display = new()
            {
                Name = "Sensor Monitoring Service",
                Description = "Monitors various sensors like ambient light and orientation. Safe to disable on desktop systems without sensor hardware",
                GroupName = "System Services",
                Icon = MaterialIcons.Radar,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SensrSvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-sensor-data-service",
            Display = new()
            {
                Name = "Sensor Data Service",
                Description = "Delivers data from a variety of sensors to applications. Safe to disable on desktop systems without sensor hardware",
                GroupName = "System Services",
                Icon = MaterialIcons.ChartBox,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SensorDataService" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_Disabled",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_ManualRecommended",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-ai-fabric-service",
            Display = new()
            {
                Name = "Windows AI Fabric Service",
                Description = "Windows AI Fabric Service (WSAIFabricSvc) manages AI workloads. Disable if you don't use Windows AI features",
                GroupName = "System Services",
                Icon = MaterialIcons.Robot,
                AddedInVersion = "26.04.10",
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WSAIFabricSvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "ServiceOption_DisabledRecommended",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = "ServiceOption_Manual",
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3) },
                },
                new SettingState
                {
                    Label = "ServiceOption_Automatic",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "gaming-task-compatibility-appraiser",
            Display = new()
            {
                Name = "Microsoft Compatibility Appraiser Task",
                Description = "Collects program compatibility telemetry for Windows upgrades. Works alongside the Connected User Experiences and Telemetry Service. Disable to reduce telemetry and background system activity",
                GroupName = "Scheduled Tasks",
                Icon = MaterialIcons.FileDocumentCheck,
            },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser") },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(false) },
                },
            },
        },
        new()
        {
            Id = "gaming-task-program-data-updater",
            Display = new()
            {
                Name = "Program Data Updater Task",
                Description = "Updates the program compatibility database with information about installed applications. Disable to reduce telemetry collection",
                GroupName = "Scheduled Tasks",
                Icon = MaterialIcons.DatabaseSync,
            },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Application Experience\ProgramDataUpdater") },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(false) },
                },
            },
        },
        new()
        {
            Id = "gaming-task-ceip-consolidator",
            Display = new()
            {
                Name = "Customer Experience Improvement Program Consolidator",
                Description = "Consolidates and uploads usage data as part of the Customer Experience Improvement Program. Works with the Connected User Experiences and Telemetry Service. Disable to improve privacy",
                GroupName = "Scheduled Tasks",
                Icon = MaterialIcons.ChartLine,
            },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator") },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(false) },
                },
            },
        },
        new()
        {
            Id = "gaming-task-usb-ceip",
            Display = new()
            {
                Name = "USB CEIP Task",
                Description = "Collects USB device-related telemetry for the Customer Experience Improvement Program. Disable to reduce telemetry",
                GroupName = "Scheduled Tasks",
                Icon = MaterialIcons.Usb,
            },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip") },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(false) },
                },
            },
        },
        new()
        {
            Id = "gaming-task-disk-diagnostic",
            Display = new()
            {
                Name = "Disk Diagnostic Data Collector Task",
                Description = "Collects disk diagnostic information and S.M.A.R.T. data for Microsoft. Disable to reduce background disk activity and telemetry",
                GroupName = "Scheduled Tasks",
                Icon = MaterialIcons.Harddisk,
            },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector") },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(false) },
                },
            },
        },
        new()
        {
            Id = "gaming-task-feedback-dmclient",
            Display = new()
            {
                Name = "Feedback DmClient Task",
                Description = "Collects feedback and diagnostic data for Microsoft. Disable to improve privacy and reduce telemetry",
                GroupName = "Scheduled Tasks",
                Icon = MaterialIcons.MessageAlert,
            },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Feedback\Siuf\DmClient") },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(false) },
                },
            },
        },
        new()
        {
            Id = "gaming-task-feedback-dmclient-download",
            Display = new()
            {
                Name = "Feedback DmClient Scenario Download Task",
                Description = "Downloads feedback scenarios and configuration data from Microsoft. Disable to reduce telemetry and network activity",
                GroupName = "Scheduled Tasks",
                Icon = MaterialIcons.Download,
            },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload") },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(false) },
                },
            },
        },
        new()
        {
            Id = "gaming-task-error-reporting-queue",
            Display = new()
            {
                Name = "Windows Error Reporting Queue Task",
                Description = "Queues crash reports and error data to send to Microsoft. Works alongside the Windows Error Reporting Service. Disable both to prevent crash data collection",
                GroupName = "Scheduled Tasks",
                Icon = MaterialIcons.AlertOctagon,
            },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Windows Error Reporting\QueueReporting") },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(false) },
                },
            },
        },
        new()
        {
            Id = "gaming-task-sqm",
            Display = new()
            {
                Name = "Software Quality Metrics Task",
                Description = "Collects software quality metrics and reliability data for Microsoft telemetry. Disable to improve privacy",
                GroupName = "Scheduled Tasks",
                Icon = MaterialIcons.ChartBar,
            },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\PI\Sqm-Tasks") },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(false) },
                },
            },
        },
        new()
        {
            Id = "gaming-task-mare-backup",
            Display = new()
            {
                Name = "MAR (Malicious Software Removal) Backup Task",
                Description = "Backs up Microsoft Assisted Recovery data. Disable to reduce background system activity",
                GroupName = "Scheduled Tasks",
                Icon = MaterialIcons.BackupRestore,
            },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Application Experience\MareBackup") },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(false) },
                },
            },
        },
        new()
        {
            Id = "gaming-task-startup-app",
            Display = new()
            {
                Name = "Startup App Task",
                Description = "Tracks and monitors startup applications for telemetry and diagnostics. Disable to reduce telemetry",
                GroupName = "Scheduled Tasks",
                Icon = MaterialIcons.RocketLaunch,
            },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Application Experience\StartupAppTask") },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(false) },
                },
            },
        },
        new()
        {
            Id = "gaming-task-maps-update",
            Display = new()
            {
                Name = "Maps Update Task",
                Description = "Updates offline maps data for the Windows Maps app. Disable if you don't use the Maps app to save bandwidth and storage",
                GroupName = "Scheduled Tasks",
                Icon = MaterialIcons.MapOutline,
            },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Maps\MapsUpdateTask") },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(false) },
                },
            },
        },
        new()
        {
            Id = "gaming-task-autochk-proxy",
            Display = new()
            {
                Name = "AutoChk Proxy Task",
                Description = "Performs disk checking operations and collects diagnostic data. Consider keeping enabled for disk health monitoring",
                GroupName = "Scheduled Tasks",
                Icon = MaterialIcons.HarddiskPlus,
            },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Autochk\Proxy") },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(false) },
                },
            },
        },
        new()
        {
            Id = "gaming-task-family-safety",
            Display = new()
            {
                Name = "Family Safety Monitor Task",
                Description = "Monitors family safety settings and usage. Disable if you don't use family safety features",
                GroupName = "Scheduled Tasks",
                Icon = MaterialIcons.AccountSupervisor,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Shell\FamilySafetyMonitor") },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(false) },
                },
            },
        },
        new()
        {
            Id = "gaming-task-power-efficiency",
            Display = new()
            {
                Name = "Power Efficiency Diagnostics Task",
                Description = "Analyzes system power consumption and collects energy efficiency data. Disable to reduce telemetry and background analysis",
                GroupName = "Scheduled Tasks",
                Icon = MaterialIcons.LightningBolt,
            },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Power Efficiency Diagnostics\AnalyzeSystem") },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(false) },
                },
            },
        },
        new()
        {
            Id = "gaming-task-windows-ai",
            Display = new()
            {
                Name = "Windows AI Tasks",
                Description = "Windows AI scheduled tasks including Recall configuration. Disable to prevent AI features from running in the background",
                GroupName = "Scheduled Tasks",
                Icon = MaterialIcons.Robot,
                AddedInVersion = "26.04.10",
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new TaskTarget("Task", @"\Microsoft\Windows\WindowsAI\RecallConfiguration"),
                new TaskTarget("Task2", @"\Microsoft\Windows\WindowsAI\RecallPipeline"),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true), ["Task2"] = Of(true) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(false), ["Task2"] = Of(false) },
                },
            },
        },
        new()
        {
            Id = "gaming-task-office-actions-server",
            Display = new()
            {
                Name = "Office Actions Server Task",
                Description = "Office AI Actions Server scheduled task. Disable to prevent Office AI from running in the background",
                GroupName = "Scheduled Tasks",
                Icon = MaterialIcons.CalendarClock,
                AddedInVersion = "26.04.10",
            },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Office\Office Actions Server") },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(false) },
                },
            },
        },
        new()
        {
            Id = "visual-effects-mode",
            Display = new()
            {
                Name = "Visual Effects",
                Description = "Choose how Windows displays visual effects",
                GroupName = "Visual Effects",
                Icon = MaterialIcons.MonitorEye,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("VisualFXSetting", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" }, "VisualFXSetting", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Let Windows choose what's best for my computer",
                    Roles = new[] { StateRole.WindowsDefault },
                    Controls = new Dictionary<string, string> { ["ui-effects"] = "Disabled", ["window-animation"] = "Disabled", ["taskbar-animations"] = "Disabled", ["enable-peek"] = "Enabled", ["menu-animation"] = "Disabled", ["fade-tooltip"] = "Disabled", ["fade-menu-items"] = "Disabled", ["taskbar-thumbnails"] = "Enabled", ["mouse-shadow"] = "Disabled", ["window-shadows"] = "Disabled", ["show-thumbnails"] = "Enabled", ["translucent-selection"] = "Enabled", ["drag-full-windows"] = "Enabled", ["combo-box-animation"] = "Disabled", ["font-smoothing"] = "Enabled", ["smooth-scroll-listboxes"] = "Enabled", ["drop-shadows"] = "Disabled" },
                    Set = new Dictionary<string, StateValue> { ["VisualFXSetting"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Adjust for best appearance",
                    Controls = new Dictionary<string, string> { ["ui-effects"] = "Enabled", ["window-animation"] = "Enabled", ["taskbar-animations"] = "Enabled", ["enable-peek"] = "Enabled", ["menu-animation"] = "Enabled", ["fade-tooltip"] = "Enabled", ["fade-menu-items"] = "Enabled", ["taskbar-thumbnails"] = "Enabled", ["mouse-shadow"] = "Enabled", ["window-shadows"] = "Enabled", ["show-thumbnails"] = "Enabled", ["translucent-selection"] = "Enabled", ["drag-full-windows"] = "Enabled", ["combo-box-animation"] = "Enabled", ["font-smoothing"] = "Enabled", ["smooth-scroll-listboxes"] = "Enabled", ["drop-shadows"] = "Enabled" },
                    Set = new Dictionary<string, StateValue> { ["VisualFXSetting"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Adjust for best performance",
                    Controls = new Dictionary<string, string> { ["ui-effects"] = "Disabled", ["window-animation"] = "Disabled", ["taskbar-animations"] = "Disabled", ["enable-peek"] = "Disabled", ["menu-animation"] = "Disabled", ["fade-tooltip"] = "Disabled", ["fade-menu-items"] = "Disabled", ["taskbar-thumbnails"] = "Disabled", ["mouse-shadow"] = "Disabled", ["window-shadows"] = "Disabled", ["show-thumbnails"] = "Disabled", ["translucent-selection"] = "Disabled", ["drag-full-windows"] = "Disabled", ["combo-box-animation"] = "Disabled", ["font-smoothing"] = "Disabled", ["smooth-scroll-listboxes"] = "Disabled", ["drop-shadows"] = "Disabled" },
                    Set = new Dictionary<string, StateValue> { ["VisualFXSetting"] = Of(2) },
                },
                new SettingState
                {
                    Label = "Custom",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["VisualFXSetting"] = Of(3) },
                },
            },
        },
        new()
        {
            Id = "ui-effects",
            Display = new()
            {
                Name = "Animate controls and elements inside windows",
                Description = "Enables animation effects for controls and UI elements",
                GroupName = "Visual Effects",
                Icon = MaterialIcons.Animation,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("UserPreferencesMask", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "UserPreferencesMask", RegistryValueKind.Binary) { ByteIndex = 4, BitMask = 0x02 } },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "window-animation",
            Display = new()
            {
                Name = "Animate windows when minimizing and maximizing",
                Description = "Shows smooth animation when windows are minimized or maximized",
                GroupName = "Visual Effects",
                Icon = MaterialIcons.WindowRestore,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("MinAnimate", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics" }, "MinAnimate", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["MinAnimate"] = Of("1") },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MinAnimate"] = Of("0") },
                },
            },
        },
        new()
        {
            Id = "taskbar-animations",
            Display = new()
            {
                Name = "Animations in the taskbar",
                Description = "Controls taskbar animation effects for opening, closing, and switching windows",
                GroupName = "Visual Effects",
                Icon = MaterialIcons.DockBottom,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("TaskbarAnimations", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "TaskbarAnimations", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TaskbarAnimations"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["TaskbarAnimations"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "enable-peek",
            Display = new()
            {
                Name = "Enable Peek",
                Description = "Allows peeking at desktop when hovering over Show Desktop button",
                GroupName = "Visual Effects",
                Icon = MaterialIcons.MonitorEye,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("EnableAeroPeek", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM" }, "EnableAeroPeek", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["EnableAeroPeek"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["EnableAeroPeek"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "menu-animation",
            Display = new()
            {
                Name = "Fade or slide menus into view",
                Description = "Animates menus when they appear using fade or slide effects",
                GroupName = "Visual Effects",
                Icon = MaterialIcons.MenuOpen,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("UserPreferencesMask", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "UserPreferencesMask", RegistryValueKind.Binary) { ByteIndex = 0, BitMask = 0x02 } },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "fade-tooltip",
            Display = new()
            {
                Name = "Fade or slide ToolTips into view",
                Description = "Animates tooltips when they appear using fade or slide effects",
                GroupName = "Visual Effects",
                Icon = MaterialIcons.TooltipText,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("UserPreferencesMask", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "UserPreferencesMask", RegistryValueKind.Binary) { ByteIndex = 1, BitMask = 0x08 } },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "fade-menu-items",
            Display = new()
            {
                Name = "Fade out menu items after clicking",
                Description = "Fades menu items after selection before closing the menu",
                GroupName = "Visual Effects",
                Icon = FluentIcons.SlideTextCursor,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("UserPreferencesMask", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "UserPreferencesMask", RegistryValueKind.Binary) { ByteIndex = 1, BitMask = 0x04 } },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "taskbar-thumbnails",
            Display = new()
            {
                Name = "Save taskbar thumbnail previews",
                Description = "Saves thumbnail previews of taskbar windows for faster display",
                GroupName = "Visual Effects",
                Icon = FluentIcons.ImageMultiple,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("AlwaysHibernateThumbnails", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM" }, "AlwaysHibernateThumbnails", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["AlwaysHibernateThumbnails"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AlwaysHibernateThumbnails"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "mouse-shadow",
            Display = new()
            {
                Name = "Show shadows under mouse pointer",
                Description = "Displays shadow effect underneath the mouse cursor",
                GroupName = "Visual Effects",
                Icon = MaterialIcons.CursorDefault,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("UserPreferencesMask", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "UserPreferencesMask", RegistryValueKind.Binary) { ByteIndex = 1, BitMask = 0x20 } },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "window-shadows",
            Display = new()
            {
                Name = "Show shadows under windows",
                Description = "Displays shadow effects underneath windows",
                GroupName = "Visual Effects",
                Icon = MaterialIcons.BoxShadow,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("UserPreferencesMask", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "UserPreferencesMask", RegistryValueKind.Binary) { ByteIndex = 2, BitMask = 0x04 } },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "show-thumbnails",
            Display = new()
            {
                Name = "Show thumbnails instead of icons",
                Description = "Displays image and document previews instead of generic file icons",
                GroupName = "Visual Effects",
                Icon = FluentIcons.ImageStack,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("IconsOnly", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "IconsOnly", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["IconsOnly"] = Of(0) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["IconsOnly"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "translucent-selection",
            Display = new()
            {
                Name = "Show translucent selection rectangle",
                Description = "Display a semi-transparent selection box when dragging to select multiple files or items",
                GroupName = "Visual Effects",
                Icon = MaterialIcons.Select,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("ListviewAlphaSelect", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "ListviewAlphaSelect", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ListviewAlphaSelect"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ListviewAlphaSelect"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "drag-full-windows",
            Display = new()
            {
                Name = "Show window contents while dragging",
                Description = "Displays window contents when dragging instead of just an outline",
                GroupName = "Visual Effects",
                Icon = MaterialIcons.SelectionDrag,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("DragFullWindows", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "DragFullWindows", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DragFullWindows"] = Of("1") },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DragFullWindows"] = Of("0") },
                },
            },
        },
        new()
        {
            Id = "combo-box-animation",
            Display = new()
            {
                Name = "Slide open combo boxes",
                Description = "Animates combo boxes when they open with a sliding effect",
                GroupName = "Visual Effects",
                Icon = MaterialIcons.FormDropdown,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("UserPreferencesMask", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "UserPreferencesMask", RegistryValueKind.Binary) { ByteIndex = 0, BitMask = 0x04 } },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "font-smoothing",
            Display = new()
            {
                Name = "Smooth edges of screen fonts",
                Description = "Apply anti-aliasing to text for smoother, more readable fonts on screen",
                GroupName = "Visual Effects",
                Icon = MaterialIcons.FormatSize,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("FontSmoothing", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "FontSmoothing", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["FontSmoothing"] = Of("2") },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["FontSmoothing"] = Of("0") },
                },
            },
        },
        new()
        {
            Id = "smooth-scroll-listboxes",
            Display = new()
            {
                Name = "Smooth-scroll list boxes",
                Description = "Enables smooth scrolling in list boxes instead of jumping",
                GroupName = "Visual Effects",
                Icon = MaterialIcons.ListBox,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("UserPreferencesMask", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "UserPreferencesMask", RegistryValueKind.Binary) { ByteIndex = 0, BitMask = 0x08 } },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "drop-shadows",
            Display = new()
            {
                Name = "Use drop shadows for icon labels on the desktop",
                Description = "Add shadow effects behind desktop icon text to improve readability against backgrounds",
                GroupName = "Visual Effects",
                Icon = MaterialIcons.TextShadow,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("ListviewShadow", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "ListviewShadow", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ListviewShadow"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ListviewShadow"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "gaming-narrator-hotkey",
            Display = new()
            {
                Name = "Narrator Win+Ctrl+Enter Hotkey",
                Description = "Enable the Win+Ctrl+Enter keyboard shortcut to quickly launch Windows Narrator screen reader",
                GroupName = "Accessibility",
                Icon = MaterialIcons.AccountVoice,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("WinEnterLaunchEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Narrator\NoRoam" }, "WinEnterLaunchEnabled", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["WinEnterLaunchEnabled"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["WinEnterLaunchEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "accessibility-stickykeys-hotkey",
            Display = new()
            {
                Name = "StickyKeys Hotkey (Shift×5)",
                Description = "Enable the keyboard shortcut to activate StickyKeys by pressing the Shift key five times",
                GroupName = "Accessibility",
                Icon = MaterialIcons.AppleKeyboardShift,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("Flags", new[] { @"HKEY_CURRENT_USER\Control Panel\Accessibility\StickyKeys" }, "Flags", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Flags"] = Of("510") },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Flags"] = Of("2") },
                },
            },
        },
        new()
        {
            Id = "accessibility-filterkeys-hotkey",
            Display = new()
            {
                Name = "FilterKeys Hotkey (Right Shift 8s)",
                Description = "Enable the keyboard shortcut to activate FilterKeys by holding the right Shift key for 8 seconds",
                GroupName = "Accessibility",
                Icon = MaterialIcons.KeyboardOutline,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("Flags", new[] { @"HKEY_CURRENT_USER\Control Panel\Accessibility\Keyboard Response" }, "Flags", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Flags"] = Of("126") },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Flags"] = Of("2") },
                },
            },
        },
        new()
        {
            Id = "accessibility-togglekeys-hotkey",
            Display = new()
            {
                Name = "ToggleKeys Hotkey (Num Lock 5s)",
                Description = "Enable the keyboard shortcut to activate ToggleKeys by holding Num Lock for 5 seconds, which plays sounds when Caps/Num/Scroll Lock are pressed",
                GroupName = "Accessibility",
                Icon = MaterialIcons.Numeric,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("Flags", new[] { @"HKEY_CURRENT_USER\Control Panel\Accessibility\ToggleKeys" }, "Flags", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Flags"] = Of("62") },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Flags"] = Of("34") },
                },
            },
        },
        new()
        {
            Id = "accessibility-mousekeys-hotkey",
            Display = new()
            {
                Name = "MouseKeys Hotkey (Alt+Shift+NumLock)",
                Description = "Enable the keyboard shortcut to activate MouseKeys, which allows using the numeric keypad to control the mouse pointer",
                GroupName = "Accessibility",
                Icon = MaterialIcons.MouseVariant,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("Flags", new[] { @"HKEY_CURRENT_USER\Control Panel\Accessibility\MouseKeys" }, "Flags", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Flags"] = Of("126") },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Flags"] = Of("130") },
                },
            },
        },
        new()
        {
            Id = "accessibility-highcontrast-hotkey",
            Display = new()
            {
                Name = "High Contrast Hotkey (Alt+Shift+PrtScn)",
                Description = "Enable the keyboard shortcut to activate High Contrast mode by pressing Left Alt + Left Shift + Print Screen",
                GroupName = "Accessibility",
                Icon = MaterialIcons.ContrastCircle,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("Flags", new[] { @"HKEY_CURRENT_USER\Control Panel\Accessibility\HighContrast" }, "Flags", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Flags"] = Of("126") },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Flags"] = Of("4194") },
                },
            },
        },
        new()
        {
            Id = "system-restore-protection",
            Display = new()
            {
                Name = "System Protection (Restore Points)",
                Description = "Allow Windows to automatically create restore points for the C: drive, making it possible to undo system changes if something goes wrong",
                Icon = MaterialIcons.History,
                AddedInVersion = "26.05.13",
                IsSubjectivePreference = true,
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Effects = new Effect[] { new ScriptEffect(@"Enable-ComputerRestore -Drive 'C:\'", RunContext.System) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Effects = new Effect[] { new ScriptEffect(@"Disable-ComputerRestore -Drive 'C:\'", RunContext.System) },
                },
            },
            Detector = new SystemRestoreDetector("Enabled", "Disabled"),
        },
    };
}
