using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using static Winhance.Core.Features.Common.Catalog.StateValue;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Optimize.Catalogs;

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
                Name = LocKey.Setting.GamingGameMode.Name,
                Description = LocKey.Setting.GamingGameMode.Description,
                Icon = FluentIcons.TopSpeed,
            },
            Targets = new Target[] { new RegTarget("AutoGameModeEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\GameBar" }, "AutoGameModeEnabled", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AutoGameModeEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingPerformanceExplorerMousePrecision.Name,
                Description = LocKey.Setting.GamingPerformanceExplorerMousePrecision.Description,
                Icon = MaterialIcons.Mouse,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("MouseSpeed", new[] { @"HKEY_CURRENT_USER\Control Panel\Mouse" }, "MouseSpeed", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["MouseSpeed"] = Of("1") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingPerformanceMouseHoverTime.Name,
                Description = LocKey.Setting.GamingPerformanceMouseHoverTime.Description,
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
                    Label = LocKey.Setting.GamingPerformanceMouseHoverTime.Option0,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["MouseHoverTime"] = Of("1") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingPerformanceMouseHoverTime.Option1,
                    Set = new Dictionary<string, StateValue> { ["MouseHoverTime"] = Of("10") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingPerformanceMouseHoverTime.Option2,
                    Set = new Dictionary<string, StateValue> { ["MouseHoverTime"] = Of("50") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingPerformanceMouseHoverTime.Option3,
                    Set = new Dictionary<string, StateValue> { ["MouseHoverTime"] = Of("100") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingPerformanceMouseHoverTime.Option4,
                    Set = new Dictionary<string, StateValue> { ["MouseHoverTime"] = Of("200") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingPerformanceMouseHoverTime.Option5,
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
                Name = LocKey.Setting.GamingPerformanceAutostartDelay.Name,
                Description = LocKey.Setting.GamingPerformanceAutostartDelay.Description,
                Icon = MaterialIcons.ClockStart,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("StartupDelayInMSec", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize" }, "StartupDelayInMSec", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["StartupDelayInMSec"] = Of(10000) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["StartupDelayInMSec"] = Of(0) },
                    ResetSet = new Dictionary<string, StateValue> { ["StartupDelayInMSec"] = Absent },
                },
            },
        },
        new()
        {
            Id = "gaming-background-apps",
            Display = new()
            {
                Name = LocKey.Setting.GamingBackgroundApps.Name,
                Description = LocKey.Setting.GamingBackgroundApps.Description,
                Icon = MaterialIcons.Apps,
                AddedInVersion = "26.04.08",
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("LetAppsRunInBackground", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\AppPrivacy" }, "LetAppsRunInBackground", RegistryValueKind.DWord) { IsGroupPolicy = true } },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.GamingBackgroundApps.Option0,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["LetAppsRunInBackground"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingBackgroundApps.Option1,
                    Set = new Dictionary<string, StateValue> { ["LetAppsRunInBackground"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingBackgroundApps.Option2,
                    Warning = LocKey.Setting.GamingBackgroundApps.OptionWarning2,
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
                Name = LocKey.Setting.GamingStorageSense.Name,
                Description = LocKey.Setting.GamingStorageSense.Description,
                Icon = MaterialIcons.Harddisk,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("AllowStorageSenseGlobal", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Policies\Microsoft\Windows\StorageSense", @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\StorageSense" }, "AllowStorageSenseGlobal", RegistryValueKind.DWord) { IsGroupPolicy = true } },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AllowStorageSenseGlobal"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingPerformanceExplorerSearch.Name,
                Description = LocKey.Setting.GamingPerformanceExplorerSearch.Description,
                Icon = MaterialIcons.FolderSearch,
            },
            Targets = new Target[] { new RegTarget("WholeFileSystem", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Search\Preferences" }, "WholeFileSystem", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["WholeFileSystem"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["WholeFileSystem"] = Of(0) },
                    ResetSet = new Dictionary<string, StateValue> { ["WholeFileSystem"] = Absent },
                },
            },
        },
        new()
        {
            Id = "gaming-performance-search-webview2",
            Display = new()
            {
                Name = LocKey.Setting.GamingPerformanceSearchWebview2.Name,
                Description = LocKey.Setting.GamingPerformanceSearchWebview2.Description,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["EnabledState"] = Of(2).OrAbsent(),
                        ["EnabledStateOptions"] = Absent,
                        ["Variant"] = Absent,
                        ["VariantPayload"] = Absent,
                        ["VariantPayloadKind"] = Absent,
                    },
                    ResetSet = new Dictionary<string, StateValue> { ["EnabledState"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingPerformanceWallpaperCompression.Name,
                Description = LocKey.Setting.GamingPerformanceWallpaperCompression.Description,
                Icon = FluentIcons.ResizeImage,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[] { new RegTarget("JPEGImportQuality", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "JPEGImportQuality", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["JPEGImportQuality"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingPerformanceExplorerMenuShowDelay.Name,
                Description = LocKey.Setting.GamingPerformanceExplorerMenuShowDelay.Description,
                Icon = MaterialIcons.MenuOpen,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("MenuShowDelay", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "MenuShowDelay", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["MenuShowDelay"] = Of("400") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingExplorerAltTabFilter.Name,
                Description = LocKey.Setting.GamingExplorerAltTabFilter.Description,
                Icon = MaterialIcons.ViewGrid,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("MultiTaskingAltTabFilter", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "MultiTaskingAltTabFilter", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["MultiTaskingAltTabFilter"] = Of(3) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MultiTaskingAltTabFilter"] = Of(0).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "gaming-win32-priority",
            Display = new()
            {
                Name = LocKey.Setting.GamingWin32Priority.Name,
                Description = LocKey.Setting.GamingWin32Priority.Description,
                GroupName = LocKey.SettingGroup.Processor,
                Icon = MaterialIcons.Application,
            },
            Targets = new Target[] { new RegTarget("Win32PrioritySeparation", new[] { @"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\PriorityControl" }, "Win32PrioritySeparation", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.GamingWin32Priority.Option0,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    // Clean installs ship Win32PrioritySeparation=2 (all three clean-install fixtures),
                    // which System Properties renders as "Programs"; 0x26 (38) is the dialog's own write
                    // when Programs is picked. Both mean Programs. Write payload stays 38 (first value).
                    Set = new Dictionary<string, StateValue> { ["Win32PrioritySeparation"] = OneOf(38, 2) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingWin32Priority.Option1,
                    Set = new Dictionary<string, StateValue> { ["Win32PrioritySeparation"] = Of(24) },
                },
            },
        },
        new()
        {
            Id = "gaming-system-responsiveness",
            Display = new()
            {
                Name = LocKey.Setting.GamingSystemResponsiveness.Name,
                Description = LocKey.Setting.GamingSystemResponsiveness.Description,
                GroupName = LocKey.SettingGroup.Processor,
                Icon = MaterialIcons.Speedometer,
            },
            Targets = new Target[] { new RegTarget("SystemResponsiveness", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" }, "SystemResponsiveness", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["SystemResponsiveness"] = Of(10) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingCpuPriority.Name,
                Description = LocKey.Setting.GamingCpuPriority.Description,
                GroupName = LocKey.SettingGroup.Processor,
                Icon = MaterialIcons.Chip,
            },
            Targets = new Target[] { new RegTarget("Priority", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games" }, "Priority", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Priority"] = Of(6) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingSchedulingCategory.Name,
                Description = LocKey.Setting.GamingSchedulingCategory.Description,
                GroupName = LocKey.SettingGroup.Processor,
                Icon = MaterialIcons.CalendarClock,
            },
            Targets = new Target[] { new RegTarget("Scheduling Category", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games" }, "Scheduling Category", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Scheduling Category"] = Of("High") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingPerformanceSvchostSplitThreshold.Name,
                Description = LocKey.Setting.GamingPerformanceSvchostSplitThreshold.Description,
                GroupName = LocKey.SettingGroup.Processor,
                Icon = FluentIcons.BranchCompare,
                AddedInVersion = "25.04.03",
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("SvcHostSplitThresholdInKB", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control" }, "SvcHostSplitThresholdInKB", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.GamingPerformanceSvchostSplitThreshold.Option0,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SvcHostSplitThresholdInKB"] = Of(3670016).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingPerformanceSvchostSplitThreshold.Option1,
                    Set = new Dictionary<string, StateValue> { ["SvcHostSplitThresholdInKB"] = Of(4194304) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingPerformanceSvchostSplitThreshold.Option2,
                    Set = new Dictionary<string, StateValue> { ["SvcHostSplitThresholdInKB"] = Of(6291456) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingPerformanceSvchostSplitThreshold.Option3,
                    Set = new Dictionary<string, StateValue> { ["SvcHostSplitThresholdInKB"] = Of(8388608) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingPerformanceSvchostSplitThreshold.Option4,
                    Set = new Dictionary<string, StateValue> { ["SvcHostSplitThresholdInKB"] = Of(12582912) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingPerformanceSvchostSplitThreshold.Option5,
                    Set = new Dictionary<string, StateValue> { ["SvcHostSplitThresholdInKB"] = Of(16777216) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingPerformanceSvchostSplitThreshold.Option6,
                    Set = new Dictionary<string, StateValue> { ["SvcHostSplitThresholdInKB"] = Of(25165824) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingPerformanceSvchostSplitThreshold.Option7,
                    Set = new Dictionary<string, StateValue> { ["SvcHostSplitThresholdInKB"] = Of(33554432) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingPerformanceSvchostSplitThreshold.Option8,
                    Set = new Dictionary<string, StateValue> { ["SvcHostSplitThresholdInKB"] = Of(67108864) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingPerformanceSvchostSplitThreshold.Option9,
                    Set = new Dictionary<string, StateValue> { ["SvcHostSplitThresholdInKB"] = Of(134217728) },
                },
            },
        },
        new()
        {
            Id = "gaming-gpu-priority",
            Display = new()
            {
                Name = LocKey.Setting.GamingGpuPriority.Name,
                Description = LocKey.Setting.GamingGpuPriority.Description,
                GroupName = LocKey.SettingGroup.Graphics,
                Icon = MaterialIcons.Memory,
            },
            Targets = new Target[] { new RegTarget("GPU Priority", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games" }, "GPU Priority", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["GPU Priority"] = Of(8) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingGpuScheduling.Name,
                Description = LocKey.Setting.GamingGpuScheduling.Description,
                GroupName = LocKey.SettingGroup.Graphics,
                Icon = MaterialIcons.ExpansionCard,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("HwSchMode", new[] { @"HKEY_LOCAL_MACHINE\System\CurrentControlSet\Control\GraphicsDrivers" }, "HwSchMode", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["HwSchMode"] = Of(2).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingDirectxFlipModel.Name,
                Description = LocKey.Setting.GamingDirectxFlipModel.Description,
                GroupName = LocKey.SettingGroup.Graphics,
                Icon = MaterialIcons.ApplicationCog,
            },
            Targets = new Target[] { new RegTarget("DirectXUserGlobalSettings", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences" }, "DirectXUserGlobalSettings", RegistryValueKind.String) { CompositeStringKey = "SwapEffectUpgradeEnable" } },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DirectXUserGlobalSettings"] = Of("1").OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingDirectxVrrOptimizations.Name,
                Description = LocKey.Setting.GamingDirectxVrrOptimizations.Description,
                GroupName = LocKey.SettingGroup.Graphics,
                Icon = MaterialIcons.MonitorShimmer,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("DirectXUserGlobalSettings", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences" }, "DirectXUserGlobalSettings", RegistryValueKind.String) { CompositeStringKey = "VRROptimizeEnable" } },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DirectXUserGlobalSettings"] = Of("1").OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingDirectxAutoHdr.Name,
                Description = LocKey.Setting.GamingDirectxAutoHdr.Description,
                GroupName = LocKey.SettingGroup.Graphics,
                Icon = MaterialIcons.Hdr,
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[] { new RegTarget("DirectXUserGlobalSettings", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences" }, "DirectXUserGlobalSettings", RegistryValueKind.String) { CompositeStringKey = "AutoHDREnable" } },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["DirectXUserGlobalSettings"] = Of("1") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingNvidiaSharpening.Name,
                Description = LocKey.Setting.GamingNvidiaSharpening.Description,
                GroupName = LocKey.SettingGroup.Graphics,
                Icon = MaterialIcons.ImageFilterHdr,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("EnableGR535", new[] { @"HKEY_LOCAL_MACHINE\Software\NVIDIA Corporation\Global\FTS" }, "EnableGR535", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["EnableGR535"] = Of(0) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["EnableGR535"] = Of(1) },
                    ResetSet = new Dictionary<string, StateValue> { ["EnableGR535"] = Absent },
                },
            },
        },
        new()
        {
            Id = "gaming-fullscreen-optimizations",
            Display = new()
            {
                Name = LocKey.Setting.GamingFullscreenOptimizations.Name,
                Description = LocKey.Setting.GamingFullscreenOptimizations.Description,
                GroupName = LocKey.SettingGroup.Graphics,
                Icon = MaterialIcons.MonitorScreenshot,
            },
            Targets = new Target[]
            {
                new RegTarget("GameDVR_FSEBehaviorMode", new[] { @"HKEY_CURRENT_USER\System\GameConfigStore" }, "GameDVR_FSEBehaviorMode", RegistryValueKind.DWord),
                new RegTarget("GameDVR_HonorUserFSEBehaviorMode", new[] { @"HKEY_CURRENT_USER\System\GameConfigStore" }, "GameDVR_HonorUserFSEBehaviorMode", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    // Windows only honors GameDVR_FSEBehaviorMode when GameDVR_HonorUserFSEBehaviorMode is 1.
                    // Clean installs ship FSEBehaviorMode=2 with Honor=0 (verified 2026-07-23), so the Honor
                    // flag is the deciding signal: 0/absent = optimizations active (the Windows default).
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["GameDVR_FSEBehaviorMode"] = OneOf(2, 0).OrAbsent(),
                        ["GameDVR_HonorUserFSEBehaviorMode"] = Of(0).OrAbsent(),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["GameDVR_FSEBehaviorMode"] = Of(2),
                        ["GameDVR_HonorUserFSEBehaviorMode"] = Of(1),
                    },
                },
            },
        },
        new()
        {
            Id = "gaming-performance-desktop-composition",
            Display = new()
            {
                Name = LocKey.Setting.GamingPerformanceDesktopComposition.Name,
                Description = LocKey.Setting.GamingPerformanceDesktopComposition.Description,
                GroupName = LocKey.SettingGroup.Graphics,
                Icon = MaterialIcons.ViewDashboard,
            },
            Targets = new Target[] { new RegTarget("CompositionPolicy", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM" }, "CompositionPolicy", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["CompositionPolicy"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingAutoColorManagement.Name,
                Description = LocKey.Setting.GamingAutoColorManagement.Description,
                GroupName = LocKey.SettingGroup.Graphics,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["AutoColorManagementEnabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingDisableMpo.Name,
                Description = LocKey.Setting.GamingDisableMpo.Description,
                GroupName = LocKey.SettingGroup.Graphics,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["OverlayTestMode"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingDisableAllOverlays.Name,
                Description = LocKey.Setting.GamingDisableAllOverlays.Description,
                GroupName = LocKey.SettingGroup.Graphics,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DisableOverlays"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingDisableMpoMinFps.Name,
                Description = LocKey.Setting.GamingDisableMpoMinFps.Description,
                GroupName = LocKey.SettingGroup.Graphics,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["OverlayMinFPS"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingNetworkThrottling.Name,
                Description = LocKey.Setting.GamingNetworkThrottling.Description,
                GroupName = LocKey.SettingGroup.Network,
                Icon = MaterialIcons.NetworkOffOutline,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("NetworkThrottlingIndex", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" }, "NetworkThrottlingIndex", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["NetworkThrottlingIndex"] = Of(10) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingNagleAlgorithm.Name,
                Description = LocKey.Setting.GamingNagleAlgorithm.Description,
                GroupName = LocKey.SettingGroup.Network,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["TcpAckFrequency"] = Absent,
                        ["TCPNoDelay"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingDnsServer.Name,
                Description = LocKey.Setting.GamingDnsServer.Description,
                GroupName = LocKey.SettingGroup.Network,
                Icon = MaterialIcons.Dns,
                AddedInVersion = "26.04.08",
                IsSubjectivePreference = true,
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.GamingDnsServer.Option0,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Effects = new Effect[]
                    {
                        new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ResetServerAddresses }", RunContext.User),
                        new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }", RunContext.User),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingDnsServer.Option1,
                    Effects = new Effect[]
                    {
                        new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('1.1.1.1','1.0.0.1') }", RunContext.User),
                        new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; $t = '{{dohtemplate}}'; if ($t -and $t -notmatch '^\{\{') { netsh dns add encryption server=1.1.1.1 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null; netsh dns add encryption server=1.0.0.1 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null }", RunContext.User),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingDnsServer.Option2,
                    Effects = new Effect[]
                    {
                        new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('1.1.1.2','1.0.0.2') }", RunContext.User),
                        new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; $t = '{{dohtemplate}}'; if ($t -and $t -notmatch '^\{\{') { netsh dns add encryption server=1.1.1.2 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null; netsh dns add encryption server=1.0.0.2 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null }", RunContext.User),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingDnsServer.Option3,
                    Effects = new Effect[]
                    {
                        new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('1.1.1.3','1.0.0.3') }", RunContext.User),
                        new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; $t = '{{dohtemplate}}'; if ($t -and $t -notmatch '^\{\{') { netsh dns add encryption server=1.1.1.3 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null; netsh dns add encryption server=1.0.0.3 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null }", RunContext.User),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingDnsServer.Option4,
                    Effects = new Effect[]
                    {
                        new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('8.8.8.8','8.8.4.4') }", RunContext.User),
                        new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; $t = '{{dohtemplate}}'; if ($t -and $t -notmatch '^\{\{') { netsh dns add encryption server=8.8.8.8 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null; netsh dns add encryption server=8.8.4.4 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null }", RunContext.User),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingDnsServer.Option5,
                    Effects = new Effect[]
                    {
                        new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('9.9.9.9','149.112.112.112') }", RunContext.User),
                        new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; $t = '{{dohtemplate}}'; if ($t -and $t -notmatch '^\{\{') { netsh dns add encryption server=9.9.9.9 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null; netsh dns add encryption server=149.112.112.112 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null }", RunContext.User),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingDnsServer.Option6,
                    Effects = new Effect[]
                    {
                        new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('208.67.222.222','208.67.220.220') }", RunContext.User),
                        new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; $t = '{{dohtemplate}}'; if ($t -and $t -notmatch '^\{\{') { netsh dns add encryption server=208.67.222.222 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null; netsh dns add encryption server=208.67.220.220 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null }", RunContext.User),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingDnsServer.Option7,
                    Effects = new Effect[]
                    {
                        new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('1.1.1.1','1.0.0.1') }", RunContext.User),
                        new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; $t = 'https://cloudflare-dns.com/dns-query'; if ($t -and $t -notmatch '^\{\{') { netsh dns add encryption server=1.1.1.1 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null; netsh dns add encryption server=1.0.0.1 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null }", RunContext.User),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingDnsServer.Option8,
                    Effects = new Effect[]
                    {
                        new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('8.8.8.8','8.8.4.4') }", RunContext.User),
                        new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; $t = 'https://dns.google/dns-query'; if ($t -and $t -notmatch '^\{\{') { netsh dns add encryption server=8.8.8.8 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null; netsh dns add encryption server=8.8.4.4 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null }", RunContext.User),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingDnsServer.Option9,
                    Effects = new Effect[]
                    {
                        new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('9.9.9.9','149.112.112.112') }", RunContext.User),
                        new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; $t = 'https://dns.quad9.net/dns-query'; if ($t -and $t -notmatch '^\{\{') { netsh dns add encryption server=9.9.9.9 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null; netsh dns add encryption server=149.112.112.112 dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null }", RunContext.User),
                    },
                },
            },
            CustomStateScripts = new[]
            {
                new ScriptEffect(@"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('{{primary}}','{{secondary}}') }", RunContext.User),
                new ScriptEffect(@"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; $t = '{{dohtemplate}}'; if ($t -and $t -notmatch '^\{\{') { netsh dns add encryption server={{primary}} dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null; netsh dns add encryption server={{secondary}} dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null }", RunContext.User),
            },
            Detector = new DnsServerDetector(LocKey.Setting.GamingDnsServer.Option0, new Dictionary<string, LocKey>
            {
                ["1.1.1.1"] = LocKey.Setting.GamingDnsServer.Option1,
                ["1.1.1.2"] = LocKey.Setting.GamingDnsServer.Option2,
                ["1.1.1.3"] = LocKey.Setting.GamingDnsServer.Option3,
                ["8.8.8.8"] = LocKey.Setting.GamingDnsServer.Option4,
                ["9.9.9.9"] = LocKey.Setting.GamingDnsServer.Option5,
                ["208.67.222.222"] = LocKey.Setting.GamingDnsServer.Option6,
            }),
        },
        new()
        {
            Id = "gaming-virtualization-based-security",
            Display = new()
            {
                Name = LocKey.Setting.GamingVirtualizationBasedSecurity.Name,
                Description = LocKey.Setting.GamingVirtualizationBasedSecurity.Description,
                GroupName = LocKey.SettingGroup.Security,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["EnableVirtualizationBasedSecurity"] = Of(1),
                        ["RequirePlatformSecurityFeatures"] = Of(1),
                        ["Locked"] = Of(1).OrAbsent(),
                    },
                    ResetSet = new Dictionary<string, StateValue> { ["Locked"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingMemoryIntegrity.Name,
                Description = LocKey.Setting.GamingMemoryIntegrity.Description,
                GroupName = LocKey.SettingGroup.Security,
                Icon = MaterialIcons.MemoryArrowDown,
                AddedInVersion = "26.04.01",
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            UiParentId = "gaming-virtualization-based-security",
            EnabledWhen = new("gaming-virtualization-based-security", new[] { LocKey.Common.Enabled }),
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Enabled"] = Of(1),
                        ["Locked"] = Of(1).OrAbsent(),
                        ["WasEnabledBy"] = Of(2),
                    },
                    ResetSet = new Dictionary<string, StateValue> { ["Locked"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Links = new[] { new Link("gaming-virtualization-based-security", LinkKind.Requires, LocKey.Common.Enabled) },
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
                Name = LocKey.Setting.GamingXboxGameDvr.Name,
                Description = LocKey.Setting.GamingXboxGameDvr.Description,
                GroupName = LocKey.SettingGroup.Xbox,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["GameDVR_Enabled"] = Of(1),
                        ["AppCaptureEnabled"] = Of(1).OrAbsent(),
                        ["AllowGameDVR"] = Of(1).OrAbsent(),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingGameBarController.Name,
                Description = LocKey.Setting.GamingGameBarController.Description,
                GroupName = LocKey.SettingGroup.Xbox,
                Icon = FluentIcons.XboxControllerError,
            },
            Targets = new Target[] { new RegTarget("UseNexusForGameBarEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\GameBar" }, "UseNexusForGameBarEnabled", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["UseNexusForGameBarEnabled"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingGameBarTips.Name,
                Description = LocKey.Setting.GamingGameBarTips.Description,
                GroupName = LocKey.SettingGroup.Xbox,
                Icon = MaterialIcons.LightbulbOff,
            },
            Targets = new Target[] { new RegTarget("ShowStartupPanel", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\GameBar" }, "ShowStartupPanel", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowStartupPanel"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingPerformanceBackgroundServices.Name,
                Description = LocKey.Setting.GamingPerformanceBackgroundServices.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.Cog,
            },
            Targets = new Target[] { new RegTarget("ServicesPipeTimeout", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control" }, "ServicesPipeTimeout", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["ServicesPipeTimeout"] = Of(30000) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ServicesPipeTimeout"] = Of(60000) },
                    ResetSet = new Dictionary<string, StateValue> { ["ServicesPipeTimeout"] = Absent },
                },
            },
        },
        new()
        {
            Id = "gaming-sysmain-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingSysmainService.Name,
                Description = LocKey.Setting.GamingSysmainService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.Cached,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SysMain" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.GamingSysmainService.Option0,
                    Warning = LocKey.Setting.GamingSysmainService.OptionWarning0,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Manual,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.GamingSysmainService.Option2,
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
                Name = LocKey.Setting.GamingPerformancePrefetch.Name,
                Description = LocKey.Setting.GamingPerformancePrefetch.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.Download,
                IsSubjectivePreference = true,
            },
            UiParentId = "gaming-sysmain-service",
            // Prefetching is the SysMain service's job: with the service off there is nothing for
            // EnablePrefetcher to configure. Manual counts - the service can still be started.
            EnabledWhen = new("gaming-sysmain-service", new[] { LocKey.ServiceOption.Manual, LocKey.Setting.GamingSysmainService.Option2 }),
            Targets = new Target[] { new RegTarget("EnablePrefetcher", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management\PrefetchParameters" }, "EnablePrefetcher", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["EnablePrefetcher"] = Of(3) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    // Deliberately NO Requires on gaming-sysmain-service. A Requires here is a demand to
                    // CHANGE the service, so turning prefetching off would also stop SysMain and ask for a
                    // reboot the user never asked for. The real relationship - prefetching is inert while
                    // the service is off - is a presentation fact, and EnabledWhen above already carries it.
                    Set = new Dictionary<string, StateValue> { ["EnablePrefetcher"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "gaming-windows-search-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingWindowsSearchService.Name,
                Description = LocKey.Setting.GamingWindowsSearchService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.DatabaseSearch,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WSearch" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Warning = LocKey.Setting.GamingWindowsSearchService.OptionWarning0,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
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
                Name = LocKey.Setting.GamingPrintSpoolerService.Name,
                Description = LocKey.Setting.GamingPrintSpoolerService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.Printer,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Spooler" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Manual,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.AutomaticRecommended,
                    Roles = new[] { StateRole.WindowsDefault, StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "gaming-telemetry-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingTelemetryService.Name,
                Description = LocKey.Setting.GamingTelemetryService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.CloudUpload,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\DiagTrack" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
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
                Name = LocKey.Setting.GamingConnectedDevicesPlatformService.Name,
                Description = LocKey.Setting.GamingConnectedDevicesPlatformService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
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
                    Label = LocKey.ServiceOption.Disabled,
                    Warning = LocKey.Setting.GamingConnectedDevicesPlatformService.OptionWarning0,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Warning = LocKey.Setting.GamingConnectedDevicesPlatformService.OptionWarning1,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
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
                Name = LocKey.Setting.GamingCompatibilityAssistantService.Name,
                Description = LocKey.Setting.GamingCompatibilityAssistantService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.ApplicationCog,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\PcaSvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "gaming-error-reporting-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingErrorReportingService.Name,
                Description = LocKey.Setting.GamingErrorReportingService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.AlertOctagon,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WerSvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-geolocation-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingGeolocationService.Name,
                Description = LocKey.Setting.GamingGeolocationService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.MapMarkerOff,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\lfsvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-retail-demo-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingRetailDemoService.Name,
                Description = LocKey.Setting.GamingRetailDemoService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.StorefrontOutline,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\RetailDemo" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-insider-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingInsiderService.Name,
                Description = LocKey.Setting.GamingInsiderService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.TestTube,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\wisvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-phone-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingPhoneService.Name,
                Description = LocKey.Setting.GamingPhoneService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.Cellphone,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\PhoneSvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-wallet-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingWalletService.Name,
                Description = LocKey.Setting.GamingWalletService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.Wallet,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WalletService" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-smart-card-services",
            Display = new()
            {
                Name = LocKey.Setting.GamingSmartCardServices.Name,
                Description = LocKey.Setting.GamingSmartCardServices.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.SmartCard,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SCardSvr", @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\ScDeviceEnum", @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SCPolicySvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-maps-broker-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingMapsBrokerService.Name,
                Description = LocKey.Setting.GamingMapsBrokerService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.MapOutline,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\MapsBroker" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
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
                Name = LocKey.Setting.GamingFaxService.Name,
                Description = LocKey.Setting.GamingFaxService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.Fax,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Fax" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.DisabledRecommended,
                    Roles = new[] { StateRole.Recommended, new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Manual,
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-wmp-network-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingWmpNetworkService.Name,
                Description = LocKey.Setting.GamingWmpNetworkService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.ShareOff,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WMPNetworkSvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.DisabledRecommended,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Manual,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-mixed-reality-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingMixedRealityService.Name,
                Description = LocKey.Setting.GamingMixedRealityService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.VirtualReality,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\MixedRealityOpenXRSvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-mobile-hotspot-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingMobileHotspotService.Name,
                Description = LocKey.Setting.GamingMobileHotspotService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.CellphoneWireless,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\icssvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-sms-router-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingSmsRouterService.Name,
                Description = LocKey.Setting.GamingSmsRouterService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.MessageText,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SmsRouter" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-parental-controls-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingParentalControlsService.Name,
                Description = LocKey.Setting.GamingParentalControlsService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.ShieldAccount,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WpcMonSvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-payments-nfc-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingPaymentsNfcService.Name,
                Description = LocKey.Setting.GamingPaymentsNfcService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.Nfc,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SEMgrSvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-spot-verifier-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingSpotVerifierService.Name,
                Description = LocKey.Setting.GamingSpotVerifierService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.ShieldCheck,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\svsvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-remote-access-manager",
            Display = new()
            {
                Name = LocKey.Setting.GamingRemoteAccessManager.Name,
                Description = LocKey.Setting.GamingRemoteAccessManager.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.Vpn,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\RasMan" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-remote-access-auto",
            Display = new()
            {
                Name = LocKey.Setting.GamingRemoteAccessAuto.Name,
                Description = LocKey.Setting.GamingRemoteAccessAuto.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.NetworkOff,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\RasAuto" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-remote-desktop-services",
            Display = new()
            {
                Name = LocKey.Setting.GamingRemoteDesktopServices.Name,
                Description = LocKey.Setting.GamingRemoteDesktopServices.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.RemoteDesktop,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\TermService" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-remote-desktop-configuration",
            Display = new()
            {
                Name = LocKey.Setting.GamingRemoteDesktopConfiguration.Name,
                Description = LocKey.Setting.GamingRemoteDesktopConfiguration.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.MonitorShare,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SessionEnv" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-remote-desktop-port-redirector",
            Display = new()
            {
                Name = LocKey.Setting.GamingRemoteDesktopPortRedirector.Name,
                Description = LocKey.Setting.GamingRemoteDesktopPortRedirector.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.TransitConnectionVariant,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\UmRdpService" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-xbox-auth-manager",
            Display = new()
            {
                Name = LocKey.Setting.GamingXboxAuthManager.Name,
                Description = LocKey.Setting.GamingXboxAuthManager.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.MicrosoftXbox,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\XblAuthManager" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Warning = LocKey.Setting.GamingXboxAuthManager.OptionWarning0,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-xbox-game-save",
            Display = new()
            {
                Name = LocKey.Setting.GamingXboxGameSave.Name,
                Description = LocKey.Setting.GamingXboxGameSave.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.CloudUploadOutline,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\XblGameSave" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-xbox-networking",
            Display = new()
            {
                Name = LocKey.Setting.GamingXboxNetworking.Name,
                Description = LocKey.Setting.GamingXboxNetworking.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.NetworkOutline,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\XboxNetApiSvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-biometric-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingBiometricService.Name,
                Description = LocKey.Setting.GamingBiometricService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.Fingerprint,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\WbioSrvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-touch-keyboard-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingTouchKeyboardService.Name,
                Description = LocKey.Setting.GamingTouchKeyboardService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
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
                    Label = LocKey.ServiceOption.Disabled,
                    Warning = LocKey.Setting.GamingTouchKeyboardService.OptionWarning0,
                    Effects = new Effect[] { new ScriptEffect(@"if([Environment]::OSVersion.Version.Build -ge 22000 -and -not(Get-WinUserLanguageList|?{$_.LanguageTag-match'^(zh|ja|ko)'})){$f='C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\TextInputHost.exe'; $o=$f-replace'\.exe$','.old.exe'; if(Test-Path $f){takeown /f $f /a | Out-Null; icacls $f /grant Administrators:F | Out-Null; if(Test-Path $o){Remove-Item $o -Force}; Rename-Item $f $o -Force}; Stop-Process -Name TextInputHost -Force -ErrorAction SilentlyContinue}", RunContext.System) },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Start"] = Of(4),
                        ["IsInputAppPreloadEnabled"] = Of(0),
                    },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.WindowsDefault, StateRole.Recommended },
                    Effects = new Effect[] { new ScriptEffect(@"$f='C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\TextInputHost.exe'; $o=$f-replace'\.exe$','.old.exe'; if(Test-Path $o){if(Test-Path $f){Remove-Item $f -Force}; Rename-Item $o $f -Force}; Start-Process $f -ErrorAction SilentlyContinue", RunContext.System) },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Start"] = Of(3).OrAbsent(),
                        ["IsInputAppPreloadEnabled"] = Of(1),
                    },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Effects = new Effect[] { new ScriptEffect(@"$f='C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\TextInputHost.exe'; $o=$f-replace'\.exe$','.old.exe'; if(Test-Path $o){if(Test-Path $f){Remove-Item $f -Force}; Rename-Item $o $f -Force}; Start-Process $f -ErrorAction SilentlyContinue", RunContext.System) },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["Start"] = Of(2),
                        ["IsInputAppPreloadEnabled"] = Of(1),
                    },
                },
            },
            CustomStateScripts = new[] { new ScriptEffect(@"$f='C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\TextInputHost.exe'; $o=$f-replace'\.exe$','.old.exe'; if(Test-Path $o){if(Test-Path $f){Remove-Item $f -Force}; Rename-Item $o $f -Force}; Start-Process $f -ErrorAction SilentlyContinue", RunContext.System) },
        },
        new()
        {
            Id = "gaming-telephony-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingTelephonyService.Name,
                Description = LocKey.Setting.GamingTelephonyService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
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
                    Label = LocKey.ServiceOption.Disabled,
                    Warning = LocKey.Setting.GamingTelephonyService.OptionWarning0,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-sensor-monitoring-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingSensorMonitoringService.Name,
                Description = LocKey.Setting.GamingSensorMonitoringService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.Radar,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SensrSvc" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-sensor-data-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingSensorDataService.Name,
                Description = LocKey.Setting.GamingSensorDataService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
                Icon = MaterialIcons.ChartBox,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("Start", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SensorDataService" }, "Start", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.ServiceOption.Disabled,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.ManualRecommended,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "gaming-ai-fabric-service",
            Display = new()
            {
                Name = LocKey.Setting.GamingAiFabricService.Name,
                Description = LocKey.Setting.GamingAiFabricService.Description,
                GroupName = LocKey.SettingGroup.SystemServices,
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
                    Label = LocKey.ServiceOption.DisabledRecommended,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(4) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Manual,
                    Set = new Dictionary<string, StateValue> { ["Start"] = Of(3) },
                },
                new SettingState
                {
                    Label = LocKey.ServiceOption.Automatic,
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
                Name = LocKey.Setting.GamingTaskCompatibilityAppraiser.Name,
                Description = LocKey.Setting.GamingTaskCompatibilityAppraiser.Description,
                GroupName = LocKey.SettingGroup.ScheduledTasks,
                Icon = MaterialIcons.FileDocumentCheck,
            },
            Availability = new() { ValidatesExistence = true },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser") },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingTaskProgramDataUpdater.Name,
                Description = LocKey.Setting.GamingTaskProgramDataUpdater.Description,
                GroupName = LocKey.SettingGroup.ScheduledTasks,
                Icon = MaterialIcons.DatabaseSync,
            },
            Availability = new() { ValidatesExistence = true },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Application Experience\ProgramDataUpdater") },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingTaskCeipConsolidator.Name,
                Description = LocKey.Setting.GamingTaskCeipConsolidator.Description,
                GroupName = LocKey.SettingGroup.ScheduledTasks,
                Icon = MaterialIcons.ChartLine,
            },
            Availability = new() { ValidatesExistence = true },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator") },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingTaskUsbCeip.Name,
                Description = LocKey.Setting.GamingTaskUsbCeip.Description,
                GroupName = LocKey.SettingGroup.ScheduledTasks,
                Icon = MaterialIcons.Usb,
            },
            Availability = new() { ValidatesExistence = true },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip") },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingTaskDiskDiagnostic.Name,
                Description = LocKey.Setting.GamingTaskDiskDiagnostic.Description,
                GroupName = LocKey.SettingGroup.ScheduledTasks,
                Icon = MaterialIcons.Harddisk,
            },
            Availability = new() { ValidatesExistence = true },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector") },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingTaskFeedbackDmclient.Name,
                Description = LocKey.Setting.GamingTaskFeedbackDmclient.Description,
                GroupName = LocKey.SettingGroup.ScheduledTasks,
                Icon = MaterialIcons.MessageAlert,
            },
            Availability = new() { ValidatesExistence = true },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Feedback\Siuf\DmClient") },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingTaskFeedbackDmclientDownload.Name,
                Description = LocKey.Setting.GamingTaskFeedbackDmclientDownload.Description,
                GroupName = LocKey.SettingGroup.ScheduledTasks,
                Icon = MaterialIcons.Download,
            },
            Availability = new() { ValidatesExistence = true },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload") },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingTaskErrorReportingQueue.Name,
                Description = LocKey.Setting.GamingTaskErrorReportingQueue.Description,
                GroupName = LocKey.SettingGroup.ScheduledTasks,
                Icon = MaterialIcons.AlertOctagon,
            },
            Availability = new() { ValidatesExistence = true },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Windows Error Reporting\QueueReporting") },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingTaskSqm.Name,
                Description = LocKey.Setting.GamingTaskSqm.Description,
                GroupName = LocKey.SettingGroup.ScheduledTasks,
                Icon = MaterialIcons.ChartBar,
            },
            Availability = new() { ValidatesExistence = true },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\PI\Sqm-Tasks") },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingTaskMareBackup.Name,
                Description = LocKey.Setting.GamingTaskMareBackup.Description,
                GroupName = LocKey.SettingGroup.ScheduledTasks,
                Icon = MaterialIcons.BackupRestore,
            },
            Availability = new() { ValidatesExistence = true },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Application Experience\MareBackup") },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingTaskStartupApp.Name,
                Description = LocKey.Setting.GamingTaskStartupApp.Description,
                GroupName = LocKey.SettingGroup.ScheduledTasks,
                Icon = MaterialIcons.RocketLaunch,
            },
            Availability = new() { ValidatesExistence = true },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Application Experience\StartupAppTask") },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingTaskMapsUpdate.Name,
                Description = LocKey.Setting.GamingTaskMapsUpdate.Description,
                GroupName = LocKey.SettingGroup.ScheduledTasks,
                Icon = MaterialIcons.MapOutline,
            },
            Availability = new() { ValidatesExistence = true },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Maps\MapsUpdateTask") },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
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
                Name = LocKey.Setting.GamingTaskAutochkProxy.Name,
                Description = LocKey.Setting.GamingTaskAutochkProxy.Description,
                GroupName = LocKey.SettingGroup.ScheduledTasks,
                Icon = MaterialIcons.HarddiskPlus,
            },
            Availability = new() { ValidatesExistence = true },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Autochk\Proxy") },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingTaskFamilySafety.Name,
                Description = LocKey.Setting.GamingTaskFamilySafety.Description,
                GroupName = LocKey.SettingGroup.ScheduledTasks,
                Icon = MaterialIcons.AccountSupervisor,
                IsSubjectivePreference = true,
            },
            Availability = new() { ValidatesExistence = true },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Shell\FamilySafetyMonitor") },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingTaskPowerEfficiency.Name,
                Description = LocKey.Setting.GamingTaskPowerEfficiency.Description,
                GroupName = LocKey.SettingGroup.ScheduledTasks,
                Icon = MaterialIcons.LightningBolt,
            },
            Availability = new() { ValidatesExistence = true },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Windows\Power Efficiency Diagnostics\AnalyzeSystem") },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingTaskWindowsAi.Name,
                Description = LocKey.Setting.GamingTaskWindowsAi.Description,
                GroupName = LocKey.SettingGroup.ScheduledTasks,
                Icon = MaterialIcons.Robot,
                AddedInVersion = "26.04.10",
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 }, ValidatesExistence = true },
            Targets = new Target[]
            {
                new TaskTarget("Task", @"\Microsoft\Windows\WindowsAI\RecallConfiguration"),
                new TaskTarget("Task2", @"\Microsoft\Windows\WindowsAI\RecallPipeline"),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true), ["Task2"] = Of(true) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingTaskOfficeActionsServer.Name,
                Description = LocKey.Setting.GamingTaskOfficeActionsServer.Description,
                GroupName = LocKey.SettingGroup.ScheduledTasks,
                Icon = MaterialIcons.CalendarClock,
                AddedInVersion = "26.04.10",
            },
            Availability = new() { ValidatesExistence = true },
            Targets = new Target[] { new TaskTarget("Task", @"\Microsoft\Office\Office Actions Server") },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Task"] = Of(true) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.VisualEffectsMode.Name,
                Description = LocKey.Setting.VisualEffectsMode.Description,
                GroupName = LocKey.SettingGroup.VisualEffects,
                Icon = MaterialIcons.MonitorEye,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("VisualFXSetting", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" }, "VisualFXSetting", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.VisualEffectsMode.Option0,
                    Roles = new[] { StateRole.WindowsDefault },
                    Controls = new Dictionary<string, LocKey> { ["ui-effects"] = LocKey.Common.Disabled, ["window-animation"] = LocKey.Common.Disabled, ["taskbar-animations"] = LocKey.Common.Disabled, ["enable-peek"] = LocKey.Common.Enabled, ["menu-animation"] = LocKey.Common.Disabled, ["fade-tooltip"] = LocKey.Common.Disabled, ["fade-menu-items"] = LocKey.Common.Disabled, ["taskbar-thumbnails"] = LocKey.Common.Enabled, ["mouse-shadow"] = LocKey.Common.Disabled, ["window-shadows"] = LocKey.Common.Disabled, ["show-thumbnails"] = LocKey.Common.Enabled, ["translucent-selection"] = LocKey.Common.Enabled, ["drag-full-windows"] = LocKey.Common.Enabled, ["combo-box-animation"] = LocKey.Common.Disabled, ["font-smoothing"] = LocKey.Common.Enabled, ["smooth-scroll-listboxes"] = LocKey.Common.Enabled, ["drop-shadows"] = LocKey.Common.Disabled },
                    Set = new Dictionary<string, StateValue> { ["VisualFXSetting"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Setting.VisualEffectsMode.Option1,
                    Controls = new Dictionary<string, LocKey> { ["ui-effects"] = LocKey.Common.Enabled, ["window-animation"] = LocKey.Common.Enabled, ["taskbar-animations"] = LocKey.Common.Enabled, ["enable-peek"] = LocKey.Common.Enabled, ["menu-animation"] = LocKey.Common.Enabled, ["fade-tooltip"] = LocKey.Common.Enabled, ["fade-menu-items"] = LocKey.Common.Enabled, ["taskbar-thumbnails"] = LocKey.Common.Enabled, ["mouse-shadow"] = LocKey.Common.Enabled, ["window-shadows"] = LocKey.Common.Enabled, ["show-thumbnails"] = LocKey.Common.Enabled, ["translucent-selection"] = LocKey.Common.Enabled, ["drag-full-windows"] = LocKey.Common.Enabled, ["combo-box-animation"] = LocKey.Common.Enabled, ["font-smoothing"] = LocKey.Common.Enabled, ["smooth-scroll-listboxes"] = LocKey.Common.Enabled, ["drop-shadows"] = LocKey.Common.Enabled },
                    Set = new Dictionary<string, StateValue> { ["VisualFXSetting"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.VisualEffectsMode.Option2,
                    Controls = new Dictionary<string, LocKey> { ["ui-effects"] = LocKey.Common.Disabled, ["window-animation"] = LocKey.Common.Disabled, ["taskbar-animations"] = LocKey.Common.Disabled, ["enable-peek"] = LocKey.Common.Disabled, ["menu-animation"] = LocKey.Common.Disabled, ["fade-tooltip"] = LocKey.Common.Disabled, ["fade-menu-items"] = LocKey.Common.Disabled, ["taskbar-thumbnails"] = LocKey.Common.Disabled, ["mouse-shadow"] = LocKey.Common.Disabled, ["window-shadows"] = LocKey.Common.Disabled, ["show-thumbnails"] = LocKey.Common.Disabled, ["translucent-selection"] = LocKey.Common.Disabled, ["drag-full-windows"] = LocKey.Common.Disabled, ["combo-box-animation"] = LocKey.Common.Disabled, ["font-smoothing"] = LocKey.Common.Disabled, ["smooth-scroll-listboxes"] = LocKey.Common.Disabled, ["drop-shadows"] = LocKey.Common.Disabled },
                    Set = new Dictionary<string, StateValue> { ["VisualFXSetting"] = Of(2) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.VisualEffectsMode.Option3,
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
                Name = LocKey.Setting.UiEffects.Name,
                Description = LocKey.Setting.UiEffects.Description,
                GroupName = LocKey.SettingGroup.VisualEffects,
                Icon = MaterialIcons.Animation,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("UserPreferencesMask", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "UserPreferencesMask", RegistryValueKind.Binary) { ByteIndex = 4, BitMask = 0x02 } },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.WindowAnimation.Name,
                Description = LocKey.Setting.WindowAnimation.Description,
                GroupName = LocKey.SettingGroup.VisualEffects,
                Icon = MaterialIcons.WindowRestore,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("MinAnimate", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics" }, "MinAnimate", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["MinAnimate"] = Of("1") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.TaskbarAnimations.Name,
                Description = LocKey.Setting.TaskbarAnimations.Description,
                GroupName = LocKey.SettingGroup.VisualEffects,
                Icon = MaterialIcons.DockBottom,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("TaskbarAnimations", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "TaskbarAnimations", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TaskbarAnimations"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.EnablePeek.Name,
                Description = LocKey.Setting.EnablePeek.Description,
                GroupName = LocKey.SettingGroup.VisualEffects,
                Icon = MaterialIcons.MonitorEye,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("EnableAeroPeek", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM" }, "EnableAeroPeek", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["EnableAeroPeek"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.MenuAnimation.Name,
                Description = LocKey.Setting.MenuAnimation.Description,
                GroupName = LocKey.SettingGroup.VisualEffects,
                Icon = MaterialIcons.MenuOpen,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("UserPreferencesMask", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "UserPreferencesMask", RegistryValueKind.Binary) { ByteIndex = 0, BitMask = 0x02 } },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.FadeTooltip.Name,
                Description = LocKey.Setting.FadeTooltip.Description,
                GroupName = LocKey.SettingGroup.VisualEffects,
                Icon = MaterialIcons.TooltipText,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("UserPreferencesMask", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "UserPreferencesMask", RegistryValueKind.Binary) { ByteIndex = 1, BitMask = 0x08 } },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.FadeMenuItems.Name,
                Description = LocKey.Setting.FadeMenuItems.Description,
                GroupName = LocKey.SettingGroup.VisualEffects,
                Icon = FluentIcons.SlideTextCursor,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("UserPreferencesMask", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "UserPreferencesMask", RegistryValueKind.Binary) { ByteIndex = 1, BitMask = 0x04 } },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.TaskbarThumbnails.Name,
                Description = LocKey.Setting.TaskbarThumbnails.Description,
                GroupName = LocKey.SettingGroup.VisualEffects,
                Icon = FluentIcons.ImageMultiple,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("AlwaysHibernateThumbnails", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM" }, "AlwaysHibernateThumbnails", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["AlwaysHibernateThumbnails"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.MouseShadow.Name,
                Description = LocKey.Setting.MouseShadow.Description,
                GroupName = LocKey.SettingGroup.VisualEffects,
                Icon = MaterialIcons.CursorDefault,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("UserPreferencesMask", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "UserPreferencesMask", RegistryValueKind.Binary) { ByteIndex = 1, BitMask = 0x20 } },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(0).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "window-shadows",
            Display = new()
            {
                Name = LocKey.Setting.WindowShadows.Name,
                Description = LocKey.Setting.WindowShadows.Description,
                GroupName = LocKey.SettingGroup.VisualEffects,
                Icon = MaterialIcons.BoxShadow,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("UserPreferencesMask", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "UserPreferencesMask", RegistryValueKind.Binary) { ByteIndex = 2, BitMask = 0x04 } },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ShowThumbnails.Name,
                Description = LocKey.Setting.ShowThumbnails.Description,
                GroupName = LocKey.SettingGroup.VisualEffects,
                Icon = FluentIcons.ImageStack,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("IconsOnly", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "IconsOnly", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["IconsOnly"] = Of(0) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.TranslucentSelection.Name,
                Description = LocKey.Setting.TranslucentSelection.Description,
                GroupName = LocKey.SettingGroup.VisualEffects,
                Icon = MaterialIcons.Select,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("ListviewAlphaSelect", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "ListviewAlphaSelect", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ListviewAlphaSelect"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.DragFullWindows.Name,
                Description = LocKey.Setting.DragFullWindows.Description,
                GroupName = LocKey.SettingGroup.VisualEffects,
                Icon = MaterialIcons.SelectionDrag,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("DragFullWindows", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "DragFullWindows", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["DragFullWindows"] = Of("1") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ComboBoxAnimation.Name,
                Description = LocKey.Setting.ComboBoxAnimation.Description,
                GroupName = LocKey.SettingGroup.VisualEffects,
                Icon = MaterialIcons.FormDropdown,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("UserPreferencesMask", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "UserPreferencesMask", RegistryValueKind.Binary) { ByteIndex = 0, BitMask = 0x04 } },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.FontSmoothing.Name,
                Description = LocKey.Setting.FontSmoothing.Description,
                GroupName = LocKey.SettingGroup.VisualEffects,
                Icon = MaterialIcons.FormatSize,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("FontSmoothing", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "FontSmoothing", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["FontSmoothing"] = Of("2") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.SmoothScrollListboxes.Name,
                Description = LocKey.Setting.SmoothScrollListboxes.Description,
                GroupName = LocKey.SettingGroup.VisualEffects,
                Icon = MaterialIcons.ListBox,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("UserPreferencesMask", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "UserPreferencesMask", RegistryValueKind.Binary) { ByteIndex = 0, BitMask = 0x08 } },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.DropShadows.Name,
                Description = LocKey.Setting.DropShadows.Description,
                GroupName = LocKey.SettingGroup.VisualEffects,
                Icon = MaterialIcons.TextShadow,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresReboot = true },
            Targets = new Target[] { new RegTarget("ListviewShadow", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "ListviewShadow", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ListviewShadow"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.GamingNarratorHotkey.Name,
                Description = LocKey.Setting.GamingNarratorHotkey.Description,
                GroupName = LocKey.SettingGroup.Accessibility,
                Icon = MaterialIcons.AccountVoice,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("WinEnterLaunchEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Narrator\NoRoam" }, "WinEnterLaunchEnabled", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["WinEnterLaunchEnabled"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.AccessibilityStickykeysHotkey.Name,
                Description = LocKey.Setting.AccessibilityStickykeysHotkey.Description,
                GroupName = LocKey.SettingGroup.Accessibility,
                Icon = MaterialIcons.AppleKeyboardShift,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("Flags", new[] { @"HKEY_CURRENT_USER\Control Panel\Accessibility\StickyKeys" }, "Flags", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Flags"] = Of("510") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.AccessibilityFilterkeysHotkey.Name,
                Description = LocKey.Setting.AccessibilityFilterkeysHotkey.Description,
                GroupName = LocKey.SettingGroup.Accessibility,
                Icon = MaterialIcons.KeyboardOutline,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("Flags", new[] { @"HKEY_CURRENT_USER\Control Panel\Accessibility\Keyboard Response" }, "Flags", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Flags"] = Of("126") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.AccessibilityTogglekeysHotkey.Name,
                Description = LocKey.Setting.AccessibilityTogglekeysHotkey.Description,
                GroupName = LocKey.SettingGroup.Accessibility,
                Icon = MaterialIcons.Numeric,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("Flags", new[] { @"HKEY_CURRENT_USER\Control Panel\Accessibility\ToggleKeys" }, "Flags", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Flags"] = Of("62") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.AccessibilityMousekeysHotkey.Name,
                Description = LocKey.Setting.AccessibilityMousekeysHotkey.Description,
                GroupName = LocKey.SettingGroup.Accessibility,
                Icon = MaterialIcons.MouseVariant,
                IsSubjectivePreference = true,
            },
            // Flags is a decimal-string bitmask; 0x04 = MKF_HOTKEYACTIVE. Fresh installs ship "62"
            // (bit set), so detection tests the bit and apply edits ONLY the bit, preserving the
            // user's other MouseKeys options.
            Targets = new Target[] { new RegTarget("Flags", new[] { @"HKEY_CURRENT_USER\Control Panel\Accessibility\MouseKeys" }, "Flags", RegistryValueKind.String) { StringFlagMask = 0x04, StringFlagAbsentBase = 62 } },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Flags"] = Of(true).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Flags"] = Of(false) },
                },
            },
        },
        new()
        {
            Id = "accessibility-highcontrast-hotkey",
            Display = new()
            {
                Name = LocKey.Setting.AccessibilityHighcontrastHotkey.Name,
                Description = LocKey.Setting.AccessibilityHighcontrastHotkey.Description,
                GroupName = LocKey.SettingGroup.Accessibility,
                Icon = MaterialIcons.ContrastCircle,
                IsSubjectivePreference = true,
            },
            Targets = new Target[] { new RegTarget("Flags", new[] { @"HKEY_CURRENT_USER\Control Panel\Accessibility\HighContrast" }, "Flags", RegistryValueKind.String) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Flags"] = Of("126") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.SystemRestoreProtection.Name,
                Description = LocKey.Setting.SystemRestoreProtection.Description,
                Icon = MaterialIcons.History,
                AddedInVersion = "26.05.13",
                IsSubjectivePreference = true,
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Effects = new Effect[] { new ScriptEffect(@"Enable-ComputerRestore -Drive 'C:\'", RunContext.System) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Effects = new Effect[] { new ScriptEffect(@"Disable-ComputerRestore -Drive 'C:\'", RunContext.System) },
                },
            },
            Detector = new SystemRestoreDetector(LocKey.Common.Enabled, LocKey.Common.Disabled),
        },
    };
}
