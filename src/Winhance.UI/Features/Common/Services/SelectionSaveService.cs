using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
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

public sealed class SelectionSaveService(
    ISelectionSetBuilder selectionSetBuilder,
    IConfigFileWriter configFiles,
    IAutounattendWriter autounattend,
    IFilePickerService picker,
    IFileStore fileStore,
    IWindowsThemeService theme,
    IFileSystemService files,
    IWimCustomizationService wim,
    IAppStagingService staging,
    ITaskProgressService progress,
    WimUtilSession session,
    IAnswerFileValidator answerFileValidator,
    IDialogService dialogService,
    ILocalizationService localizationService,
    ILogService logService) : ISelectionSaveService
{
    // Windows Setup only picks the answer file up under this exact name.
    private const string AutounattendFileName = "autounattend.xml";

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
                logService.Log(LogLevel.Info, $"{target} save: no destination chosen");
                return SelectionSaveResult.Nothing;
            }

            // Setup reads only autounattend.xml, but a renamed copy is still a fine archive, so the save goes
            // ahead and the success dialog says so. A fixed path skips this - the caller named the file itself.
            misnamed = target == BuilderTarget.Autounattend
                && !string.Equals(Path.GetFileName(path), AutounattendFileName, StringComparison.OrdinalIgnoreCase);
        }

        string? media = target == BuilderTarget.Autounattend ? options.MediaFolder ?? session.WorkingDirectory : null;

        // Staged before the writer reads the set: the file has to name where the album lands, not where it was authored.
        var toWrite = media is { Length: > 0 } ? StageAlbumOnMedia(selections, media) : selections;

        // Generating and checking an answer file takes seconds, so the progress bar covers both.
        bool xml = target == BuilderTarget.Autounattend;
        if (xml)
            progress.StartTask(localizationService.GetString("Builder_Save_SavingAnswerFile"), true);

        string written;
        AnswerFileReport? report = null;
        try
        {
            written = await WriteAsync(target, toWrite, path);
            logService.Log(LogLevel.Info, $"{target} saved to {written}");

            if (media is { Length: > 0 })
                await AddMediaStepsAsync(written, media);

            // Checked before the apps are staged: staging writes beside the file, never into it.
            if (xml && options.ReportSuccessInDialog)
                report = await CheckAnswerFileAsync(written, progress.GetCurrentCancellationToken());
        }
        catch
        {
            if (xml)
                progress.FailTask();
            throw;
        }

        if (xml)
            progress.CompleteTask();

        IReadOnlyList<StagedApp>? staged = media is { Length: > 0 }
            ? await StageAppsAsync(selections, media, options.ReportSuccessInDialog)
            : [];

        if (options.ReportSuccessInDialog)
        {
            // The file names the installers either way; only a save onto media copies them.
            bool ownInstallersNotOnMedia = media is not { Length: > 0 } && OwnInstaller.From(selections).Count > 0;
            await ReportSuccessAsync(target, written, report, misnamed, selections.WindowsApps.Count == 0, ownInstallersNotOnMedia);
        }

        return new SelectionSaveResult(written, staged ?? []) { StagingCancelled = staged is null };
    }

    // The save already worked, so a failed driver or update step is only logged.
    private async Task AddMediaStepsAsync(string written, string media)
    {
        try
        {
            await wim.EnsureDriverInstallStepAsync(written, media);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Could not add the driver install step to {written}: {ex.Message}");
        }

        try
        {
            await wim.EnsureUpdateInstallStepAsync(written, media);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Could not add the update install step to {written}: {ex.Message}");
        }
    }

    internal static string ReasonKey(StagingOutcome outcome) => outcome switch
    {
        StagingOutcome.OnMedia => "WIMUtil_Generate_Result_AppStaged",
        StagingOutcome.NeedsWinGet => "WIMUtil_Generate_Result_AppNeedsWinGet",
        StagingOutcome.DownloadFailed => "WIMUtil_Generate_Result_AppDownloadFailed",
        StagingOutcome.NoInstallerForArchitecture => "WIMUtil_Generate_Result_AppNoInstaller",
        StagingOutcome.NotFound => "WIMUtil_Generate_Result_AppNotFound",
        StagingOutcome.CopyFailed => "WIMUtil_Generate_Result_AppCopyFailed",
        _ => "WIMUtil_Generate_Result_AppNotStaged",
    };

    // The answer file is written and stays valid without the installers (the new PC downloads the catalog apps
    // instead), so a failure here warns and the save still returns its path. Null means the user cancelled.
    private async Task<IReadOnlyList<StagedApp>?> StageAppsAsync(SelectionSet selections, string media, bool reportInDialog)
    {
        IReadOnlyList<StagedApp> outcomes;
        try
        {
            if (ToStage(selections) is not { } toStage)
            {
                staging.RemoveStaged(media);
                return [];
            }

            progress.StartTask(localizationService.GetString("WIMUtil_Status_StagingApps"), false);
            outcomes = await staging.StageAsync(
                media,
                toStage,
                progress.CreateDetailedProgress(),
                progress.CurrentTaskCancellationSource!.Token);
            progress.CompleteTask();
        }
        catch (OperationCanceledException)
        {
            progress.CancelTask();
            return null;
        }
        catch (Exception ex)
        {
            progress.FailTask();
            logService.Log(LogLevel.Error, $"Could not put the apps on {media}: {ex.Message}", ex);
            await dialogService.ShowWarningAsync(ex.Message, localizationService.GetString("Dialog_Warning"));
            return [];
        }

        var notStaged = outcomes
            .Where(app => !app.Staged)
            .Select(app => localizationService.GetString("WIMUtil_Generate_Summary_AppNotStaged", app.Name, localizationService.GetString(ReasonKey(app.Outcome))))
            .ToList();

        // The format overload returns the raw text when a translation breaks the placeholder, so it cannot throw.
        if (reportInDialog && notStaged.Count > 0)
        {
            await dialogService.ShowInformationAsync(
                localizationService.GetString("WIMUtil_Msg_AppsNotStaged", string.Join("\n", notStaged)),
                localizationService.GetString("Dialog_Information"));
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
            ? picker.PickSavePath(
                localizationService.GetString("AdvancedTools_FileDialog_SaveXml"),
                "XML Files",
                "*.xml",
                AutounattendFileName,
                "xml")
            : picker.PickSavePath(
                localizationService.GetString("Config_FileDialog_SaveConfig"),
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

            if (!files.DirectoryExists(typed.Value))
            {
                // Still a correct choice on a PC that has the folder, so it travels as it was authored.
                logService.Log(LogLevel.Warning,
                    $"Slideshow album '{typed.Value}' is not on this PC; it was not copied onto the media.");
                continue;
            }

            string landing = theme.AlbumDestinationFor(typed.Value);
            string staged = files.CombinePath(
                workingDirectory, "sources", "$OEM$", "$$", "Web", "Wallpaper", "Winhance", files.GetFileName(landing));
            files.CreateDirectory(staged);

            foreach (var picture in files.GetFiles(typed.Value))
            {
                if (PictureFileConstants.IsPicture(picture))
                    files.CopyFile(picture, files.CombinePath(staged, files.GetFileName(picture)), overwrite: true);
            }

            rewritten ??= [.. set.Settings];
            rewritten[i] = choice with { Value = new ChoiceValue.Text(landing) };
        }

        return rewritten is null ? set : set with { Settings = rewritten };
    }

    private async Task<string> WriteAsync(BuilderTarget target, SelectionSet selections, string path)
    {
        var carried = await fileStore.LoadAsync(selections);

        if (target == BuilderTarget.Autounattend)
            return await autounattend.WriteAsync(carried, selectionSetBuilder.CurrentScope, path);

        await configFiles.WriteAsync(carried, selectionSetBuilder.CurrentScope, path);
        return path;
    }

    // The XML message names the file and spells out the steps to WIMUtil; it never offers to go there, so every
    // entry point reports the same save the same way. Neither an empty app list nor a finding stops the save: both
    // are shown on the one dialog and the user decides.
    private async Task ReportSuccessAsync(BuilderTarget target, string path, AnswerFileReport? report, bool misnamed, bool noWindowsApps, bool ownInstallersNotOnMedia)
    {
        bool xml = target == BuilderTarget.Autounattend;
        bool hasFindings = report is { Findings.Count: > 0 };

        SummaryVerdict? noAppsVerdict = noWindowsApps
            ? new SummaryVerdict(SummaryTone.Warning,
                localizationService.GetString("WIMUtil_Generate_Plan_NoWindowsApps"),
                xml
                    ? localizationService.GetString("WIMUtil_Generate_Plan_NoWindowsApps_Detail")
                    : localizationService.GetString("Config_Export_NoWindowsApps_Detail"))
            : null;

        var sections = new List<SummarySection>();
        if (hasFindings)
        {
            sections.Add(AnswerFileReportDialog.FindingsSection(report!, localizationService));
            if (noAppsVerdict is not null)
                sections.Add(new SummarySection(null, [new SummaryRow(SummaryIcon.Status, noAppsVerdict.Title, noAppsVerdict.Message, Tone: SummaryTone.Warning)]));
        }

        if (ownInstallersNotOnMedia)
        {
            sections.Add(new SummarySection(null,
                [new SummaryRow(SummaryIcon.Status, localizationService.GetString("Builder_Save_OwnInstallersNotOnMedia"), Tone: SummaryTone.Warning)]));
        }

        if (xml)
        {
            sections.Add(new SummarySection(localizationService.GetString("AdvancedTools_Msg_NextSteps"),
            [
                new SummaryRow(SummaryIcon.Status, localizationService.GetString("AdvancedTools_Msg_NextStep1")),
                new SummaryRow(SummaryIcon.Status, localizationService.GetString("AdvancedTools_Msg_NextStep2")),
                new SummaryRow(SummaryIcon.Status, localizationService.GetString("AdvancedTools_Msg_NextStep3")),
            ]));
        }

        if (misnamed)
            sections.Add(new SummarySection(null, [new SummaryRow(SummaryIcon.Status, localizationService.GetString("AdvancedTools_Msg_RenameForSetup"))]));

        await dialogService.ShowSummaryAsync(new SummaryRequest
        {
            Title = xml ? localizationService.GetString("Dialog_Success") : localizationService.GetString("Config_Export_Success_Title"),
            Message = xml
                ? localizationService.GetString("AdvancedTools_Msg_XmlGenSuccess", path)
                : localizationService.GetString("Config_Export_Success_Message", path),
            Verdict = hasFindings || (report is not null && noAppsVerdict is null)
                ? AnswerFileReportDialog.SummaryVerdict(report!, localizationService)
                : noAppsVerdict,
            Sections = sections,
            CopyText = hasFindings ? string.Join(Environment.NewLine, AnswerFileReportDialog.Lines(report!, localizationService)) : null,
        });
    }

    // Null when the check could not run, or the user cancelled it from the progress bar.
    private async Task<AnswerFileReport?> CheckAnswerFileAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            return await answerFileValidator.ValidateAsync(path, cancellationToken);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Could not check {path}: {ex.Message}");
            return null;
        }
    }
}
