using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.WimUtil.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.WimUtil.ViewModels;

public partial class WimStep1ViewModel : ObservableObject
{
    private readonly IIsoService _isoService;
    private readonly ITaskProgressService _taskProgressService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IFilePickerService _filePickerService;
    private readonly ILogService _logService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly string _automaticWorkingDirectory;

    internal const string WorkingFolderName = "WinhanceWIM";

    // Extracted media is about 7 GB, turning install.esd into install.wim adds about 5 GB and the ISO
    // is about 7 GB more. Servicing needs more again, which its own note on the page covers.
    internal const int LowSpaceGb = 20;
    private const double BytesPerGb = 1024d * 1024 * 1024;

    [ObservableProperty]
    public partial string SelectedIsoPath { get; set; }

    [ObservableProperty]
    public partial string WorkingDirectory { get; set; }

    [ObservableProperty]
    public partial bool HasExtractedIsoAlready { get; set; }

    [ObservableProperty]
    public partial bool CanStartExtraction { get; set; }

    [ObservableProperty]
    public partial bool IsExtracting { get; set; }

    [ObservableProperty]
    public partial bool IsExtractionComplete { get; set; }

    [ObservableProperty]
    public partial bool ShowSpaceWarning { get; set; }

    [ObservableProperty]
    public partial string SpaceWarningText { get; set; }

    // Raised each time a folder becomes the media, which IsExtractionComplete cannot signal once it is already true.
    public event EventHandler? MediaReady;

    public WizardActionCard SelectIsoCard { get; private set; } = new();
    public WizardActionCard SelectDirectoryCard { get; private set; } = new();

    public WimStep1ViewModel(
        IIsoService isoService,
        ITaskProgressService taskProgressService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IFileSystemService fileSystemService,
        IFilePickerService filePickerService,
        ILogService logService,
        IUserPreferencesService userPreferencesService)
    {
        _isoService = isoService;
        _taskProgressService = taskProgressService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _fileSystemService = fileSystemService;
        _filePickerService = filePickerService;
        _logService = logService;
        _userPreferencesService = userPreferencesService;

        SelectedIsoPath = string.Empty;
        SpaceWarningText = string.Empty;
        _automaticWorkingDirectory = AutomaticWorkingDirectory();
        WorkingDirectory = SavedWorkingDirectory() ?? _automaticWorkingDirectory;

        CreateActionCards();
    }

    private string? SavedWorkingDirectory()
    {
        var saved = _userPreferencesService.GetPreference(UserPreferenceKeys.WimUtilWorkingDirectory, string.Empty);
        return !string.IsNullOrEmpty(saved) && _fileSystemService.DirectoryExists(_fileSystemService.GetPathRoot(saved))
            ? saved
            : null;
    }

    // The biggest NTFS drive keeps a full copy of the media off a crowded system drive, and image
    // servicing mounts next to this folder, which needs NTFS.
    private string AutomaticWorkingDirectory()
    {
        var drives = _fileSystemService.GetFixedNtfsDrives();
        var root = drives.Length > 0 ? drives.MaxBy(d => d.FreeBytes).Root : _fileSystemService.GetTempPath();
        return _fileSystemService.CombinePath(root, WorkingFolderName);
    }

    // Low space only warns: the figure is an estimate and a build that fits must still be allowed.
    private void DescribeWorkingDirectory()
    {
        var description = WorkingDirectory == _automaticWorkingDirectory
            ? string.Format(_localizationService.GetString("WIMUtil_Card_SelectDirectory_Description_Default"), WorkingDirectory)
            : $"{_localizationService.GetString("WIMUtil_Label_Using")}: {WorkingDirectory}";

        if (_fileSystemService.GetAvailableFreeSpace(WorkingDirectory) is not { } freeBytes)
        {
            SelectDirectoryCard.Description = description;
            ShowSpaceWarning = false;
            return;
        }

        var freeGb = (freeBytes / BytesPerGb).ToString("F1");
        SelectDirectoryCard.Description = $"{description} ({string.Format(_localizationService.GetString("WIMUtil_Label_FreeSpace"), freeGb)})";
        SpaceWarningText = string.Format(
            _localizationService.GetString("WIMUtil_Warning_LowWorkingSpace"), freeGb, _fileSystemService.GetPathRoot(WorkingDirectory), LowSpaceGb);
        ShowSpaceWarning = freeBytes < LowSpaceGb * BytesPerGb;
    }

    // The media folder was deleted after the build, so the wizard starts over from extraction.
    public void ResetAfterWorkingFilesDeleted()
    {
        IsExtractionComplete = false;
        SelectIsoCard.IsComplete = false;
        SelectIsoCard.Description = string.IsNullOrEmpty(SelectedIsoPath)
            ? _localizationService.GetString("WIMUtil_Label_NoSelection")
            : SelectedIsoPath;
        if (!HasExtractedIsoAlready)
            DescribeWorkingDirectory();
        CanStartExtraction = !string.IsNullOrEmpty(SelectedIsoPath) && !string.IsNullOrEmpty(WorkingDirectory);
    }

    private void CreateActionCards()
    {
        SelectIsoCard = new WizardActionCard { ButtonCommand = SelectIsoFileCommand };
        SelectDirectoryCard = new WizardActionCard { ButtonCommand = SelectWorkingDirectoryCommand };
        RelabelActionCards();
    }

    // The page holds these card instances, so a language switch relabels them in place. A description that reports
    // how a pick or an extraction went keeps its text until the next one.
    public void RelabelActionCards()
    {
        SelectIsoCard.Title = _localizationService.GetString("WIMUtil_Card_SelectISO_Title");
        SelectIsoCard.ButtonText = _localizationService.GetString("WIMUtil_Card_SelectISO_Button");
        SelectIsoCard.BusyReason = _localizationService.GetString("WIMUtil_Disabled_Busy");
        SelectDirectoryCard.Title = _localizationService.GetString("WIMUtil_Card_SelectDirectory_Title");
        SelectDirectoryCard.ButtonText = _localizationService.GetString("WIMUtil_Card_SelectDirectory_Button");
        SelectDirectoryCard.BusyReason = _localizationService.GetString("WIMUtil_Disabled_Busy");

        if (string.IsNullOrEmpty(SelectedIsoPath))
            SelectIsoCard.Description = _localizationService.GetString("WIMUtil_Label_NoSelection");

        // An empty folder means the last pick failed and the card shows why.
        if (string.IsNullOrEmpty(WorkingDirectory))
            return;
        if (!HasExtractedIsoAlready)
            DescribeWorkingDirectory();
        else
            SelectDirectoryCard.Description = IsExtractionComplete
                ? $"{_localizationService.GetString("WIMUtil_Label_Using")}: {WorkingDirectory}"
                : _localizationService.GetString("WIMUtil_Label_SelectExtracted");
    }

    [RelayCommand]
    private void SelectIsoFile()
    {
        var path = _filePickerService.PickFile(
            ["ISO Files", "*.iso"],
            _localizationService.GetString("WIMUtil_FileDialog_SelectIso"));
        if (!string.IsNullOrEmpty(path))
        {
            SelectedIsoPath = path;
            SelectIsoCard.Description = SelectedIsoPath;
            CanStartExtraction = !string.IsNullOrEmpty(SelectedIsoPath) && !string.IsNullOrEmpty(WorkingDirectory);
        }
    }

    [RelayCommand]
    private async Task SelectWorkingDirectoryAsync()
    {
        var description = HasExtractedIsoAlready
            ? _localizationService.GetString("WIMUtil_FolderDialog_SelectExtracted")
            : _localizationService.GetString("WIMUtil_FolderDialog_SelectWorkDir");
        var selectedPath = _filePickerService.PickFolder(description);

        if (string.IsNullOrEmpty(selectedPath)) return;

        if (HasExtractedIsoAlready)
        {
            var isValid = await ValidateExtractedIsoDirectory(selectedPath);
            if (isValid)
            {
                WorkingDirectory = selectedPath;
                SelectDirectoryCard.Description = $"{_localizationService.GetString("WIMUtil_Label_Using")}: {WorkingDirectory}";
                IsExtractionComplete = true;
                MediaReady?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                WorkingDirectory = string.Empty;
                SelectDirectoryCard.Description = _localizationService.GetString("WIMUtil_Error_InvalidDirectory");
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_InvalidDirectory"),
                    _localizationService.GetString("WIMUtil_Error_InvalidDirectory"));
            }
        }
        else
        {
            WorkingDirectory = _fileSystemService.CombinePath(selectedPath, WorkingFolderName);
            var created = await _dialogService.RunOrShowErrorAsync(_logService, () =>
                {
                    _fileSystemService.CreateDirectory(WorkingDirectory);
                    _userPreferencesService.SetPreferenceAsync(UserPreferenceKeys.WimUtilWorkingDirectory, WorkingDirectory).FireAndForget(_logService);
                    DescribeWorkingDirectory();
                    CanStartExtraction = !string.IsNullOrEmpty(SelectedIsoPath) && !string.IsNullOrEmpty(WorkingDirectory);
                },
                _ => _localizationService.GetString("WIMUtil_Error_DirectoryCreateFailed"));
            if (!created)
            {
                SelectDirectoryCard.Description = _localizationService.GetString("WIMUtil_Error_DirectoryCreateFailed");
                WorkingDirectory = string.Empty;
            }
        }
    }

    internal async Task<bool> ValidateExtractedIsoDirectory(string path)
    {
        try
        {
            var pathRoot = _fileSystemService.GetPathRoot(path);
            if (!string.IsNullOrEmpty(pathRoot) && path.TrimEnd('\\', '/').Equals(pathRoot.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                return false;

            var driveInfo = new DriveInfo(path);
            if (driveInfo.DriveType == DriveType.CDRom) return false;

            var testFile = _fileSystemService.CombinePath(path, $".winhance_write_test_{Guid.NewGuid()}.tmp");
            try
            {
                await _fileSystemService.WriteAllTextAsync(testFile, "test");
                _fileSystemService.DeleteFile(testFile);
            }
            catch (Exception ex) { _logService.LogDebug($"Write test failed for directory '{path}': {ex.Message}"); return false; }

            var extractedDirs = _fileSystemService.GetDirectories(path);
            var hasSourcesDir = extractedDirs.Any(d => _fileSystemService.GetFileName(d)?.Equals("sources", StringComparison.OrdinalIgnoreCase) == true);
            var hasBootDir = extractedDirs.Any(d => _fileSystemService.GetFileName(d)?.Equals("boot", StringComparison.OrdinalIgnoreCase) == true);

            return hasSourcesDir && hasBootDir;
        }
        catch (Exception ex) { _logService.LogDebug($"ISO directory validation failed for '{path}': {ex.Message}"); return false; }
    }

    [RelayCommand]
    private async Task StartIsoExtractionAsync()
    {
        try
        {
            await _dialogService.RunOrShowErrorAsync(_logService, ExtractIsoAsync, ExtractionFailed);
        }
        finally
        {
            _taskProgressService.CompleteTask();
        }
    }

    private async Task ExtractIsoAsync()
    {
        try
        {
            SelectIsoCard.IsEnabled = false;
            SelectDirectoryCard.IsEnabled = false;
            IsExtracting = true;

            _taskProgressService.StartTask(_localizationService.GetString("WIMUtil_Status_Extracting"), true);
            var progress = _taskProgressService.CreateDetailedProgress();

            var success = await _isoService.ExtractIsoAsync(SelectedIsoPath, WorkingDirectory, progress, _taskProgressService.CurrentTaskCancellationSource!.Token);
            _taskProgressService.CompleteTask();

            ResetExtractionState();

            if (success)
            {
                SelectIsoCard.IsComplete = true;
                SelectIsoCard.Description = _localizationService.GetString("WIMUtil_Status_IsoExtractionSuccess");
                IsExtractionComplete = true;
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetString("WIMUtil_Msg_ExtractionComplete"),
                    _localizationService.GetString("WIMUtil_Status_IsoExtractionSuccess"));
                MediaReady?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _taskProgressService.FailTask();
                SelectIsoCard.HasFailed = true;
                SelectIsoCard.Description = _localizationService.GetString("WIMUtil_Status_IsoExtractionFailed");
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_ExtractionFailed"),
                    _localizationService.GetString("WIMUtil_Status_IsoExtractionFailed"));
            }
        }
        catch (OperationCanceledException)
        {
            _taskProgressService.CancelTask();
            ResetExtractionState();
            SelectIsoCard.Description = _localizationService.GetString("WIMUtil_Status_IsoExtractionCancelled");
        }
        catch (InsufficientDiskSpaceException spaceEx)
        {
            _taskProgressService.FailTask();
            ResetExtractionState();
            SelectIsoCard.HasFailed = true;
            SelectIsoCard.Description = string.Format(_localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace"), spaceEx.DriveName);
            await _dialogService.ShowWarningAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_InsufficientSpace"), spaceEx.DriveName, spaceEx.RequiredGB.ToString("F2"), spaceEx.AvailableGB.ToString("F2"), (spaceEx.RequiredGB - spaceEx.AvailableGB).ToString("F2")),
                string.Format(_localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace"), spaceEx.DriveName));
        }
    }

    // Runs before the error dialog opens, so the card and the progress bar already show the failure behind it.
    private string ExtractionFailed(Exception ex)
    {
        _taskProgressService.FailTask();
        ResetExtractionState();
        SelectIsoCard.HasFailed = true;
        SelectIsoCard.Description = string.Format(_localizationService.GetString("WIMUtil_Status_ErrorPrefix"), ex.Message);
        return string.Format(_localizationService.GetString("WIMUtil_Msg_ExtractionError"), ex.Message);
    }

    private void ResetExtractionState()
    {
        SelectIsoCard.IsEnabled = true;
        SelectDirectoryCard.IsEnabled = true;
        IsExtracting = false;
    }

    [RelayCommand]
    private Task OpenWindows10DownloadAsync() => OpenLinkAsync("https://www.microsoft.com/software-download/windows10");

    [RelayCommand]
    private Task OpenWindows11DownloadAsync() => OpenLinkAsync("https://www.microsoft.com/software-download/windows11");

    private Task<bool> OpenLinkAsync(string url) =>
        _dialogService.RunOrShowErrorAsync(_logService,
            async () => await Windows.System.Launcher.LaunchUriAsync(new Uri(url)),
            ex => _localizationService.GetString("WIMUtil_Msg_OpenLinkFailed", ex.Message));

    partial void OnHasExtractedIsoAlreadyChanged(bool value)
    {
        if (value)
        {
            SelectDirectoryCard.Description = _localizationService.GetString("WIMUtil_Label_SelectExtracted");
            ShowSpaceWarning = false;
            return;
        }

        // A folder picked while the box was ticked holds the user's own media, and extracting into it would
        // overwrite it. Once that media is in use the later steps point at it, so it stays.
        if (!IsExtractionComplete)
            WorkingDirectory = SavedWorkingDirectory() ?? _automaticWorkingDirectory;
        DescribeWorkingDirectory();
    }
}
