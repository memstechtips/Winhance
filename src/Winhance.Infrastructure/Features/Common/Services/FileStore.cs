using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Infrastructure.Features.Customize.Services;

namespace Winhance.Infrastructure.Features.Common.Services;

internal sealed class FileStore(IFileSystemService files, ILogService log, IWindowsThemeService theme) : IFileStore
{
    // Only logged, never refused. Windows Setup reads the whole answer file into memory before it runs it.
    private const long LargeFileBytes = 16L * 1024 * 1024;

    // A reserved device name resolves to the device with any extension on it, so NUL.jpg discards the bytes written
    // and leaves the desktop pointed at a device rather than at a picture.
    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

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
                || !WindowsThemeService.IsPicture(keyed.Key)
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
                if (Path.GetDirectoryName(destination) is { Length: > 0 } folder)
                    files.CreateDirectory(folder);
                await files.WriteAllBytesAsync(destination, bytes).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log.Log(LogLevel.Warning, $"The file '{file.SettingId}' carries could not be written: {ex.Message}");
                continue;
            }

            landed[file.SettingId] = destination;
        }

        if (landed.Count == 0)
            return set;

        var settings = set.Settings
            .Select(choice =>
                landed.TryGetValue(choice.SettingId, out var destination) && choice.Value is ChoiceValue.Keyed keyed
                    ? choice with { Value = keyed with { Key = destination } }
                    : choice)
            .ToList();

        return set with { Settings = settings };
    }

    // A config is someone else's file, so only the name it gives is believed, and a colon in one would address a
    // stream inside another file.
    private string? LandingFor(CarriedFile file)
    {
        if (SettingCatalog.Find(file.SettingId)?.Options?.Source != OptionSource.Pictures)
        {
            log.Log(LogLevel.Warning, $"The file '{file.SettingId}' carries was ignored: that setting takes no file.");
            return null;
        }

        if (Path.GetFileName(file.Destination) is not { Length: > 0 } name
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || !WindowsThemeService.IsPicture(name)
            || ReservedDeviceNames.Contains(Path.GetFileNameWithoutExtension(name)))
        {
            log.Log(LogLevel.Warning, $"The file '{file.SettingId}' carries was ignored: '{file.Destination}' does not name a picture.");
            return null;
        }

        return theme.DestinationFor(name);
    }

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
