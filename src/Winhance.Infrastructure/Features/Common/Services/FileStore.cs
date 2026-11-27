using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Helpers;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.Customize.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

internal sealed class FileStore(IFileSystemService files, ILogService log, IWindowsThemeService theme) : IFileStore
{
    // Only logged, never refused. Windows Setup reads the whole answer file into memory before it runs it.
    private const long LargeFileBytes = 16L * 1024 * 1024;

    // A carried picture runs to megabytes; comparing 48 KB at a time keeps both copies off the large object heap.
    private const int CompareChars = 64 * 1024;

    public async Task<SelectionSet> LoadAsync(SelectionSet set)
    {
        List<SettingChoice>? rewritten = null;
        List<CarriedFile>? carried = null;

        for (int i = 0; i < set.Settings.Count; i++)
        {
            var choice = set.Settings[i];
            if (choice.Value is not ChoiceValue.Keyed { Key.Length: > 0 } keyed)
                continue;

            if (SettingCatalog.Find(choice.SettingId)?.Options?.Source != OptionSource.Pictures
                || !PictureFileConstants.IsPicture(keyed.Key)
                || !theme.Travels(keyed.Key))
                continue;

            if (await LoadBytesAsync(keyed.Key).ConfigureAwait(false) is not { } base64)
                continue;

            string destination = theme.DestinationFor(keyed.Key);
            rewritten ??= [.. set.Settings];
            carried ??= [.. set.Files];
            rewritten[i] = choice with { Value = keyed with { Key = destination } };
            carried.Add(new CarriedFile(choice.SettingId, destination, base64));
        }

        return rewritten is null ? set : set with { Settings = rewritten, Files = carried! };
    }

    public async Task<SelectionSet> MaterializeAsync(SelectionSet set)
    {
        if (set.Files.Count == 0)
            return set;

        var landed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in set.Files)
        {
            if (IsAlbum(file.SettingId) && !set.Settings.Any(choice => choice.SettingId == file.SettingId
                && choice.Value is ChoiceValue.Text album
                && string.Equals(Path.GetDirectoryName(file.Destination), album.Value, StringComparison.OrdinalIgnoreCase)))
            {
                log.Log(LogLevel.Warning, $"The file '{file.SettingId}' carries was ignored: it does not belong to the selected album.");
                continue;
            }
            if (LandingFor(file) is not { } destination)
                continue;

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(file.Base64);
            }
            catch (FormatException ex)
            {
                log.Log(LogLevel.Warning, $"The file '{file.SettingId}' carries could not be decoded: {ex.Message}");
                continue;
            }

            try
            {
                if (!files.EnsureProtectedPictureDestination(destination))
                {
                    log.Log(LogLevel.Warning, $"The file '{file.SettingId}' carries was ignored: its destination is not protected.");
                    continue;
                }
                await files.WriteAllBytesAsync(destination, bytes).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log.Log(LogLevel.Warning, $"The file '{file.SettingId}' carries could not be written: {ex.Message}");
                continue;
            }

            landed[file.SettingId] = IsAlbum(file.SettingId) ? Path.GetDirectoryName(destination)! : destination;
        }

        if (landed.Count == 0)
            return set;

        var settings = set.Settings
            .Select(choice => landed.TryGetValue(choice.SettingId, out var destination) ? choice.Value switch
            {
                ChoiceValue.Keyed keyed => choice with { Value = keyed with { Key = destination } },
                ChoiceValue.Text when IsAlbum(choice.SettingId) => choice with { Value = new ChoiceValue.Text(destination) },
                _ => choice,
            } : choice)
            .ToList();

        return set with { Settings = settings };
    }

    public async Task<bool> IsSameFileAsync(string path, string base64)
    {
        try
        {
            if (!files.FileExists(path) || files.GetFileSize(path) != DecodedLength(base64))
                return false;

            await using var stream = files.OpenRead(path);
            var carried = new byte[CompareChars / 4 * 3];
            var onDisk = new byte[carried.Length];
            for (int start = 0; start < base64.Length; start += CompareChars)
            {
                if (!Convert.TryFromBase64Chars(base64.AsSpan(start, Math.Min(CompareChars, base64.Length - start)), carried, out int count))
                    return false;
                await stream.ReadExactlyAsync(onDisk.AsMemory(0, count)).ConfigureAwait(false);
                if (!onDisk.AsSpan(0, count).SequenceEqual(carried.AsSpan(0, count)))
                    return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Log(LogLevel.Debug, $"Could not compare '{path}' with the file a config carries: {ex.Message}");
            return false;
        }
    }

    // Convert.ToBase64String writes no line breaks; base64 that has them compares as different, which costs only a re-apply.
    private static long DecodedLength(string base64) =>
        base64.Length % 4 != 0 ? -1 : base64.Length / 4 * 3 - (base64.EndsWith("==", StringComparison.Ordinal) ? 2 : base64.EndsWith('=') ? 1 : 0);

    // A config is someone else's file, so only the name it gives is believed, and a colon in one would address a
    // stream inside another file.
    private string? LandingFor(CarriedFile file)
    {
        if (SettingCatalog.Find(file.SettingId)?.Options?.Source != OptionSource.Pictures && !IsAlbum(file.SettingId))
        {
            log.Log(LogLevel.Warning, $"The file '{file.SettingId}' carries was ignored: that setting takes no file.");
            return null;
        }

        if (Path.GetFileName(file.Destination) is not { Length: > 0 } name
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || !PictureFileConstants.IsPicture(name)
            || WindowsFileNames.IsReservedDeviceName(name))
        {
            log.Log(LogLevel.Warning, $"The file '{file.SettingId}' carries was ignored: '{file.Destination}' does not name a picture.");
            return null;
        }

        if (!IsAlbum(file.SettingId))
            return theme.DestinationFor(name);
        var album = Path.GetFileName(Path.GetDirectoryName(file.Destination));
        if (string.IsNullOrEmpty(album) || album is "." or ".." || album.EndsWith('.') || album.EndsWith(' ')
            || album.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || WindowsFileNames.IsReservedDeviceName(album))
            return null;
        return files.CombinePath(theme.AlbumDestinationFor(album), name);
    }

    private static bool IsAlbum(string settingId) =>
        SettingCatalog.Find(settingId)?.Targets.OfType<DesktopSlideshowTarget>().Any() == true;

    private async Task<string?> LoadBytesAsync(string path)
    {
        try
        {
            if (!files.FileExists(path))
            {
                log.Log(LogLevel.Warning, $"Carried file '{path}' is not on this PC; it travels as a path.");
                return null;
            }

            long size = files.GetFileSize(path);
            if (size > LargeFileBytes)
            {
                log.Log(LogLevel.Warning,
                    $"Carried file '{path}' is {size / (1024 * 1024)} MB; the file it travels in will be at least that large.");
            }

            return Convert.ToBase64String(await files.ReadAllBytesAsync(path).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            log.Log(LogLevel.Warning, $"Could not read '{path}' ({ex.Message}); it travels as a path.");
            return null;
        }
    }
}
