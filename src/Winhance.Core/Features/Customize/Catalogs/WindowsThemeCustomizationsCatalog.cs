using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using static Winhance.Core.Features.Common.Catalog.StateValue;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Customize.Catalogs;

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
                Name = LocKey.Setting.ThemeModeWindows.Name,
                Description = LocKey.Setting.ThemeModeWindows.Description,
                GroupName = LocKey.SettingGroup.ThemeMode,
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
                    Label = LocKey.Setting.ThemeModeWindows.Option0,
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                    // A preset over the two facets below, NOT a gate on them - the labels are the children's
                    // own ("Enabled" = that surface uses the LIGHT theme), not this setting's Light/Dark.
                    Controls = new Dictionary<string, LocKey> { ["theme-mode-apps"] = LocKey.Common.Enabled, ["theme-mode-system"] = LocKey.Common.Enabled },
                    Set = new Dictionary<string, StateValue> { ["AppsUseLightTheme"] = Of(1), ["SystemUsesLightTheme"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ThemeModeWindows.Option1,
                    Roles = new[] { StateRole.Recommended },
                    Controls = new Dictionary<string, LocKey> { ["theme-mode-apps"] = LocKey.Common.Disabled, ["theme-mode-system"] = LocKey.Common.Disabled },
                    Set = new Dictionary<string, StateValue> { ["AppsUseLightTheme"] = Of(0), ["SystemUsesLightTheme"] = Of(0) },
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
                    Label = LocKey.Setting.ThemeModeWindows.Option2,
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
                Name = LocKey.Setting.ThemeModeApps.Name,
                Description = LocKey.Setting.ThemeModeApps.Description,
                GroupName = LocKey.SettingGroup.ThemeMode,
                Icon = MaterialIcons.Apps,
                IsSubjectivePreference = true,
                AddedInVersion = "26.07.22",
            },
            UiParentId = "theme-mode-windows",
            // NO EnabledWhen: the two facets are independently meaningful in EVERY state of the master
            // above - that is exactly why "Mixed" has to exist - so nesting them under it must not grey them.
            // (Gating on the state index would kill both sub-toggles on every stock Windows 11 install, where
            // Light Mode is index 0.)
            Apply = new() { NotifyWindows = WindowsChange.Appearance },
            Targets = new Target[]
            {
                new RegTarget("AppsUseLightTheme", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" }, "AppsUseLightTheme", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    // Light apps is the shipped default on BOTH builds (first logon writes 1; the
                    // image ships no value, so absence also reads as the default).
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AppsUseLightTheme"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ThemeModeSystem.Name,
                Description = LocKey.Setting.ThemeModeSystem.Description,
                GroupName = LocKey.SettingGroup.ThemeMode,
                Icon = MaterialIcons.Monitor,
                IsSubjectivePreference = true,
                AddedInVersion = "26.07.22",
            },
            UiParentId = "theme-mode-windows",
            // NO EnabledWhen: the two facets are independently meaningful in EVERY state of the master
            // above - that is exactly why "Mixed" has to exist - so nesting them under it must not grey them.
            // (Gating on the state index would kill both sub-toggles on every stock Windows 11 install, where
            // Light Mode is index 0.)
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                    Set = new Dictionary<string, StateValue> { ["SystemUsesLightTheme"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ThemeTransparency.Name,
                Description = LocKey.Setting.ThemeTransparency.Description,
                GroupName = LocKey.SettingGroup.Transparency,
                Icon = MaterialIcons.Opacity,
                IsSubjectivePreference = true,
            },
            // No Restart: the notice is not a restart - it is the true statement that applying this changes
            // how Windows looks.
            Apply = new() { NotifyWindows = WindowsChange.Appearance },
            Targets = new Target[]
            {
                new RegTarget("EnableTransparency", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize" }, "EnableTransparency", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["EnableTransparency"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["EnableTransparency"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "theme-wallpaper",
            Display = new()
            {
                Name = LocKey.Setting.ThemeWallpaper.Name,
                Description = LocKey.Setting.ThemeWallpaper.Description,
                GroupName = LocKey.SettingGroup.Background,
                Icon = MaterialIcons.ImageOutline,
                IsSubjectivePreference = true,
                AddedInVersion = "26.09.10",
                CompactChildren = true,
            },
            Apply = new() { NotifyWindows = WindowsChange.Desktop },
            Targets = new Target[]
            {
                new RegTarget("BackgroundType", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers" }, "BackgroundType", RegistryValueKind.DWord),
                new RegTarget("WallPaper", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "WallPaper", RegistryValueKind.String),
                // The Spotlight on/off switch. Measured 2026-09-16, Picture to Spotlight on one machine: BackgroundType
                // went 0 to 3 and this went 0 to 1. Writing BackgroundType alone does nothing.
                new RegTarget("SpotlightEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\DesktopSpotlight\Settings" }, "EnabledState", RegistryValueKind.DWord),
            },
            States = new[]
            {
                // BackgroundType is absent on a profile that has only ever shown a picture. Windows empties WallPaper
                // for a solid colour; the other kinds accept whatever it holds.
                new SettingState
                {
                    Label = LocKey.Setting.ThemeWallpaper.Option0,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["BackgroundType"] = Of(0).OrAbsent(), ["WallPaper"] = Exists.OrAbsent(), ["SpotlightEnabled"] = Of(0).OrAbsent() },
                    ResetSet = new Dictionary<string, StateValue> { ["BackgroundType"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ThemeWallpaper.Option1,
                    Set = new Dictionary<string, StateValue> { ["BackgroundType"] = Of(1), ["WallPaper"] = Of("").OrAbsent(), ["SpotlightEnabled"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ThemeWallpaper.Option2,
                    Set = new Dictionary<string, StateValue> { ["BackgroundType"] = Of(2), ["WallPaper"] = Exists.OrAbsent(), ["SpotlightEnabled"] = Of(0).OrAbsent() },
                },
                // Detect-only: the WindowsUdk.UI.Shell.DesktopSpotlight classes are not registered for activation
                // outside the CBS package (measured 2026-09-17, 0x80073D54 APPMODEL_ERROR_NO_PACKAGE), and the shell's
                // background task ignores these two values when written. The other kinds write SpotlightEnabled = 0,
                // which is how leaving Spotlight works.
                new SettingState
                {
                    Label = LocKey.Setting.ThemeWallpaper.Option3,
                    IsDetectOnly = true,
                    Warning = LocKey.Setting.ThemeWallpaper.OptionWarning3,
                    Set = new Dictionary<string, StateValue> { ["BackgroundType"] = Of(3), ["WallPaper"] = Exists.OrAbsent(), ["SpotlightEnabled"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "theme-wallpaper-picture",
            Display = new()
            {
                Name = LocKey.Setting.ThemeWallpaperPicture.Name,
                Description = LocKey.Setting.ThemeWallpaperPicture.Description,
                GroupName = LocKey.SettingGroup.Background,
                Icon = MaterialIcons.ImageOutline,
                IsSubjectivePreference = true,
                AddedInVersion = "26.09.14",
                Tiles = OptionTiles.Pictures,
            },
            Apply = new() { NotifyWindows = WindowsChange.Desktop },
            Options = new(OptionSource.Pictures),
            Targets = new Target[]
            {
                new RegTarget("WallPaper", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "WallPaper", RegistryValueKind.String),
                // Windows keeps the picture here through a colour or a slideshow, and restores it when Picture comes back.
                new RegTarget("CurrentWallpaperPath", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers" }, "CurrentWallpaperPath", RegistryValueKind.String) { ApplyOnly = true },
                new RegTarget("BackgroundType", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers" }, "BackgroundType", RegistryValueKind.DWord) { ReadOnly = true },
                new RegTarget("TranscodedImageCount", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "TranscodedImageCount", RegistryValueKind.DWord) { ReadOnly = true },
                new RegTarget("TranscodedImageCache", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "TranscodedImageCache", RegistryValueKind.Binary) { ReadOnly = true },
                new RegTarget("TranscodedImageCache_000", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "TranscodedImageCache_000", RegistryValueKind.Binary) { ReadOnly = true },
                new RegTarget("BackgroundHistoryPath0", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers" }, "BackgroundHistoryPath0", RegistryValueKind.String) { ReadOnly = true },
                new RegTarget("BackgroundHistoryPath1", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers" }, "BackgroundHistoryPath1", RegistryValueKind.String) { ReadOnly = true },
                new RegTarget("BackgroundHistoryPath2", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers" }, "BackgroundHistoryPath2", RegistryValueKind.String) { ReadOnly = true },
                new RegTarget("BackgroundHistoryPath3", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers" }, "BackgroundHistoryPath3", RegistryValueKind.String) { ReadOnly = true },
                new RegTarget("BackgroundHistoryPath4", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers" }, "BackgroundHistoryPath4", RegistryValueKind.String) { ReadOnly = true },
            },
            // A picture the build does not ship is not on disk, so the file check that offers a state is also its build filter.
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.ThemeWallpaperPicture.OptionWindows11Light,
                    Set = new Dictionary<string, StateValue> { ["WallPaper"] = Of(@"C:\Windows\Web\Wallpaper\Windows\img0.jpg") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ThemeWallpaperPicture.OptionWindows11Dark,
                    Set = new Dictionary<string, StateValue> { ["WallPaper"] = Of(@"C:\Windows\Web\Wallpaper\Windows\img19.jpg") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ThemeWallpaperPicture.OptionWindows10,
                    Set = new Dictionary<string, StateValue> { ["WallPaper"] = Of(@"C:\Windows\Web\4K\Wallpaper\Windows\img0_3840x2160.jpg") },
                },
            },
            UiParentId = "theme-wallpaper",
            VisibleWhen = new("theme-wallpaper", [LocKey.Setting.ThemeWallpaper.Option0]),
        },
        new()
        {
            Id = "theme-wallpaper-fit",
            Display = new()
            {
                Name = LocKey.Setting.ThemeWallpaperFit.Name,
                Description = LocKey.Setting.ThemeWallpaperFit.Description,
                GroupName = LocKey.SettingGroup.Background,
                Icon = MaterialIcons.ImageOutline,
                IsSubjectivePreference = true,
                AddedInVersion = "26.09.14",
            },
            Apply = new() { NotifyWindows = WindowsChange.Desktop },
            // Windows stores the fit as this pair of REG_SZ values, and Tile is the only one that moves the second.
            Targets = new Target[]
            {
                new RegTarget("WallpaperStyle", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "WallpaperStyle", RegistryValueKind.String),
                new RegTarget("TileWallpaper", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "TileWallpaper", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.ThemeWallpaperFit.Option0,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["WallpaperStyle"] = Of("10"), ["TileWallpaper"] = Of("0").OrAbsent() },
                    // WallpaperStyle is a value a clean machine carries, so only the tile flag is deleted.
                    ResetSet = new Dictionary<string, StateValue> { ["TileWallpaper"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ThemeWallpaperFit.Option1,
                    Set = new Dictionary<string, StateValue> { ["WallpaperStyle"] = Of("6"), ["TileWallpaper"] = Of("0") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ThemeWallpaperFit.Option2,
                    Set = new Dictionary<string, StateValue> { ["WallpaperStyle"] = Of("2"), ["TileWallpaper"] = Of("0") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ThemeWallpaperFit.Option3,
                    Set = new Dictionary<string, StateValue> { ["WallpaperStyle"] = Of("0"), ["TileWallpaper"] = Of("1") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ThemeWallpaperFit.Option4,
                    Set = new Dictionary<string, StateValue> { ["WallpaperStyle"] = Of("0"), ["TileWallpaper"] = Of("0") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ThemeWallpaperFit.Option5,
                    Set = new Dictionary<string, StateValue> { ["WallpaperStyle"] = Of("22"), ["TileWallpaper"] = Of("0") },
                },
            },
            UiParentId = "theme-wallpaper",
            VisibleWhen = new("theme-wallpaper", [LocKey.Setting.ThemeWallpaper.Option0, LocKey.Setting.ThemeWallpaper.Option2]),
        },
        new()
        {
            Id = "theme-wallpaper-color",
            Display = new()
            {
                Name = LocKey.Setting.ThemeWallpaperColor.Name,
                Description = LocKey.Setting.ThemeWallpaperColor.Description,
                GroupName = LocKey.SettingGroup.Background,
                Icon = MaterialIcons.ImageOutline,
                IsSubjectivePreference = true,
                AddedInVersion = "26.09.14",
                Tiles = OptionTiles.Colors,
            },
            Apply = new() { NotifyWindows = WindowsChange.Desktop },
            Options = new(OptionSource.Colors)
            {
                // Windows' own background palette, measured off Windows' Personalization page.
                Keys =
                [
                    "#FF8C00", "#E81123", "#D13438", "#C30052", "#BF0077", "#9A0089", "#881798", "#744DA9",
                    "#10893E", "#107C10", "#018574", "#2D7D9A", "#0063B1", "#6B69D6", "#8E8CD8", "#8764B8",
                    "#038387", "#486860", "#525E54", "#7E735F", "#4C4A48", "#515C6B", "#4A5459", "#000000",
                ],
            },
            Targets = new Target[]
            {
                new RegTarget("Background", new[] { @"HKEY_CURRENT_USER\Control Panel\Colors" }, "Background", RegistryValueKind.String) { From = OptionValue.Color },
                new RegTarget("BackgroundType", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers" }, "BackgroundType", RegistryValueKind.DWord) { ReadOnly = true },
            },
            UiParentId = "theme-wallpaper",
            VisibleWhen = new("theme-wallpaper", [LocKey.Setting.ThemeWallpaper.Option1]),
        },
        new()
        {
            Id = "theme-wallpaper-album",
            Display = new()
            {
                Name = LocKey.Setting.ThemeWallpaperAlbum.Name,
                Description = LocKey.Setting.ThemeWallpaperAlbum.Description,
                GroupName = LocKey.SettingGroup.Background,
                Icon = MaterialIcons.ImageOutline,
                IsSubjectivePreference = true,
                AddedInVersion = "26.09.14",
            },
            // No desktop notice: the slideshow op already hands the shell the album, and a refresh would hand it over again.
            // Any non-empty folder passes, so the rule's message is never shown and reuses the placeholder key.
            TextBox = new(
                new TextRule("^.{1,}$", UpperCase: false, LocKey.Setting.ThemeWallpaperAlbum.Placeholder),
                Picker: PickerKind.Folder,
                SeedKey: "album",
                Placeholder: LocKey.Setting.ThemeWallpaperAlbum.Placeholder),
            Targets = new Target[]
            {
                new DesktopSlideshowTarget("album"),
                new RegTarget("BackgroundType", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers" }, "BackgroundType", RegistryValueKind.DWord) { ReadOnly = true },
                new RegTarget("SlideshowDirectoryPath1", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers" }, "SlideshowDirectoryPath1", RegistryValueKind.String) { ReadOnly = true },
            },
            // The answer file has no element for the folder id Windows records, so this script sets the album on the
            // new install. It reads the interval, shuffle and position the sibling cards have already written.
            CustomStateScripts = new[]
            {
                new ScriptEffect(@"$album = '{{value}}'
$slideshowKey = Get-ItemProperty -Path 'HKCU:\Control Panel\Personalization\Desktop Slideshow' -ErrorAction SilentlyContinue
$desktopKey = Get-ItemProperty -Path 'HKCU:\Control Panel\Desktop' -ErrorAction SilentlyContinue
$interval = if ($slideshowKey.Interval) { [int]$slideshowKey.Interval } else { 1800000 }
$shuffle = if ($slideshowKey.Shuffle -eq 1) { 1 } else { 0 }
$position = switch (""$($desktopKey.WallpaperStyle)"") { '0' { 0 } '2' { 2 } '6' { 3 } '22' { 5 } default { 4 } }
if (""$($desktopKey.TileWallpaper)"" -eq '1') { $position = 1 }
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace Winhance.Desktop {
    [ComImport, Guid(""43826D1E-E718-42EE-BC55-A1E261C37BFE""), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem { }
    [ComImport, Guid(""B63EA76D-1F85-456F-A19C-48159EFA858B""), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItemArray { }
    [ComImport, Guid(""B92B56A9-8B55-4E14-9A89-0199BBB6F93B""), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDesktopWallpaper {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string monitorID);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetMonitorDevicePathAt(uint monitorIndex);
        uint GetMonitorDevicePathCount();
        void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorID, IntPtr displayRect);
        void SetBackgroundColor(uint color);
        uint GetBackgroundColor();
        void SetPosition(int position);
        int GetPosition();
        void SetSlideshow(IShellItemArray items);
        IShellItemArray GetSlideshow();
        void SetSlideshowOptions(int options, uint slideshowTick);
        void GetSlideshowOptions(out int options, out uint slideshowTick);
        void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string monitorID, int direction);
        int GetStatus();
        void Enable(int enable);
    }
    [ComImport, Guid(""C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD"")]
    public class DesktopWallpaper { }
    public static class Shell {
        static Guid itemId = new Guid(""43826D1E-E718-42EE-BC55-A1E261C37BFE"");
        static Guid arrayId = new Guid(""B63EA76D-1F85-456F-A19C-48159EFA858B"");
        [DllImport(""shell32.dll"", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid riid, out IShellItem item);
        [DllImport(""shell32.dll"", ExactSpelling = true)]
        static extern int SHCreateShellItemArrayFromShellItem(IShellItem item, ref Guid riid, out IShellItemArray items);
        public static IShellItemArray Album(string folder) {
            IShellItem folderItem;
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(folder, IntPtr.Zero, ref itemId, out folderItem));
            IShellItemArray items;
            Marshal.ThrowExceptionForHR(SHCreateShellItemArrayFromShellItem(folderItem, ref arrayId, out items));
            return items;
        }
    }
}
'@
$desktop = [Winhance.Desktop.IDesktopWallpaper](New-Object Winhance.Desktop.DesktopWallpaper)
try {
    $desktop.SetSlideshow([Winhance.Desktop.Shell]::Album($album))
    $desktop.SetSlideshowOptions($shuffle, $interval)
    $desktop.SetPosition($position)
    Write-Log ""Desktop background set: slideshow of $album"" ""SUCCESS""
} catch {
    Write-Log ""Desktop background slideshow failed: $($_.Exception.Message)"" ""ERROR""
}", RunContext.User),
            },
            UiParentId = "theme-wallpaper",
            VisibleWhen = new("theme-wallpaper", [LocKey.Setting.ThemeWallpaper.Option2]),
        },
        new()
        {
            Id = "theme-wallpaper-interval",
            Display = new()
            {
                Name = LocKey.Setting.ThemeWallpaperInterval.Name,
                Description = LocKey.Setting.ThemeWallpaperInterval.Description,
                GroupName = LocKey.SettingGroup.Background,
                Icon = MaterialIcons.ImageOutline,
                IsSubjectivePreference = true,
                AddedInVersion = "26.09.14",
            },
            Apply = new() { NotifyWindows = WindowsChange.Desktop },
            // Windows writes Interval when a slideshow starts, so an album without one ticks at half an hour.
            Targets = new Target[]
            {
                new RegTarget("Interval", new[] { @"HKEY_CURRENT_USER\Control Panel\Personalization\Desktop Slideshow" }, "Interval", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Setting.ThemeWallpaperInterval.Option0,
                    Set = new Dictionary<string, StateValue> { ["Interval"] = Of(60000) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ThemeWallpaperInterval.Option1,
                    Set = new Dictionary<string, StateValue> { ["Interval"] = Of(600000) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ThemeWallpaperInterval.Option2,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Interval"] = Of(1800000).OrAbsent() },
                    ResetSet = new Dictionary<string, StateValue> { ["Interval"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ThemeWallpaperInterval.Option3,
                    Set = new Dictionary<string, StateValue> { ["Interval"] = Of(3600000) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ThemeWallpaperInterval.Option4,
                    Set = new Dictionary<string, StateValue> { ["Interval"] = Of(21600000) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ThemeWallpaperInterval.Option5,
                    Set = new Dictionary<string, StateValue> { ["Interval"] = Of(86400000) },
                },
            },
            UiParentId = "theme-wallpaper",
            VisibleWhen = new("theme-wallpaper", [LocKey.Setting.ThemeWallpaper.Option2]),
        },
        new()
        {
            Id = "theme-wallpaper-shuffle",
            Display = new()
            {
                Name = LocKey.Setting.ThemeWallpaperShuffle.Name,
                Description = LocKey.Setting.ThemeWallpaperShuffle.Description,
                GroupName = LocKey.SettingGroup.Background,
                Icon = MaterialIcons.ImageOutline,
                IsSubjectivePreference = true,
                AddedInVersion = "26.09.14",
            },
            Apply = new() { NotifyWindows = WindowsChange.Desktop },
            Targets = new Target[]
            {
                new RegTarget("Shuffle", new[] { @"HKEY_CURRENT_USER\Control Panel\Personalization\Desktop Slideshow" }, "Shuffle", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["Shuffle"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Shuffle"] = Of(0).OrAbsent() },
                    ResetSet = new Dictionary<string, StateValue> { ["Shuffle"] = Absent },
                },
            },
            UiParentId = "theme-wallpaper",
            VisibleWhen = new("theme-wallpaper", [LocKey.Setting.ThemeWallpaper.Option2]),
        },
    };
}
