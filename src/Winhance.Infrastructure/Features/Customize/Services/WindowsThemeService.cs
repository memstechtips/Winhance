using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32.Graphics.Gdi;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Customize.Interfaces;

namespace Winhance.Infrastructure.Features.Customize.Services;

// Holds no IStateWriter, so WindowsStateWriter can take it without a DI cycle.
internal sealed class WindowsThemeService : IWindowsThemeService, IOptionProvider
{
    private const int SPI_SETDESKWALLPAPER = 0x0014;
    private const int SPIF_SENDCHANGE = 0x02;

    // Windows writes Interval when a slideshow starts, so an album that has never run one ticks at 30 minutes.
    private const int DefaultIntervalMs = 1800000;

    private const int RecentPictureCount = 5;

    // Machine-wide, beside Windows' own pictures, so whatever account exists on the target PC can read it.
    private const string CarriedPictureFolder = @"C:\Windows\Web\Wallpaper\Winhance";

    private const int CachePathOffset = 24;

    private const string FolderIdAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";

    // Every picture type the Windows background picker offers. A config is someone else's file: nothing else lands.
    private static readonly HashSet<string> PictureExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jpe", ".jfif", ".png", ".bmp", ".dib", ".gif", ".tif", ".tiff", ".wdp", ".jxr", ".webp",
        ".heic", ".heif", ".hif", ".avif",
    };

    private readonly ILogService _log;
    private readonly IWindowsRegistryService _registry;
    private readonly ISystemParametersService _systemParameters;
    private readonly IFileSystemService _files;
    private readonly IDesktopSlideshow _slideshow;
    private readonly IInteractiveUserService _interactiveUser;
    private readonly ISystemDetectionContextFactory _detection;
    private readonly ILocalizationService _localization;

    // The refresh runs on the thread pool, so an album apply can land between it reading the album and starting it.
    private readonly object _slideshowGate = new();

    public WindowsThemeService(
        ILogService log,
        IWindowsRegistryService registry,
        ISystemParametersService systemParameters,
        IFileSystemService files,
        IDesktopSlideshow slideshow,
        IInteractiveUserService interactiveUser,
        ISystemDetectionContextFactory detection,
        ILocalizationService localization)
    {
        _log = log;
        _registry = registry;
        _systemParameters = systemParameters;
        _files = files;
        _slideshow = slideshow;
        _interactiveUser = interactiveUser;
        _detection = detection;
        _localization = localization;
    }

    public IReadOnlyList<OptionSource> Sources { get; } = [OptionSource.Pictures, OptionSource.Colors];

    public bool RefreshDesktop()
    {
        var kind = ReadLive("theme-wallpaper", "BackgroundType") as int?;
        _log.Log(LogLevel.Debug, $"Refreshing the desktop background for BackgroundType={kind?.ToString() ?? "<absent>"}");

        return kind switch
        {
            1 => RefreshColor(),
            2 => RefreshSlideshow(),
            // No nudge for Spotlight: its DesktopSpotlight task owns the picture, and WallPaper then holds a real file
            // (C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\DesktopSpotlight\Assets\Images,
            // measured 2026-09-16), so RefreshPicture would pin it and write BackgroundType = 0, ending Spotlight.
            3 => true,
            _ => RefreshPicture(),
        };
    }

    public bool SetSlideshow(string folder)
    {
        lock (_slideshowGate)
            return StartSlideshow(folder);
    }

    // The id outlives the slideshow it was set for, so it is only this PC's album while BackgroundType is 2.
    public string? CurrentAlbum(Setting setting, IDetectionContext context) =>
        RegTargetReader.Read(setting, "BackgroundType", context) is int kind && kind == 2
            ? AlbumPath(RegTargetReader.Read(setting, "SlideshowDirectoryPath1", context) as string, context)
            : null;

    // Only the pictures the setting itself ships are on the new install. One an OEM put under C:\Windows, or one an
    // earlier import carried into CarriedPictureFolder, is not.
    public bool Travels(string path)
    {
        var setting = SettingCatalog.Find("theme-wallpaper-picture")
            ?? throw new InvalidOperationException("'theme-wallpaper-picture' is not in the catalog.");

        return !setting.States.Any(state =>
            string.Equals(KeyedOptions.KeyOf(setting, state), path, StringComparison.OrdinalIgnoreCase));
    }

    public string DestinationFor(string path) => $@"{CarriedPictureFolder}\{Path.GetFileName(path)}";

    public string AlbumDestinationFor(string folder) =>
        $@"{CarriedPictureFolder}\{Path.GetFileName(folder.TrimEnd('\\'))}";

    public IReadOnlyList<DynamicOption> Options(Setting setting, IDetectionContext context) => setting.Options!.Source switch
    {
        OptionSource.Pictures => PictureOptions(setting, context),
        OptionSource.Colors => ColorOptions(setting, context),
        var other => throw new ArgumentOutOfRangeException(nameof(setting), other, "Not a desktop background list."),
    };

    public string? CurrentKey(Setting setting, IDetectionContext context) => setting.Options!.Source switch
    {
        OptionSource.Pictures => ShownPicture(setting, context),
        OptionSource.Colors => RegTargetReader.Read(setting, "BackgroundType", context) is int kind && kind == 1
            ? RecordedColor(setting, context)
            : null,
        var other => throw new ArgumentOutOfRangeException(nameof(setting), other, "Not a desktop background list."),
    };

    // A key need not be one this PC offers: a config from another machine can name a picture or colour it never had.
    public bool Accepts(Setting setting, string key) => setting.Options!.Source switch
    {
        OptionSource.Pictures => IsPicture(key),
        OptionSource.Colors => RegistryColorFromHex(key) is not null,
        var other => throw new ArgumentOutOfRangeException(nameof(setting), other, "Not a desktop background list."),
    };

    public string? ValueFor(OptionValue value, string key) =>
        value == OptionValue.Color
            ? RegistryColorFromHex(key)
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Not a desktop background value.");

    public Effect? EffectFor(Setting setting, string key) => null;

    private List<DynamicOption> PictureOptions(Setting setting, IDetectionContext context)
    {
        var options = new List<DynamicOption>(setting.States.Count + 1 + RecentPictureCount);

        foreach (var state in setting.States)
        {
            if (KeyedOptions.KeyOf(setting, state) is { } picture && context.FileExists(picture))
                options.Add(new DynamicOption(_localization.GetString(state.Label.Value), picture));
        }

        if (ShownPicture(setting, context) is { Length: > 0 } shown && context.FileExists(shown) && !Listed(options, shown))
            options.Add(new DynamicOption(Path.GetFileName(shown), shown));

        for (int i = 0; i < RecentPictureCount; i++)
        {
            if (RegTargetReader.Read(setting, $"BackgroundHistoryPath{i}", context) is string recent
                && recent.Length > 0 && context.FileExists(recent) && !Listed(options, recent))
                options.Add(new DynamicOption(Path.GetFileName(recent), recent));
        }

        return options;
    }

    // An absent BackgroundType still means a picture. WallPaper names the transcoded copy when the picture came from
    // Windows Settings, so the cache blob is read first; two or more screens write one each (_000, _001, ...).
    private static string? ShownPicture(Setting setting, IDetectionContext context)
    {
        if (RegTargetReader.Read(setting, "BackgroundType", context) is int kind && kind != 0)
            return null;

        int screens = RegTargetReader.Read(setting, "TranscodedImageCount", context) is int n ? n : 0;
        var blob = RegTargetReader.Read(setting, screens >= 2 ? "TranscodedImageCache_000" : "TranscodedImageCache", context) as byte[];
        if (PathFromCache(blob) is { Length: > 0 } original)
            return original;

        // No cache: an older Windows, or something wrote WallPaper itself. The transcoded copy is no selection.
        return RegTargetReader.Read(setting, "WallPaper", context) is string path
            && path.Length > 0
            && !IsTranscodedCopy(path)
                ? path
                : null;
    }

    // A swatch has no name of its own: the hex is the caption in every language.
    private static List<DynamicOption> ColorOptions(Setting setting, IDetectionContext context)
    {
        var options = setting.Options!.Keys.Select(hex => new DynamicOption(hex, hex)).ToList();

        if (RecordedColor(setting, context) is { } own && !Listed(options, own))
            options.Add(new DynamicOption(own, own));

        return options;
    }

    private static string? RecordedColor(Setting setting, IDetectionContext context) =>
        HexFromRegistryColor(RegTargetReader.Read(setting, "Background", context) as string);

    private static bool Listed(List<DynamicOption> options, string key) =>
        options.Any(o => string.Equals(o.Value, key, StringComparison.OrdinalIgnoreCase));

    // WallPaper cannot always name the last picture: a colour empties it and a slideshow points it at the transcoded
    // slide, while CurrentWallpaperPath keeps it through both (probe, 2026-09-11). An empty path removes the picture.
    private bool RefreshPicture()
    {
        string?[] candidates =
        [
            ReadLive("theme-wallpaper-picture", "WallPaper") as string,
            ReadLive("theme-wallpaper-picture", "CurrentWallpaperPath") as string,
            ReadLive("theme-wallpaper-picture", "BackgroundHistoryPath0") as string,
        ];

        if (candidates.FirstOrDefault(IsShowablePicture) is { } path)
            return ShowPicture(path);

        _log.Log(LogLevel.Warning, "No picture to show: WallPaper, CurrentWallpaperPath and the latest recent image name no file on this PC");
        return false;
    }

    private bool IsShowablePicture(string? path) =>
        path is { Length: > 0 } && !IsTranscodedCopy(path) && _files.FileExists(path);

    // SetSysColors paints the colour now; the broadcast with an empty path takes the old picture off the desktop.
    private bool RefreshColor()
    {
        if (ChannelsFromRegistry(ReadLive("theme-wallpaper-color", "Background") as string) is { } color)
            _systemParameters.SetSysColors((int)SYS_COLOR_INDEX.COLOR_BACKGROUND, ColorRef(color));

        return Broadcast(string.Empty);
    }

    // BackgroundType = 2 alone shows nothing: the shell only plays an album it is handed.
    private bool RefreshSlideshow()
    {
        lock (_slideshowGate)
        {
            var album = AlbumPath(ReadLive("theme-wallpaper-album", "SlideshowDirectoryPath1") as string, _detection.Create());

            if (album is not null)
                return StartSlideshow(album);

            _log.Log(LogLevel.Warning, "No slideshow album is recorded on this PC yet, so there is nothing to play until one is chosen");
            return false;
        }
    }

    // The refresh's one write: a catalog state writes fixed values, so no card's apply can copy the kept picture back.
    private bool ShowPicture(string path)
    {
        WriteLive("theme-wallpaper-picture", "WallPaper", path);
        WriteLive("theme-wallpaper-picture", "CurrentWallpaperPath", path);
        WriteLive("theme-wallpaper", "BackgroundType", 0);

        bool ok = Broadcast(path);
        if (ok)
            _log.Log(LogLevel.Info, $"Wallpaper set to {path}");
        return ok;
    }

    private bool StartSlideshow(string folder)
    {
        if (RefuseUnderOtsElevation())
            return false;

        if (!_files.DirectoryExists(folder))
        {
            _log.Log(LogLevel.Error, $"Slideshow album not found: {folder}");
            return false;
        }

        try
        {
            _slideshow.Set(folder, Position(), IntervalMs(), Shuffle());
            _log.Log(LogLevel.Info, $"Desktop background set to a slideshow of {folder}");
            return true;
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"Could not set the slideshow of {folder}: {ex.Message}", ex);
            return false;
        }
    }

    // The COM object acts on the profile of the process that created it, which under OTS elevation is the admin's.
    private bool RefuseUnderOtsElevation()
    {
        if (!_interactiveUser.IsOtsElevation)
            return false;

        _log.Log(LogLevel.Error,
            $"A slideshow cannot be set for {_interactiveUser.InteractiveUserName} while Winhance runs as another user.");
        return true;
    }

    // SPIF_UPDATEINIFILE stays out: the registry has the values, and under OTS it would persist to the admin's profile.
    private bool Broadcast(string path)
    {
        bool ok = _systemParameters.SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_SENDCHANGE) != 0;
        if (!ok)
            _log.Log(LogLevel.Error, $"SystemParametersInfo failed for '{path}': {Marshal.GetLastWin32Error()}");
        return ok;
    }

    private int IntervalMs() => ReadLive("theme-wallpaper-interval", "Interval") as int? ?? DefaultIntervalMs;

    private bool Shuffle() => ReadLive("theme-wallpaper-shuffle", "Shuffle") is int shuffle && shuffle != 0;

    private int Position() => WallpaperPosition(
        ReadLive("theme-wallpaper-fit", "WallpaperStyle") as string,
        ReadLive("theme-wallpaper-fit", "TileWallpaper") as string);

    // Via the registry service, not a detection context, so an OTS-elevated process uses the interactive user's hive.
    private object? ReadLive(string settingId, string key)
    {
        var target = Target(settingId, key);
        return _registry.GetValue(target.Paths[0], target.ValueName!);
    }

    private void WriteLive(string settingId, string key, object value)
    {
        var target = Target(settingId, key);
        _registry.SetValue(target.Paths[0], target.ValueName!, value, target.Type);
    }

    private static RegTarget Target(string settingId, string key) =>
        RegTargetReader.Target(SettingCatalog.Find(settingId) ?? throw new InvalidOperationException($"'{settingId}' is not in the catalog."), key);

    internal static bool IsPicture(string path) => PictureExtensions.Contains(Path.GetExtension(path));

    // The copy Windows shows a Settings picture or a slideshow slide from.
    private static bool IsTranscodedCopy(string path) =>
        path.EndsWith("TranscodedWallpaper", StringComparison.OrdinalIgnoreCase);

    // TranscodedImageCache: six DWORDs of header, then the original path as UTF-16LE, ending at the first NUL.
    internal static string? PathFromCache(byte[]? blob)
    {
        if (blob is null || blob.Length <= CachePathOffset + 1)
            return null;
        var text = Encoding.Unicode.GetString(blob, CachePathOffset, blob.Length - CachePathOffset);
        int end = text.IndexOf('\0');
        var path = end >= 0 ? text[..end] : text;
        return path.Length > 0 ? path : null;
    }

    // Registry pair WallpaperStyle + TileWallpaper to DESKTOP_WALLPAPER_POSITION; the two numberings do not line up.
    internal static int WallpaperPosition(string? wallpaperStyle, string? tileWallpaper) => (wallpaperStyle, tileWallpaper) switch
    {
        (_, "1") => 1,
        ("0", _) => 0,
        ("2", _) => 2,
        ("6", _) => 3,
        ("10", _) => 4,
        ("22", _) => 5,
        _ => 4,
    };

    internal static string? HexFromRegistryColor(string? text) =>
        ChannelsFromRegistry(text) is { } color ? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : null;

    internal static string? RegistryColorFromHex(string? text) =>
        ChannelsFromHex(text) is { } color ? $"{color.R} {color.G} {color.B}" : null;

    private static (byte R, byte G, byte B)? ChannelsFromRegistry(string? text)
    {
        var parts = text?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts is not { Length: 3 }
            || !byte.TryParse(parts[0], out var r)
            || !byte.TryParse(parts[1], out var g)
            || !byte.TryParse(parts[2], out var b))
            return null;
        return (r, g, b);
    }

    private static (byte R, byte G, byte B)? ChannelsFromHex(string? text)
    {
        var digits = text?.TrimStart('#');
        if (digits is not { Length: 6 }
            || !byte.TryParse(digits.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            || !byte.TryParse(digits.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            || !byte.TryParse(digits.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            return null;
        return (r, g, b);
    }

    // COLORREF is 0x00BBGGRR, the reverse of the registry value's channel order.
    private static uint ColorRef((byte R, byte G, byte B) color) => (uint)(color.R | (color.G << 8) | (color.B << 16));

    // The album folder as Windows records it: a shell item list, base64'd with the six bits of every group written
    // low bit first, and the bytes packed the same way (probe, 2026-09-11). Standard base64 decodes it to garbage.
    internal static byte[] DecodeFolderId(string text)
    {
        var bytes = new List<byte>(text.Length * 6 / 8);
        int buffer = 0;
        int bits = 0;
        foreach (var character in text)
        {
            int value = FolderIdAlphabet.IndexOf(character);
            if (value < 0)
                throw new FormatException($"'{character}' is not a shell folder id character.");
            buffer |= value << bits;
            bits += 6;
            while (bits >= 8)
            {
                bytes.Add((byte)(buffer & 0xFF));
                buffer >>= 8;
                bits -= 8;
            }
        }
        return [.. bytes];
    }

    // The last group is padded with zeros, so a round trip can differ in its last character from the id Windows wrote.
    internal static string EncodeFolderId(byte[] itemList)
    {
        var text = new StringBuilder((itemList.Length * 8 + 5) / 6);
        int buffer = 0;
        int bits = 0;
        foreach (var value in itemList)
        {
            buffer |= value << bits;
            bits += 8;
            while (bits >= 6)
            {
                text.Append(FolderIdAlphabet[buffer & 0x3F]);
                buffer >>= 6;
                bits -= 6;
            }
        }
        if (bits > 0)
            text.Append(FolderIdAlphabet[buffer & 0x3F]);
        return text.ToString();
    }

    // Only the shell can turn the stored id back into a path, so a context that cannot ask it reads nothing.
    internal static string? AlbumPath(string? registryValue, IDetectionContext context)
    {
        if (registryValue is not { Length: > 0 })
            return null;

        byte[] itemList;
        try
        {
            itemList = DecodeFolderId(registryValue);
        }
        catch (FormatException)
        {
            return null;
        }

        return context.ShellFolderPath(itemList) is { Length: > 0 } folder ? folder : null;
    }
}
