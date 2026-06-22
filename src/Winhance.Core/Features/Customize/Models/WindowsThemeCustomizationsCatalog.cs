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
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AppsUseLightTheme"] = Of(1), ["SystemUsesLightTheme"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Dark Mode",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["AppsUseLightTheme"] = Of(0), ["SystemUsesLightTheme"] = Of(0) },
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
