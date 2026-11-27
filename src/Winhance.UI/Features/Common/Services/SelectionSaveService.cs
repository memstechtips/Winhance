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

        string? media = target == BuilderTarget.Autounattend ? options.MediaFolder : null;
        string? appMedia = target == BuilderTarget.Autounattend ? media ?? session.WorkingDirectory : null;

        // Generating and checking an answer file takes seconds, so the progress bar covers both.
        bool xml = target == BuilderTarget.Autounattend;
        if (xml)
            progress.StartTask(localizationService.GetString("Builder_Save_SavingAnswerFile"), true);

        string written;
        AnswerFileReport? report = null;
        // Validate a replacement beside the existing file before publishing it. Sidecar paths stay relative to the same folder.
        bool mediaBound = media is { Length: > 0 };
        string writePath = xml && !mediaBound && files.FileExists(path) ? path + ".tmp-" + Guid.NewGuid().ToString("N") : path;
        try
        {
            written = await WriteAsync(target, selections, writePath, media, async staged =>
            {
                await AddMediaStepsAsync(staged, media!);
                report = await answerFileValidator.ValidateAsync(staged, progress.GetCurrentCancellationToken());
                report.ThrowIfInvalidGenerated();
            });
            logService.Log(LogLevel.Info, $"{target} saved to {written}");

            // Checked before the apps are staged: staging writes beside the file, never into it.
            if (xml && !mediaBound)
            {
                report = await answerFileValidator.ValidateAsync(written, progress.GetCurrentCancellationToken());
                report.ThrowIfInvalidGenerated();
                if (writePath != path)
                {
                    files.SetFileAttributes(path, FileAttributes.Normal);
                    files.CopyFile(writePath, path, overwrite: true);
                    written = path;
                }
            }
        }
        catch
        {
            if (xml)
                progress.FailTask();
            if (xml && !mediaBound && writePath == path && files.FileExists(writePath))
                files.DeleteFile(writePath);
            throw;
        }
        finally
        {
            if (writePath != path && files.FileExists(writePath))
                files.DeleteFile(writePath);
        }

        if (xml)
            progress.CompleteTask();

        IReadOnlyList<StagedApp>? staged = appMedia is { Length: > 0 }
            ? await StageAppsAsync(selections, appMedia, options.ReportSuccessInDialog)
            : [];

        if (options.ReportSuccessInDialog)
        {
            // The file names the installers either way; only a save onto media copies them.
            bool ownInstallersNotOnMedia = appMedia is not { Length: > 0 } && OwnInstaller.From(selections).Count > 0;
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

    private async Task<SelectionSet> CarryAlbumsAsync(SelectionSet set)
    {
        List<SettingChoice>? rewritten = null;
        List<CarriedFile>? carried = null;
        for (int i = 0; i < set.Settings.Count; i++)
        {
            var choice = set.Settings[i];
            if (choice.Value is not ChoiceValue.Text { Value.Length: > 0 } typed
                || SettingCatalog.Find(choice.SettingId) is not { } setting
                || !setting.Targets.OfType<DesktopSlideshowTarget>().Any())
                continue;

            if (!files.DirectoryExists(typed.Value))
                throw new IOException($"Slideshow album '{typed.Value}' is not available to copy onto the media.");

            var pictures = files.GetFiles(typed.Value).Where(PictureFileConstants.IsPicture).ToList();
            if (pictures.Count == 0)
                throw new IOException($"Slideshow album '{typed.Value}' contains no pictures to copy onto the media.");
            string landing = theme.AlbumDestinationFor(typed.Value);
            carried ??= [.. set.Files];
            foreach (var picture in pictures)
            {
                var bytes = await files.ReadAllBytesAsync(picture);
                carried.Add(new CarriedFile(choice.SettingId, files.CombinePath(landing, files.GetFileName(picture)), Convert.ToBase64String(bytes)));
            }
            rewritten ??= [.. set.Settings];
            rewritten[i] = choice with { Value = new ChoiceValue.Text(landing) };
        }
        return rewritten is null ? set : set with { Settings = rewritten, Files = carried! };
    }

    private async Task<string> WriteAsync(BuilderTarget target, SelectionSet selections, string path, string? media, Func<string, Task> validateMedia)
    {
        var carried = (await fileStore.LoadAsync(selections)).WithOnlySelectedPicture();
        if (target == BuilderTarget.Autounattend)
        {
            bool mediaBound = media is { Length: > 0 };
            if (mediaBound)
            {
                foreach (var choice in selections.Settings)
                {
                    if (choice.Value is ChoiceValue.Keyed { Key.Length: > 0 } keyed
                        && SettingCatalog.Find(choice.SettingId)?.Options?.Source == OptionSource.Pictures
                        && theme.Travels(keyed.Key)
                        && !carried.Files.Any(file => file.SettingId == choice.SettingId && file.Base64.Length > 0))
                        throw new IOException($"Wallpaper '{keyed.Key}' is not available to copy onto the media.");
                }
                carried = await CarryAlbumsAsync(carried);
            }
            return media is { Length: > 0 }
                ? await autounattend.WriteMediaAsync(carried, selectionSetBuilder.CurrentScope, path, media, validateMedia, progress.GetCurrentCancellationToken())
                : await autounattend.WriteAsync(carried, selectionSetBuilder.CurrentScope, path);
        }

        await configFiles.WriteAsync(carried, selectionSetBuilder.CurrentScope, path);
        return path;
    }

    // The XML message names the file and spells out the steps to WIMUtil; it never offers to go there, so every
    // entry point reports the same save the same way. An empty app list and advisory findings
    // are shown on the one dialog. Blocking findings have already stopped the save.
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

}
