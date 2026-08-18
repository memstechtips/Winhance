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
            // No Restart: the Appearance broadcast alone applies a theme switch live (verified on Windows
            // 2026-07-31). Restarting Explorer for it only raised the pending-restart bar for nothing.
            Apply = new() { RequiresConfirmation = true, NotifyWindows = WindowsChange.Appearance },
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
                    // A preset over the two facets below, NOT a gate on them - the labels are the children's
                    // own ("Enabled" = that surface uses the LIGHT theme), not this setting's Light/Dark.
                    Controls = new Dictionary<string, string> { ["theme-mode-apps"] = "Enabled", ["theme-mode-system"] = "Enabled" },
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
                    Controls = new Dictionary<string, string> { ["theme-mode-apps"] = "Disabled", ["theme-mode-system"] = "Disabled" },
                    Set = new Dictionary<string, StateValue> { ["AppsUseLightTheme"] = Of(0), ["SystemUsesLightTheme"] = Of(0) },
                    // The default dark-mode wallpaper (Win11 uses img19; Win10 has one 4K image for both modes).
                    Effects = new Effect[]
                    {
                        new WallpaperEffect(@"C:\Windows\Web\Wallpaper\Windows\img19.jpg") { AppliesTo = new[] { BuildRange.Windows11 } },
                        new WallpaperEffect(@"C:\Windows\Web\4K\Wallpaper\Windows\img0_3840x2160.jpg") { AppliesTo = new[] { BuildRange.Windows10 } },
                    },
                },
                // The NEUTRAL state, APPENDED at index 2 - Light stays 0 and Dark stays 1 because saved
                // .winhance configs persist the raw state index, so state order is a public contract.
                // AppsUseLightTheme and SystemUsesLightTheme are independent, so a machine can sit on
                // light-apps/dark-shell (the Windows 10 shipped default) - a real configuration this setting
                // has no single write for. IsFallback lands detection here and names it instead of reporting
                // "Not recognized"; IsDetectOnly keeps it out of the dropdown, because picking it would write
                // nothing. Declaring NO Controls is also what makes it the state ResolveReverseSync snaps the
                // master to when neither preset is satisfied.
                new SettingState
                {
                    Label = "Mixed",
                    IsFallback = true,
                    IsDetectOnly = true,
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
            // NO EnabledWhen. This is the bug fix: the two facets are independently meaningful in
            // EVERY state of the master above - that is exactly why "Mixed" has to exist - so nesting
            // them under it must not grey them. The old code guessed the gate from the state INDEX
            // (index != 0), and Light Mode is index 0, so every stock Windows 11 install opened this
            // page with both sub-toggles dead.
            Apply = new() { NotifyWindows = WindowsChange.Appearance },
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
            // NO EnabledWhen. This is the bug fix: the two facets are independently meaningful in
            // EVERY state of the master above - that is exactly why "Mixed" has to exist - so nesting
            // them under it must not grey them. The old code guessed the gate from the state INDEX
            // (index != 0), and Light Mode is index 0, so every stock Windows 11 install opened this
            // page with both sub-toggles dead.
            Apply = new() { NotifyWindows = WindowsChange.Appearance },
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
            // No Restart: transparency has never declared one, and the notice is not a restart -
            // it is the true statement that applying this changes how Windows looks.
            Apply = new() { NotifyWindows = WindowsChange.Appearance },
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
