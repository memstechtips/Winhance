using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.WimUtil.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.Features.WimUtil.ViewModels;

public partial class WimStep2XmlViewModel : ObservableObject, IDisposable
{
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
    private readonly IApplicationModeService _applicationModeService;
    private readonly IBuilderModeEntry _builderModeEntry;
    private readonly IDispatcherService _dispatcherService;
    private readonly AnswerFileCheckState _checkState;
    private readonly WimUtilSession _session;
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
        IApplicationModeService applicationModeService,
        IBuilderModeEntry builderModeEntry,
        IDispatcherService dispatcherService,
        AnswerFileCheckState checkState,
        WimUtilSession session)
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
        _applicationModeService = applicationModeService;
        _builderModeEntry = builderModeEntry;
        _dispatcherService = dispatcherService;
        _checkState = checkState;
        _session = session;

        SelectedXmlPath = string.Empty;
        XmlStatus = _localizationService.GetString("WIMUtil_Status_NoXmlAdded");

        CreateActionCards();

        _applicationModeService.ModeChanged += OnModeChanged;
    }

    // The bar can open an Autounattend session on its own, and a save from there must not pick up a stale WIMUtil folder.
    private bool RecordsFolder => IsAutounattendSession && _startedHere;

    private void OnModeChanged(object? sender, EventArgs e)
    {
        if (!IsAutounattendSession) _startedHere = false;
        _session.WorkingDirectory = RecordsFolder ? WorkingDirectory : null;

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

            XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlGenerating");
            var set = await _selections.FromBuilderSessionAsync();

            var outputPath = _fileSystemService.CombinePath(WorkingDirectory, "autounattend.xml");
            string? generatedPath = await _saves.SaveAsync(BuilderTarget.Autounattend, set, new SelectionSaveOptions
            {
                FixedPath = outputPath,
                MediaFolder = WorkingDirectory,
                ReportSuccessInDialog = false,
            });

            if (generatedPath == null) return;

            SelectedXmlPath = generatedPath;
            IsXmlAdded = true;
            XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlGenSuccess");
            ClearOtherXmlCardCompletions("generate");
            GenerateWinhanceXmlCard.IsComplete = true;
            await CheckAnswerFileAsync();
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
        // Already in Builder: switching the target keeps what the session holds, where entering again clears it.
        _startedHere = true;
        if (_applicationModeService.CurrentMode == WinhanceMode.Builder)
        {
            _applicationModeService.SetBuilderTarget(BuilderTarget.Autounattend);
            _session.WorkingDirectory = RecordsFolder ? WorkingDirectory : null;
            return;
        }

        if (!await _builderModeEntry.EnterAsync(BuilderTarget.Autounattend))
            _startedHere = false;
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

            var addSuccess = await _wimCustomizationService.AddXmlToImageAsync(destinationPath, WorkingDirectory);
            if (addSuccess)
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
                    _localizationService.GetString("WIMUtil_Msg_XmlAddFailed"),
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
            var addSuccess = await _wimCustomizationService.AddXmlToImageAsync(selectedPath, WorkingDirectory);
            if (addSuccess)
            {
                SelectedXmlPath = selectedPath;
                IsXmlAdded = true;
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlSelectSuccess");
                ClearOtherXmlCardCompletions("select");
                SelectXmlCard.IsComplete = true;
                await CheckAnswerFileAsync();
            }
            else
            {
                SelectXmlCard.HasFailed = true;
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlValidAddFailed");
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_XmlValidAddFailed"),
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

    // The copy on the media is what Setup reads, so that is the one checked. The verdict never
    // undoes the card: the user sees the findings and decides.
    private async Task CheckAnswerFileAsync()
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
            if (report.Findings.Count > 0)
            {
                await _dialogService.ShowTaskOutputDialogAsync(
                    _localizationService.GetString("WIMUtil_AnswerFile_DialogTitle"),
                    AnswerFileReportDialog.Lines(report, _localizationService));
            }
        }
        catch (Exception ex)
        {
            XmlStatus = previousStatus;
            _logService.LogWarning($"Could not check autounattend.xml: {ex.Message}");
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
