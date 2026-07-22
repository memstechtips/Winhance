using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using static Winhance.Core.Features.Common.Catalog.StateValue;

namespace Winhance.Core.Features.Customize.Models;

public static class WindowsThemeCustomizationsCatalog
{
    public const string FeatureId = FeatureIds.WindowsTheme;
    public const string FeatureName = "Windows Theme";

    public static IReadOnlyList<Setting> All { get; } = new Setting[]
    {
        new()
        {
            Id = "theme-mode-windows",
            Display = new()
            {
                Name = "Choose your mode",
                Description = "Choose between Light and Dark mode for Windows and apps",
                GroupName = "Theme Mode",
                Icon = MaterialIcons.BrushVariant,
                IsSubjectivePreference = true,
            },
            Apply = new() { RequiresConfirmation = true, Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("AppsUseLightTheme", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" }, "AppsUseLightTheme", RegistryValueKind.DWord),
                new RegTarget("SystemUsesLightTheme", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" }, "SystemUsesLightTheme", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Light Mode",
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                    Set = new Dictionary<string, StateValue> { ["AppsUseLightTheme"] = Of(1), ["SystemUsesLightTheme"] = Of(1) },
                    // The default light-mode wallpaper (applied only when the user opts into "also change the
                    // wallpaper"); OS-divergent, so per-OS via AppliesTo. Moved here from the retired WallpaperDefaults.
                    Effects = new Effect[]
                    {
                        new WallpaperEffect(@"C:\Windows\Web\Wallpaper\Windows\img0.jpg") { AppliesTo = new[] { BuildRange.Windows11 } },
                        new WallpaperEffect(@"C:\Windows\Web\4K\Wallpaper\Windows\img0_3840x2160.jpg") { AppliesTo = new[] { BuildRange.Windows10 } },
                    },
                },
                new SettingState
                {
                    Label = "Dark Mode",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["AppsUseLightTheme"] = Of(0), ["SystemUsesLightTheme"] = Of(0) },
                    // The default dark-mode wallpaper (Win11 uses img19; Win10 has one 4K image for both modes).
                    Effects = new Effect[]
                    {
                        new WallpaperEffect(@"C:\Windows\Web\Wallpaper\Windows\img19.jpg") { AppliesTo = new[] { BuildRange.Windows11 } },
                        new WallpaperEffect(@"C:\Windows\Web\4K\Wallpaper\Windows\img0_3840x2160.jpg") { AppliesTo = new[] { BuildRange.Windows10 } },
                    },
                },
            },
        },
        new()
        {
            Id = "theme-mode-apps",
            Display = new()
            {
                Name = "Apps Use Light Theme",
                Description = "Use the light theme for apps such as Settings and File Explorer",
                GroupName = "Theme Mode",
                Icon = MaterialIcons.Apps,
                IsSubjectivePreference = true,
                AddedInVersion = "26.07.22",
            },
            UiParentId = "theme-mode-windows",
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("AppsUseLightTheme", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" }, "AppsUseLightTheme", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    // Light apps is the shipped default on BOTH builds (first logon writes 1; the
                    // image ships no value, so absence also reads as the default).
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AppsUseLightTheme"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AppsUseLightTheme"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "theme-mode-system",
            Display = new()
            {
                Name = "Windows Uses Light Theme",
                Description = "Use the light theme for the Windows shell, including the taskbar, Start menu, and notification center",
                GroupName = "Theme Mode",
                Icon = MaterialIcons.Monitor,
                IsSubjectivePreference = true,
                AddedInVersion = "26.07.22",
            },
            UiParentId = "theme-mode-windows",
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("SystemUsesLightTheme", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" }, "SystemUsesLightTheme", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    // The shell theme default genuinely differs per build: light on Windows 11,
                    // dark on Windows 10 (the probe-confirmed mixed default).
                    Label = "Enabled",
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                    Set = new Dictionary<string, StateValue> { ["SystemUsesLightTheme"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["SystemUsesLightTheme"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "theme-transparency",
            Display = new()
            {
                Name = "Transparency effects",
                Description = "Enable translucent effects for the Start Menu, taskbar, and other Windows interface elements",
                GroupName = "Transparency",
                Icon = MaterialIcons.Opacity,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("EnableTransparency", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" }, "EnableTransparency", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["EnableTransparency"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["EnableTransparency"] = Of(0) },
                },
            },
        },
    };
}
