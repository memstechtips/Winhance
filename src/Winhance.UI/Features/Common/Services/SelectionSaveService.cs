using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.WimUtil.Models;

namespace Winhance.UI.Features.Common.Services;

public sealed class SelectionSaveService : ISelectionSaveService
{
    // Windows Setup only picks the answer file up under this exact name.
    private const string AutounattendFileName = "autounattend.xml";

    private static readonly HashSet<string> AlbumPictureExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tif", ".tiff" };

    private readonly ISelectionSetBuilder _selections;
    private readonly IConfigFileWriter _configFiles;
    private readonly IAutounattendWriter _autounattend;
    private readonly ISaveFilePicker _picker;
    private readonly IFileStore _fileStore;
    private readonly IWindowsThemeService _theme;
    private readonly IFileSystemService _files;
    private readonly IWimCustomizationService _wim;
    private readonly WimUtilSession _session;
    private readonly IDialogService _dialogs;
    private readonly ILocalizationService _loc;
    private readonly ILogService _log;

    public SelectionSaveService(
        ISelectionSetBuilder selections,
        IConfigFileWriter configFiles,
        IAutounattendWriter autounattend,
        ISaveFilePicker picker,
        IFileStore fileStore,
        IWindowsThemeService theme,
        IFileSystemService files,
        IWimCustomizationService wim,
        WimUtilSession session,
        IDialogService dialogs,
        ILocalizationService loc,
        ILogService log)
    {
        _selections = selections;
        _configFiles = configFiles;
        _autounattend = autounattend;
        _picker = picker;
        _fileStore = fileStore;
        _theme = theme;
        _files = files;
        _wim = wim;
        _session = session;
        _dialogs = dialogs;
        _loc = loc;
        _log = log;
    }

    public async Task<string?> SaveAsync(BuilderTarget target, SelectionSet selections, SelectionSaveOptions? options = null)
    {
        options ??= new SelectionSaveOptions();

        if (options.ConfirmEmptyAppSelection
            && selections.WindowsApps.Count == 0
            && !await ConfirmEmptyAppSelectionAsync(target))
        {
            return null;
        }

        string? path = options.FixedPath;
        if (path == null)
        {
            path = PickDestination(target);
            if (string.IsNullOrEmpty(path))
            {
                _log.Log(LogLevel.Info, $"{target} save: no destination chosen");
                return null;
            }

            // A fixed path skips this - the caller named the file itself.
            if (target == BuilderTarget.Autounattend
                && !string.Equals(Path.GetFileName(path), AutounattendFileName, StringComparison.OrdinalIgnoreCase))
            {
                await _dialogs.ShowInformationAsync(
                    _loc.GetString("AdvancedTools_Msg_InvalidFilename"),
                    _loc.GetString("Dialog_Warning"));
                return null;
            }
        }

        string? media = target == BuilderTarget.Autounattend ? options.MediaFolder ?? _session.WorkingDirectory : null;

        // Staged before the writer reads the set: the file has to name where the album lands, not where it was authored.
        var toWrite = media is { Length: > 0 } ? StageAlbumOnMedia(selections, media) : selections;

        string written = await WriteAsync(target, toWrite, path);
        _log.Log(LogLevel.Info, $"{target} saved to {written}");

        if (media is { Length: > 0 })
        {
            // The save already worked, so a failed driver step is only logged.
            try
            {
                await _wim.EnsureDriverInstallStepAsync(written, media);
            }
            catch (Exception ex)
            {
                _log.Log(LogLevel.Warning, $"Could not add the driver install step to {written}: {ex.Message}");
            }
        }

        if (options.ReportSuccessInDialog)
            await ReportSuccessAsync(target, written);

        return written;
    }

    private async Task<bool> ConfirmEmptyAppSelectionAsync(BuilderTarget target)
    {
        string message = target == BuilderTarget.Autounattend
            ? _loc.GetString("Dialog_NoAppsSelected_Xml_Message")
            : _loc.GetString("Dialog_NoAppsSelected_Config_Message");

        return (await _dialogs.ShowConfirmationAsync(new ConfirmationRequest
        {
            Message = message,
            Title = _loc.GetString("Dialog_NoAppsSelected_Title"),
            ConfirmButtonText = _loc.GetString("Button_Yes"),
            CancelButtonText = _loc.GetString("Button_No"),
        })).Confirmed;
    }

    private string? PickDestination(BuilderTarget target) =>
        target == BuilderTarget.Autounattend
            ? _picker.PickSavePath(
                _loc.GetString("AdvancedTools_FileDialog_SaveXml"),
                "XML Files",
                "*.xml",
                AutounattendFileName,
                "xml")
            : _picker.PickSavePath(
                _loc.GetString("Config_FileDialog_SaveConfig"),
                ConfigFileConstants.FileFilter,
                ConfigFileConstants.FilePattern,
                $"Winhance_Config_{DateTime.Now:yyyyMMdd}{ConfigFileConstants.FileExtension}",
                "winhance");

    // Setup copies sources\$OEM$\$$ into %WINDIR%, so an album staged there lands where AlbumDestinationFor says.
    // Rewritten on the set being saved, never on the edit store, where it would come back as the user's own choice.
    private SelectionSet StageAlbumOnMedia(SelectionSet set, string workingDirectory)
    {
        List<SettingChoice>? rewritten = null;

        for (int i = 0; i < set.Settings.Count; i++)
        {
            var choice = set.Settings[i];

            if (choice.Value is not ChoiceValue.Text { Value.Length: > 0 } typed
                || SettingCatalog.Find(choice.SettingId) is not { } setting
                || !setting.Targets.OfType<DesktopSlideshowTarget>().Any())
                continue;

            if (!_files.DirectoryExists(typed.Value))
            {
                // Still a correct choice on a PC that has the folder, so it travels as it was authored.
                _log.Log(LogLevel.Warning,
                    $"Slideshow album '{typed.Value}' is not on this PC; it was not copied onto the media.");
                continue;
            }

            string landing = _theme.AlbumDestinationFor(typed.Value);
            string staged = _files.CombinePath(
                workingDirectory, "sources", "$OEM$", "$$", "Web", "Wallpaper", "Winhance", _files.GetFileName(landing));
            _files.CreateDirectory(staged);

            foreach (var picture in _files.GetFiles(typed.Value))
            {
                if (AlbumPictureExtensions.Contains(_files.GetExtension(picture)))
                    _files.CopyFile(picture, _files.CombinePath(staged, _files.GetFileName(picture)), overwrite: true);
            }

            rewritten ??= [.. set.Settings];
            rewritten[i] = choice with { Value = new ChoiceValue.Text(landing) };
        }

        return rewritten is null ? set : set with { Settings = rewritten };
    }

    private async Task<string> WriteAsync(BuilderTarget target, SelectionSet selections, string path)
    {
        var carried = await _fileStore.LoadAsync(selections);

        if (target == BuilderTarget.Autounattend)
            return await _autounattend.WriteAsync(carried, _selections.CurrentScope, path);

        await _configFiles.WriteAsync(carried, _selections.CurrentScope, path);
        return path;
    }

    // The XML message names the file and spells out the steps to WIMUtil; it never offers to go there, so every
    // entry point reports the same save the same way.
    private Task ReportSuccessAsync(BuilderTarget target, string path) =>
        target == BuilderTarget.Autounattend
            ? _dialogs.ShowInformationAsync(
                _loc.GetString("AdvancedTools_Msg_XmlGenSuccess", path),
                _loc.GetString("Dialog_Success"))
            : _dialogs.ShowInformationAsync(
                _loc.GetString("Config_Export_Success_Message", path),
                _loc.GetString("Config_Export_Success_Title"));
}
