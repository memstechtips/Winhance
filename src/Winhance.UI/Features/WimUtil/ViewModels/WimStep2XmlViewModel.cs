using System.Globalization;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.WimUtil.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.Features.WimUtil.ViewModels;

public partial class WimStep2XmlViewModel : ObservableObject, IDisposable
{
    private const string SetupLanguageId = "autounattend-setup-language";
    private const string EditionId = "autounattend-edition";
    private const string MediaEditionsId = "autounattend-media-editions";
    private const string MediaEditionsListId = "autounattend-media-editions-list";

    private readonly ISelectionSaveService _saves;
    private readonly IWimCustomizationService _wimCustomizationService;
    private readonly ISelectionSetBuilder _selections;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IFilePickerService _filePickerService;
    private readonly ILogService _logService;
    private readonly IResourceService _resourceService;
    private readonly IAnswerFileValidator _answerFileValidator;
    private readonly IAutounattendReader _autounattendReader;
    private readonly IApplicationModeService _applicationModeService;
    private readonly IBuilderModeEntry _builderModeEntry;
    private readonly IDispatcherService _dispatcherService;
    private readonly IEventBus _eventBus;
    private readonly AnswerFileCheckState _checkState;
    private readonly WimUtilSession _session;
    private readonly IWimImageService _wimImageService;
    private readonly ITargetWindowsService _targetWindows;
    private readonly ITaskProgressService _taskProgressService;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _disposed;
    private bool _startedHere;
    private string _workingDirectory = string.Empty;

    // Step 1 can move the folder while the session is open, and the next save must target the media being built now.
    public string WorkingDirectory
    {
        get => _workingDirectory;
        set
        {
            _workingDirectory = value;
            if (RecordsFolder) _session.WorkingDirectory = value;
        }
    }

    [ObservableProperty]
    public partial string SelectedXmlPath { get; set; }

    [ObservableProperty]
    public partial string XmlStatus { get; set; }

    [ObservableProperty]
    public partial bool IsXmlAdded { get; set; }

    [ObservableProperty]
    public partial bool IsRemovingEditions { get; set; }

    public WizardActionCard GenerateWinhanceXmlCard { get; private set; } = new();
    public WizardActionCard DownloadXmlCard { get; private set; } = new();
    public WizardActionCard SelectXmlCard { get; private set; } = new();

    public bool IsAutounattendSession =>
        _applicationModeService.CurrentMode == WinhanceMode.Builder
        && _applicationModeService.CurrentBuilderTarget == BuilderTarget.Autounattend;

    public string StartButtonText => _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Start");

    public WimStep2XmlViewModel(
        ISelectionSaveService saves,
        IWimCustomizationService wimCustomizationService,
        ISelectionSetBuilder selections,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IFileSystemService fileSystemService,
        IFilePickerService filePickerService,
        ILogService logService,
        IResourceService resourceService,
        IAnswerFileValidator answerFileValidator,
        IAutounattendReader autounattendReader,
        IApplicationModeService applicationModeService,
        IBuilderModeEntry builderModeEntry,
        IDispatcherService dispatcherService,
        IEventBus eventBus,
        AnswerFileCheckState checkState,
        WimUtilSession session,
        IWimImageService wimImageService,
        ITargetWindowsService targetWindows,
        ITaskProgressService taskProgressService)
    {
        _saves = saves;
        _wimCustomizationService = wimCustomizationService;
        _selections = selections;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _fileSystemService = fileSystemService;
        _filePickerService = filePickerService;
        _logService = logService;
        _resourceService = resourceService;
        _answerFileValidator = answerFileValidator;
        _autounattendReader = autounattendReader;
        _applicationModeService = applicationModeService;
        _builderModeEntry = builderModeEntry;
        _dispatcherService = dispatcherService;
        _eventBus = eventBus;
        _checkState = checkState;
        _session = session;
        _wimImageService = wimImageService;
        _targetWindows = targetWindows;
        _taskProgressService = taskProgressService;

        SelectedXmlPath = string.Empty;
        XmlStatus = _localizationService.GetString("WIMUtil_Status_NoXmlAdded");

        CreateActionCards();

        _applicationModeService.ModeChanged += OnModeChanged;
    }

    // The bar can open an Autounattend session on its own, and a save from there must not pick up a stale WIMUtil folder.
    private bool RecordsFolder => IsAutounattendSession && _startedHere;

    private void OnModeChanged(object? sender, EventArgs e)
    {
        if (!IsAutounattendSession)
            _startedHere = false;

        _session.WorkingDirectory = RecordsFolder ? WorkingDirectory : null;
        if (!RecordsFolder)
            _session.Editions = [];

        _dispatcherService.RunOnUIThreadAsync(() =>
        {
            OnPropertyChanged(nameof(IsAutounattendSession));
            GenerateWinhanceXmlCard.Description = GenerateCardDescription();
            return Task.CompletedTask;
        }).FireAndForget(_logService);
    }

    private string GenerateCardDescription() => IsAutounattendSession
        ? _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Session_Description")
        : _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Snapshot_Description");

    private void CreateActionCards()
    {
        GenerateWinhanceXmlCard = new WizardActionCard
        {
            Icon = "\uE710",
            Title = _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Title"),
            Description = GenerateCardDescription(),
            ButtonText = _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Button"),
            ButtonCommand = GenerateWinhanceXmlCommand,
            IsEnabled = true
        };

        DownloadXmlCard = new WizardActionCard
        {
            IconPath = _resourceService.GetResourceIconPath("FileDownloadIconPath"),
            Title = _localizationService.GetString("WIMUtil_Card_DownloadXML_Title"),
            Description = _localizationService.GetString("WIMUtil_Card_DownloadXML_Description"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_DownloadXML_Button"),
            ButtonCommand = DownloadUnattendedWinstallXmlCommand,
            IsEnabled = true
        };

        SelectXmlCard = new WizardActionCard
        {
            IconPath = _resourceService.GetResourceIconPath("AutounattendXmlIconPath"),
            Title = _localizationService.GetString("WIMUtil_Card_SelectXML_Title"),
            Description = _localizationService.GetString("WIMUtil_Card_SelectXML_Description"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_SelectXML_Button"),
            ButtonCommand = SelectXmlFileCommand,
            IsEnabled = true
        };
    }

    [RelayCommand]
    private async Task GenerateWinhanceXml()
    {
        try
        {
            GenerateWinhanceXmlCard.IsComplete = false;
            GenerateWinhanceXmlCard.HasFailed = false;

            if (string.IsNullOrEmpty(WorkingDirectory))
            {
                await _dialogService.ShowWarningAsync(
                    _localizationService.GetString("WIMUtil_Msg_WorkingDirectoryRequired"),
                    _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
                return;
            }

            if (!IsAutounattendSession)
            {
                await _dialogService.ShowWarningAsync(
                    _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Start"),
                    _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
                return;
            }

            var set = await _selections.FromBuilderSessionAsync();
            var outputPath = _fileSystemService.CombinePath(WorkingDirectory, "autounattend.xml");
            var editions = PlanEditions(set);

            if (!await ConfirmGenerateAsync(set, outputPath, editions)) return;

            XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlGenerating");
            var saved = await _saves.SaveAsync(BuilderTarget.Autounattend, set, new SelectionSaveOptions
            {
                FixedPath = outputPath,
                MediaFolder = WorkingDirectory,
                ReportSuccessInDialog = false,
            });

            if (saved.Path == null) return;

            SelectedXmlPath = saved.Path;
            IsXmlAdded = true;
            var removal = editions.Removed.Count > 0 ? await KeepEditionsAsync(editions) : null;
            XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlGenSuccess");
            ClearOtherXmlCardCompletions("generate");
            GenerateWinhanceXmlCard.IsComplete = true;
            await ShowGenerateSummaryAsync(saved, await RunAnswerFileCheckAsync(), editions, removal);
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error generating XML: {ex.Message}", ex);
            XmlStatus = string.Format(_localizationService.GetString("WIMUtil_Status_XmlGenFailed"), ex.Message);
            GenerateWinhanceXmlCard.HasFailed = true;
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_XmlGenError"), ex.Message),
                _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
        }
    }

    [RelayCommand]
    private async Task StartAutounattendSessionAsync()
    {
        var language = await MediaLanguageAsync();
        var image = await MediaImageAsync();
        int? build = image is { Build: > 0 } ? image.Build : null;
        var edition = image?.Editions is [var only] ? MediaEditions.OptionFor(only.EditionId) : null;

        // Already in Builder: switching the target keeps what the session holds, where entering again clears it.
        _startedHere = true;
        _session.Editions = image?.Editions ?? [];
        if (_applicationModeService.CurrentMode == WinhanceMode.Builder)
        {
            _applicationModeService.SetBuilderTarget(BuilderTarget.Autounattend);
            _session.WorkingDirectory = RecordsFolder ? WorkingDirectory : null;
            PreselectFromMedia(language, edition);
            await TargetMediaBuildAsync(build);
            return;
        }

        if (await _builderModeEntry.EnterAsync(BuilderTarget.Autounattend))
        {
            PreselectFromMedia(language, edition);
            await TargetMediaBuildAsync(build);
        }
        else
        {
            _startedHere = false;
            _session.Editions = [];
        }
    }

    private async Task<ImageFormatInfo?> MediaImageAsync() =>
        string.IsNullOrEmpty(WorkingDirectory) ? null : await _wimImageService.DetectImageFormatAsync(WorkingDirectory);

    // The media is the Windows the file will install, so its build beats the default of the newest release.
    private async Task TargetMediaBuildAsync(int? build)
    {
        if (build is { } b)
            await _targetWindows.SetAsync(b);
    }

    // Setup can only show a language the media carries, so media with exactly one leaves nothing to ask.
    private async Task<CultureInfo?> MediaLanguageAsync()
    {
        if (string.IsNullOrEmpty(WorkingDirectory))
            return null;

        var langIni = _fileSystemService.CombinePath(WorkingDirectory, "sources", "lang.ini");
        try
        {
            return _fileSystemService.FileExists(langIni)
                && MediaLanguages.Parse(await _fileSystemService.ReadAllTextAsync(langIni)) is [var only]
                    ? only
                    : null;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not read the languages on the media: {ex.Message}");
            return null;
        }
    }

    // A language or edition the user already picked in this session wins over the media's. Media holding one edition
    // gets that edition's install key, or Setup would stop to ask which edition to install.
    private void PreselectFromMedia(CultureInfo? language, int? editionOption)
    {
        var seeded = false;
        if (language is not null && _applicationModeService.GetBuilderEdit(SetupLanguageId) is null)
        {
            _applicationModeService.RecordBuilderEdit(
                new SettingChoice(SetupLanguageId, new ChoiceValue.Keyed(language.Name, language.DisplayName)));
            seeded = true;
        }

        if (editionOption is { } option && _applicationModeService.GetBuilderEdit(EditionId) is null)
        {
            _applicationModeService.RecordBuilderEdit(new SettingChoice(EditionId, new ChoiceValue.Option(option)));
            seeded = true;
        }

        if (seeded)
            _eventBus.Publish(new BuilderSeededEvent());
    }

    [RelayCommand]
    private async Task DownloadUnattendedWinstallXml()
    {
        try
        {
            DownloadXmlCard.IsComplete = false;
            DownloadXmlCard.HasFailed = false;

            if (string.IsNullOrEmpty(WorkingDirectory))
            {
                await _dialogService.ShowWarningAsync(
                    _localizationService.GetString("WIMUtil_Msg_WorkingDirectoryRequired"),
                    _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
                return;
            }

            var destinationPath = _fileSystemService.CombinePath(WorkingDirectory, "autounattend.xml");
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            var progress = new Progress<TaskProgressDetail>(detail => XmlStatus = detail.StatusText ?? _localizationService.GetString("WIMUtil_Status_XmlDownloading"));

            XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlDownloadStart");
            await _wimCustomizationService.DownloadUnattendedWinstallXmlAsync(destinationPath, progress, _cancellationTokenSource.Token);

            var addResult = await _wimCustomizationService.AddXmlToImageAsync(destinationPath, WorkingDirectory);
            if (addResult.Success)
            {
                SelectedXmlPath = destinationPath;
                IsXmlAdded = true;
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlDownloadSuccess");
                ClearOtherXmlCardCompletions("download");
                DownloadXmlCard.IsComplete = true;
                await CheckAnswerFileAsync();
            }
            else
            {
                DownloadXmlCard.HasFailed = true;
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlAddFailed");
                await _dialogService.ShowErrorAsync(
                    string.Format(_localizationService.GetString("WIMUtil_Msg_XmlAddFailed"), addResult.ErrorMessage),
                    _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error downloading XML: {ex.Message}", ex);
            XmlStatus = string.Format(_localizationService.GetString("WIMUtil_Status_XmlDownloadFailed"), ex.Message);
            DownloadXmlCard.HasFailed = true;
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_XmlDownloadError"), ex.Message),
                _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
        }
    }

    [RelayCommand]
    private async Task SelectXmlFile()
    {
        try
        {
            SelectXmlCard.IsComplete = false;
            SelectXmlCard.HasFailed = false;

            if (string.IsNullOrEmpty(WorkingDirectory))
            {
                await _dialogService.ShowWarningAsync(
                    _localizationService.GetString("WIMUtil_Msg_WorkingDirectoryRequired"),
                    _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
                return;
            }

            var selectedPath = _filePickerService.PickFile(
                ["XML Files", "*.xml"],
                _localizationService.GetString("WIMUtil_FileDialog_SelectXml"));
            if (string.IsNullOrEmpty(selectedPath)) return;

            XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlValidating");
            var isValidXml = await ValidateXmlFile(selectedPath);
            if (!isValidXml)
            {
                SelectXmlCard.HasFailed = true;
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlInvalid");
                await PublishRejectedFileReportAsync(selectedPath);
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_XmlInvalidError"),
                    _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
                return;
            }

            XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlAdding");
            var addResult = await _wimCustomizationService.AddXmlToImageAsync(selectedPath, WorkingDirectory);
            if (addResult.Success)
            {
                SelectedXmlPath = selectedPath;
                IsXmlAdded = true;
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlSelectSuccess");
                ClearOtherXmlCardCompletions("select");
                SelectXmlCard.IsComplete = true;
                await CheckAnswerFileAsync();
                await OpenWinhanceFileAsync(selectedPath);
            }
            else
            {
                SelectXmlCard.HasFailed = true;
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlValidAddFailed");
                await _dialogService.ShowErrorAsync(
                    string.Format(_localizationService.GetString("WIMUtil_Msg_XmlValidAddFailed"), addResult.ErrorMessage),
                    _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error selecting XML: {ex.Message}", ex);
            XmlStatus = string.Format(_localizationService.GetString("WIMUtil_Status_ErrorPrefix"), ex.Message);
            SelectXmlCard.HasFailed = true;
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_XmlSelectError"), ex.Message),
                _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
        }
    }

    [RelayCommand]
    private async Task OpenSchneegansXmlGenerator()
    {
        try { await Windows.System.Launcher.LaunchUriAsync(new Uri("https://schneegans.de/windows/unattend-generator/")); }
        catch (Exception ex) { _logService.LogError($"Error opening Schneegans XML generator: {ex.Message}", ex); }
    }

    // A file Winhance made carries the choices behind it, so it can reopen as a session to review or change.
    private async Task OpenWinhanceFileAsync(string path)
    {
        WinhanceAnswerFile? made;
        try
        {
            made = await _autounattendReader.ReadAsync(path);
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not read the choices in {path}: {ex.Message}");
            return;
        }

        if (made is null)
            return;

        var title = _localizationService.GetString("WIMUtil_OpenWinhanceFile_Title");
        if (made.Config is null)
        {
            await _dialogService.ShowInformationAsync(
                _localizationService.GetString("WIMUtil_OpenWinhanceFile_NoChoices", made.Version), title);
            return;
        }

        var message = _localizationService.GetString("WIMUtil_OpenWinhanceFile_Message");
        if (_applicationModeService.CurrentMode == WinhanceMode.Builder && _applicationModeService.HasBuilderChanges)
            message += "\n\n" + _localizationService.GetString("WIMUtil_OpenWinhanceFile_Replaces");

        var answer = await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
        {
            Title = title,
            Message = message,
            ConfirmButtonText = _localizationService.GetString("Button_Yes"),
            CancelButtonText = _localizationService.GetString("Button_No"),
        });
        if (!answer.Confirmed)
            return;

        _startedHere = true;
        _session.Editions = (await MediaImageAsync())?.Editions ?? [];
        if (!await _builderModeEntry.EnterFromAsync(BuilderTarget.Autounattend, made.Config))
        {
            _startedHere = false;
            _session.Editions = [];
        }
    }

    // The refused file never reaches the media, but the banner still shows where it breaks.
    private async Task PublishRejectedFileReportAsync(string selectedPath)
    {
        try
        {
            var report = await _answerFileValidator.ValidateAsync(selectedPath);
            _checkState.Publish(selectedPath, report);
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not check the selected file: {ex.Message}");
        }
    }

    private async Task<bool> ValidateXmlFile(string xmlPath)
    {
        try
        {
            await Task.Run(() => XDocument.Load(xmlPath));
            return true;
        }
        catch (Exception ex) { _logService.LogDebug($"XML validation failed for '{xmlPath}': {ex.Message}"); return false; }
    }

    // Generate downloads installers onto the media, which is a surprise to anyone who did not expect a
    // network round trip, so everything that is about to happen is named before any of it starts. An
    // empty Windows Apps list is a warning on this dialog rather than a second question after it.
    private async Task<bool> ConfirmGenerateAsync(SelectionSet set, string outputPath, EditionPlan editions)
    {
        var rows = new List<SummaryRow>
        {
            new(SummaryIcon.WriteFile, _localizationService.GetString("WIMUtil_Generate_Plan_Write"), outputPath),
        };

        if (SelectionSaveService.ToStage(set) is { } toStage)
        {
            rows.Add(new(SummaryIcon.Download,
                _localizationService.GetString("WIMUtil_Generate_Plan_Download"),
                _localizationService.GetString(
                    "WIMUtil_Generate_Plan_Download_Detail",
                    string.Join(", ", toStage.ExternalApps.Select(app => app.Name).Append("Winhance")))));
        }

        var ownInstallers = OwnInstaller.From(set);
        if (ownInstallers.Count > 0)
        {
            rows.Add(new(SummaryIcon.CopyFiles,
                _localizationService.GetString("WIMUtil_Generate_Plan_OwnInstallers"),
                string.Join(", ", ownInstallers.Select(i => Path.GetFileName(i.FilePath)))));
        }

        if (editions.Removed.Count > 0)
        {
            rows.Add(new(SummaryIcon.CopyFiles,
                _localizationService.GetString("WIMUtil_Generate_Plan_RemoveEditions"),
                _localizationService.GetString("WIMUtil_Generate_Plan_RemoveEditions_Detail", string.Join(", ", editions.Removed.Select(e => e.Name)))));
        }

        if (editions.RemovesChosen)
        {
            rows.Add(new(SummaryIcon.Status,
                _localizationService.GetString("WIMUtil_Generate_Plan_ChosenEditionRemoved"),
                _localizationService.GetString("WIMUtil_Generate_Plan_ChosenEditionRemoved_Detail"),
                Tone: SummaryTone.Warning));
        }

        if (editions.NoneMatched)
        {
            rows.Add(new(SummaryIcon.Status,
                _localizationService.GetString("WIMUtil_Generate_Plan_NoEditionLeft"),
                _localizationService.GetString("WIMUtil_Generate_Plan_NoEditionLeft_Detail"),
                Tone: SummaryTone.Warning));
        }

        rows.Add(new(SummaryIcon.Check,
            _localizationService.GetString("WIMUtil_Generate_Plan_Check"),
            _localizationService.GetString("WIMUtil_Generate_Plan_Check_Detail")));

        return await _dialogService.ShowSummaryAsync(new SummaryRequest
        {
            Title = _localizationService.GetString("WIMUtil_Generate_Confirm_Title"),
            Message = _localizationService.GetString("WIMUtil_Generate_Confirm_Message"),
            Verdict = set.WindowsApps.Count == 0
                ? new SummaryVerdict(SummaryTone.Warning,
                    _localizationService.GetString("WIMUtil_Generate_Plan_NoWindowsApps"),
                    _localizationService.GetString("WIMUtil_Generate_Plan_NoWindowsApps_Detail"))
                : null,
            Sections = [new SummarySection(null, rows)],
            ConfirmButtonText = _localizationService.GetString("Button_Continue"),
        });
    }

    // One report for the whole run, because a green check beside the button says nothing about the
    // installers or the check. Every item names its own outcome, including the ones that failed.
    private async Task ShowGenerateSummaryAsync(
        SelectionSaveResult saved, AnswerFileReport? report, EditionPlan editions, EditionOutcome? removal)
    {
        var sections = new List<SummarySection>
        {
            new(_localizationService.GetString("WIMUtil_Generate_Result_FileHeading"),
                [new SummaryRow(SummaryIcon.WriteFile, Path.GetFileName(saved.Path!), Path.GetDirectoryName(saved.Path!))]),
        };
        var copy = new List<string> { _localizationService.GetString("WIMUtil_Generate_Summary_Generated"), saved.Path! };

        if (saved.Apps.Count > 0)
        {
            sections.Add(new(_localizationService.GetString("WIMUtil_Generate_Result_AppsHeading"),
                saved.Apps
                    .Select(app => app.Staged
                        ? new SummaryRow(SummaryIcon.Status, app.Name, _localizationService.GetString("WIMUtil_Generate_Result_AppStaged"), Tone: SummaryTone.Success)
                        : new SummaryRow(SummaryIcon.Status, app.Name, _localizationService.GetString("WIMUtil_Generate_Result_AppNotStaged"), Tone: SummaryTone.Warning))
                    .ToList()));

            copy.Add(string.Empty);
            copy.Add(_localizationService.GetString("WIMUtil_Generate_Summary_AppsHeading"));
            copy.AddRange(saved.Apps.Select(app => app.Staged
                ? _localizationService.GetString("WIMUtil_Generate_Summary_AppStaged", app.Name)
                : _localizationService.GetString("WIMUtil_Generate_Summary_AppNotStaged", app.Name)));
        }

        if (removal is not null)
        {
            var rows = new List<SummaryRow>();
            copy.Add(string.Empty);
            copy.Add(_localizationService.GetString("WIMUtil_Generate_Summary_EditionsHeading"));
            if (removal.Result.Removed)
            {
                rows.AddRange(editions.Kept.Select(e => new SummaryRow(SummaryIcon.Status, e.Name, _localizationService.GetString("WIMUtil_Generate_Result_EditionKept"), Tone: SummaryTone.Success)));
                rows.AddRange(editions.Removed.Select(e => new SummaryRow(SummaryIcon.Status, e.Name, _localizationService.GetString("WIMUtil_Generate_Result_EditionRemoved"))));
                copy.AddRange(editions.Kept.Select(e => _localizationService.GetString("WIMUtil_Generate_Summary_EditionKept", e.Name)));
                copy.AddRange(editions.Removed.Select(e => _localizationService.GetString("WIMUtil_Generate_Summary_EditionRemoved", e.Name)));

                if (removal.Result.LeftoverImage is { } leftover)
                {
                    var oldImageLeft = _localizationService.GetString("WIMUtil_Generate_Summary_OldImageLeft", leftover);
                    rows.Add(new SummaryRow(SummaryIcon.Status, oldImageLeft, Tone: SummaryTone.Warning));
                    copy.Add(oldImageLeft);
                }
            }
            else
            {
                var notRemoved = removal.Cancelled
                    ? _localizationService.GetString("WIMUtil_Msg_RemoveEditionsCancelled")
                    : _localizationService.GetString("WIMUtil_Msg_RemoveEditionsFailed");
                rows.Add(new SummaryRow(SummaryIcon.Status, notRemoved, Tone: SummaryTone.Warning));
                copy.Add(notRemoved);
            }

            sections.Add(new(_localizationService.GetString("WIMUtil_Generate_Result_EditionsHeading"), rows));
        }

        copy.Add(string.Empty);
        SummaryVerdict verdict;
        if (report is null)
        {
            verdict = new SummaryVerdict(SummaryTone.Warning, _localizationService.GetString("WIMUtil_Generate_Summary_CheckFailed"));
            copy.Add(verdict.Title);
        }
        else
        {
            verdict = AnswerFileReportDialog.SummaryVerdict(report, _localizationService);
            if (report.Findings.Count > 0)
                sections.Add(AnswerFileReportDialog.FindingsSection(report, _localizationService));
            copy.AddRange(AnswerFileReportDialog.Lines(report, _localizationService));
        }

        await _dialogService.ShowSummaryAsync(new SummaryRequest
        {
            Title = _localizationService.GetString("WIMUtil_Generate_Summary_Title"),
            Verdict = verdict,
            Sections = sections,
            CopyText = string.Join(Environment.NewLine, copy),
        });
    }

    // Kept is every edition when the choice keeps none, so a slip in the checklist never empties the media.
    private EditionPlan PlanEditions(SelectionSet set)
    {
        var editions = _session.Editions;
        int? chosen = set.Value(EditionId) is ChoiceValue.Option { Index: var option } ? option : null;

        // An edition outside the table has no option either, and must not pass for a choice that names none.
        bool IsChosen(ImageEdition e) => chosen is { } picked && MediaEditions.OptionFor(e.EditionId) == picked;

        var keep = set.StateOf(MediaEditionsId)?.Label;
        var kept =
            keep == LocKey.Setting.AutounattendMediaEditions.Option1
                ? editions.Where(IsChosen).ToList()
            : keep == LocKey.Setting.AutounattendMediaEditions.Option2 && set.Value(MediaEditionsListId) is ChoiceValue.Keys ticked
                ? editions.Where(e => ticked.Values.Contains(e.Name)).ToList()
            : editions.ToList();

        if (kept.Count == 0)
            return new EditionPlan(editions, [], NoneMatched: editions.Count > 0, RemovesChosen: false);

        var removed = editions.Except(kept).ToList();
        return new EditionPlan(kept, removed, NoneMatched: false,
            RemovesChosen: removed.Any(IsChosen) && !kept.Any(IsChosen));
    }

    // The file is already on the media, so a failed or cancelled removal still leaves media that installs, with
    // every edition on it. The image is being rewritten until this returns, which is what locks the other steps.
    private async Task<EditionOutcome> KeepEditionsAsync(EditionPlan editions)
    {
        var status = _localizationService.GetString("WIMUtil_Status_RemovingEditions");
        XmlStatus = status;
        IsRemovingEditions = true;
        GenerateWinhanceXmlCard.IsProcessing = true;
        try
        {
            var cancellation = _taskProgressService.StartTask(status, true);
            var result = await _wimImageService.KeepEditionsAsync(
                WorkingDirectory,
                [.. editions.Kept.Select(e => e.Name)],
                _taskProgressService.CreateDetailedProgress(),
                cancellation.Token);

            if (result.Removed)
                _session.Editions = (await MediaImageAsync())?.Editions ?? editions.Kept;
            else
                _taskProgressService.FailTask();

            return new EditionOutcome(result, Cancelled: false);
        }
        catch (OperationCanceledException)
        {
            _taskProgressService.CancelTask();
            return new EditionOutcome(EditionRemovalResult.Failed, Cancelled: true);
        }
        catch (Exception ex)
        {
            _taskProgressService.FailTask();
            _logService.LogError($"Error removing editions: {ex.Message}", ex);
            return new EditionOutcome(EditionRemovalResult.Failed, Cancelled: false);
        }
        finally
        {
            IsRemovingEditions = false;
            GenerateWinhanceXmlCard.IsProcessing = false;
            _taskProgressService.CompleteTask();
        }
    }

    private sealed record EditionPlan(
        IReadOnlyList<ImageEdition> Kept, IReadOnlyList<ImageEdition> Removed, bool NoneMatched, bool RemovesChosen);

    private sealed record EditionOutcome(EditionRemovalResult Result, bool Cancelled);

    // The copy on the media is what Setup reads, so that is the one checked. The verdict never
    // undoes the card: the user sees the findings and decides.
    private async Task CheckAnswerFileAsync()
    {
        var report = await RunAnswerFileCheckAsync();
        if (report is { Findings.Count: > 0 })
        {
            await _dialogService.ShowSummaryAsync(AnswerFileReportDialog.Request(report, _localizationService));
        }
    }

    // Null means the check itself could not run, which is not the same as a clean file and is reported as its own line.
    private async Task<AnswerFileReport?> RunAnswerFileCheckAsync()
    {
        var previousStatus = XmlStatus;
        try
        {
            _checkState.Publish(null, null);
            XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlChecking");
            var answerFile = _fileSystemService.CombinePath(WorkingDirectory, "autounattend.xml");
            var report = await _answerFileValidator.ValidateAsync(answerFile);
            XmlStatus = previousStatus;
            _checkState.Publish(answerFile, report);
            return report;
        }
        catch (Exception ex)
        {
            XmlStatus = previousStatus;
            _logService.LogWarning($"Could not check autounattend.xml: {ex.Message}");
            return null;
        }
    }

    internal void ClearOtherXmlCardCompletions(string exceptCard)
    {
        if (exceptCard != "generate") GenerateWinhanceXmlCard.IsComplete = false;
        if (exceptCard != "download") DownloadXmlCard.IsComplete = false;
        if (exceptCard != "select") SelectXmlCard.IsComplete = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _applicationModeService.ModeChanged -= OnModeChanged;
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        GC.SuppressFinalize(this);
    }

}
