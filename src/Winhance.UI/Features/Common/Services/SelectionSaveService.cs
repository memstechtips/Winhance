using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.WimUtil;
using Winhance.UI.Features.WimUtil.Models;

namespace Winhance.UI.Features.Common.Services;

public sealed class SelectionSaveService : ISelectionSaveService
{
    // Windows Setup only picks the answer file up under this exact name.
    private const string AutounattendFileName = "autounattend.xml";

    private readonly ISelectionSetBuilder _selections;
    private readonly IConfigFileWriter _configFiles;
    private readonly IAutounattendWriter _autounattend;
    private readonly ISaveFilePicker _picker;
    private readonly IFileStore _fileStore;
    private readonly IWindowsThemeService _theme;
    private readonly IFileSystemService _files;
    private readonly IWimCustomizationService _wim;
    private readonly IAppStagingService _staging;
    private readonly ITaskProgressService _progress;
    private readonly WimUtilSession _session;
    private readonly IAnswerFileValidator _answerFileValidator;
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
        IAppStagingService staging,
        ITaskProgressService progress,
        WimUtilSession session,
        IAnswerFileValidator answerFileValidator,
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
        _staging = staging;
        _progress = progress;
        _session = session;
        _answerFileValidator = answerFileValidator;
        _dialogs = dialogs;
        _loc = loc;
        _log = log;
    }

    public async Task<SelectionSaveResult> SaveAsync(BuilderTarget target, SelectionSet selections, SelectionSaveOptions? options = null)
    {
        options ??= new SelectionSaveOptions();

        string? path = options.FixedPath;
        var misnamed = false;
        if (path == null)
        {
            path = PickDestination(target);
            if (string.IsNullOrEmpty(path))
            {
                _log.Log(LogLevel.Info, $"{target} save: no destination chosen");
                return SelectionSaveResult.Nothing;
            }

            // Setup reads only autounattend.xml, but a renamed copy is still a fine archive, so the save goes
            // ahead and the success dialog says so. A fixed path skips this - the caller named the file itself.
            misnamed = target == BuilderTarget.Autounattend
                && !string.Equals(Path.GetFileName(path), AutounattendFileName, StringComparison.OrdinalIgnoreCase);
        }

        string? media = target == BuilderTarget.Autounattend ? options.MediaFolder ?? _session.WorkingDirectory : null;

        // Staged before the writer reads the set: the file has to name where the album lands, not where it was authored.
        var toWrite = media is { Length: > 0 } ? StageAlbumOnMedia(selections, media) : selections;

        string written = await WriteAsync(target, toWrite, path);
        _log.Log(LogLevel.Info, $"{target} saved to {written}");

        IReadOnlyList<StagedApp> staged = [];
        if (media is { Length: > 0 })
        {
            // The save already worked, so a failed driver or update step is only logged.
            try
            {
                await _wim.EnsureDriverInstallStepAsync(written, media);
            }
            catch (Exception ex)
            {
                _log.Log(LogLevel.Warning, $"Could not add the driver install step to {written}: {ex.Message}");
            }

            try
            {
                await _wim.EnsureUpdateInstallStepAsync(written, media);
            }
            catch (Exception ex)
            {
                _log.Log(LogLevel.Warning, $"Could not add the update install step to {written}: {ex.Message}");
            }

            staged = await StageAppsAsync(selections, media, options.ReportSuccessInDialog);
        }

        if (options.ReportSuccessInDialog)
        {
            // The file names the installers either way; only a save onto media copies them.
            bool ownInstallersNotOnMedia = media is not { Length: > 0 } && OwnInstaller.From(selections).Count > 0;
            await ReportSuccessAsync(target, written, misnamed, selections.WindowsApps.Count == 0, ownInstallersNotOnMedia);
        }

        return new SelectionSaveResult(written, staged);
    }

    // The answer file is written and stays valid without the installers (the new PC downloads the catalog apps
    // instead), so a failure here warns and the save still returns its path.
    private async Task<IReadOnlyList<StagedApp>> StageAppsAsync(SelectionSet selections, string media, bool reportInDialog)
    {
        IReadOnlyList<StagedApp> outcomes;
        try
        {
            if (ToStage(selections) is not { } toStage)
            {
                _staging.RemoveStaged(media);
                return [];
            }

            _progress.StartTask(_loc.GetString("WIMUtil_Status_StagingApps"), false);
            outcomes = await _staging.StageAsync(
                media,
                toStage,
                _progress.CreateDetailedProgress(),
                _progress.CurrentTaskCancellationSource!.Token);
            _progress.CompleteTask();
        }
        catch (OperationCanceledException)
        {
            _progress.CancelTask();
            return [];
        }
        catch (Exception ex)
        {
            _progress.FailTask();
            _log.Log(LogLevel.Error, $"Could not put the apps on {media}: {ex.Message}", ex);
            await _dialogs.ShowWarningAsync(ex.Message, _loc.GetString("Dialog_Warning"));
            return [];
        }

        var notStaged = outcomes.Where(app => !app.Staged).Select(app => app.Name).ToList();

        // The format overload returns the raw text when a translation breaks the placeholder, so it cannot throw.
        if (reportInDialog && notStaged.Count > 0)
        {
            await _dialogs.ShowInformationAsync(
                _loc.GetString("WIMUtil_Msg_AppsNotStaged", string.Join("\n", notStaged)),
                _loc.GetString("Dialog_Information"));
        }

        return outcomes;
    }

    // What a save puts on the media beside the Winhance installer, or null when it stages nothing at all.
    internal static SelectionSet? ToStage(SelectionSet selections)
    {
        var toStage = OwnInstaller.StagesApps(selections) ? selections : selections with { ExternalApps = [] };
        return LeavesWinhanceOff(selections) || (toStage.ExternalApps.Count == 0 && OwnInstaller.From(toStage).Count == 0)
            ? null
            : toStage;
    }

    // Don't add leaves nothing on the new PC to run them. An absent choice is the seed state, the desktop shortcut.
    private static bool LeavesWinhanceOff(SelectionSet selections) =>
        selections.StateOf("autounattend-desktop-shortcut")?.Label == LocKey.Setting.AutounattendDesktopShortcut.Option0;

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
                if (PictureFileConstants.IsPicture(picture))
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
    // entry point reports the same save the same way. Neither an empty app list nor a finding stops the save: both
    // are shown on the one dialog and the user decides.
    private async Task ReportSuccessAsync(BuilderTarget target, string path, bool misnamed, bool noWindowsApps, bool ownInstallersNotOnMedia)
    {
        bool xml = target == BuilderTarget.Autounattend;
        var report = xml ? await CheckAnswerFileAsync(path) : null;
        bool hasFindings = report is { Findings.Count: > 0 };

        SummaryVerdict? noAppsVerdict = noWindowsApps
            ? new SummaryVerdict(SummaryTone.Warning,
                _loc.GetString("WIMUtil_Generate_Plan_NoWindowsApps"),
                xml
                    ? _loc.GetString("WIMUtil_Generate_Plan_NoWindowsApps_Detail")
                    : _loc.GetString("Config_Export_NoWindowsApps_Detail"))
            : null;

        var sections = new List<SummarySection>();
        if (hasFindings)
        {
            sections.Add(AnswerFileReportDialog.FindingsSection(report!, _loc));
            if (noAppsVerdict is not null)
                sections.Add(new SummarySection(null, [new SummaryRow(SummaryIcon.Status, noAppsVerdict.Title, noAppsVerdict.Message, Tone: SummaryTone.Warning)]));
        }

        if (ownInstallersNotOnMedia)
        {
            sections.Add(new SummarySection(null,
                [new SummaryRow(SummaryIcon.Status, _loc.GetString("Builder_Save_OwnInstallersNotOnMedia"), Tone: SummaryTone.Warning)]));
        }

        if (xml)
        {
            sections.Add(new SummarySection(_loc.GetString("AdvancedTools_Msg_NextSteps"),
            [
                new SummaryRow(SummaryIcon.Status, _loc.GetString("AdvancedTools_Msg_NextStep1")),
                new SummaryRow(SummaryIcon.Status, _loc.GetString("AdvancedTools_Msg_NextStep2")),
                new SummaryRow(SummaryIcon.Status, _loc.GetString("AdvancedTools_Msg_NextStep3")),
            ]));
        }

        if (misnamed)
            sections.Add(new SummarySection(null, [new SummaryRow(SummaryIcon.Status, _loc.GetString("AdvancedTools_Msg_RenameForSetup"))]));

        await _dialogs.ShowSummaryAsync(new SummaryRequest
        {
            Title = xml ? _loc.GetString("Dialog_Success") : _loc.GetString("Config_Export_Success_Title"),
            Message = xml
                ? _loc.GetString("AdvancedTools_Msg_XmlGenSuccess", path)
                : _loc.GetString("Config_Export_Success_Message", path),
            Verdict = hasFindings || (report is not null && noAppsVerdict is null)
                ? AnswerFileReportDialog.SummaryVerdict(report!, _loc)
                : noAppsVerdict,
            Sections = sections,
            CopyText = hasFindings ? string.Join(Environment.NewLine, AnswerFileReportDialog.Lines(report!, _loc)) : null,
        });
    }

    private async Task<AnswerFileReport?> CheckAnswerFileAsync(string path)
    {
        try
        {
            return await _answerFileValidator.ValidateAsync(path);
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Warning, $"Could not check {path}: {ex.Message}");
            return null;
        }
    }
}
