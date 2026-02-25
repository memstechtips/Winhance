using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.AdvancedTools.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.AdvancedTools.ViewModels;

/// <summary>
/// Sub-ViewModel for WIM Utility Step 1: ISO selection, working directory, and extraction.
/// </summary>
public partial class WimStep1ViewModel : ObservableObject
{
    private readonly IWimUtilService _wimUtilService;
    private readonly ITaskProgressService _taskProgressService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IFilePickerService _filePickerService;
    private readonly ILogService _logService;

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

    public WizardActionCard SelectIsoCard { get; private set; } = new();
    public WizardActionCard SelectDirectoryCard { get; private set; } = new();

    public WimStep1ViewModel(
        IWimUtilService wimUtilService,
        ITaskProgressService taskProgressService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IFileSystemService fileSystemService,
        IFilePickerService filePickerService,
        ILogService logService)
    {
        _wimUtilService = wimUtilService;
        _taskProgressService = taskProgressService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _fileSystemService = fileSystemService;
        _filePickerService = filePickerService;
        _logService = logService;

        SelectedIsoPath = string.Empty;
        WorkingDirectory = _fileSystemService.CombinePath(_fileSystemService.GetTempPath(), "WinhanceWIM");

        CreateActionCards();
    }

    private void CreateActionCards()
    {
        SelectIsoCard = new WizardActionCard
        {
            Icon = "\uE958",
            Title = _localizationService.GetString("WIMUtil_Card_SelectISO_Title"),
            Description = _localizationService.GetString("WIMUtil_Label_NoSelection"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_SelectISO_Button"),
            ButtonCommand = SelectIsoFileCommand,
            IsEnabled = true
        };

        SelectDirectoryCard = new WizardActionCard
        {
            IconPath = GetResourceIconPath("ExplorerIconPath"),
            Title = _localizationService.GetString("WIMUtil_Card_SelectDirectory_Title"),
            Description = string.Format(
                _localizationService.GetString("WIMUtil_Card_SelectDirectory_Description_Default"),
                _fileSystemService.CombinePath(_fileSystemService.GetTempPath(), "WinhanceWIM")),
            ButtonText = _localizationService.GetString("WIMUtil_Card_SelectDirectory_Button"),
            ButtonCommand = SelectWorkingDirectoryCommand,
            IsEnabled = true
        };
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
    private async Task SelectWorkingDirectory()
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
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetString("WIMUtil_Msg_ValidationComplete"),
                    _localizationService.GetString("WIMUtil_SelectWorkingDirectory"));
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
            WorkingDirectory = _fileSystemService.CombinePath(selectedPath, "WinhanceWIM");
            try
            {
                _fileSystemService.CreateDirectory(WorkingDirectory);
                SelectDirectoryCard.Description = $"{_localizationService.GetString("WIMUtil_Label_Using")}: {WorkingDirectory}";
                CanStartExtraction = !string.IsNullOrEmpty(SelectedIsoPath) && !string.IsNullOrEmpty(WorkingDirectory);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to create working directory: {ex.Message}", ex);
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
    private async Task StartIsoExtraction()
    {
        try
        {
            SelectIsoCard.IsEnabled = false;
            SelectIsoCard.Opacity = 0.5;
            SelectDirectoryCard.IsEnabled = false;
            SelectDirectoryCard.Opacity = 0.5;
            IsExtracting = true;

            _taskProgressService.StartTask(_localizationService.GetString("WIMUtil_Status_Extracting"), true);
            var progress = _taskProgressService.CreatePowerShellProgress();

            var success = await _wimUtilService.ExtractIsoAsync(SelectedIsoPath, WorkingDirectory, progress, _taskProgressService.CurrentTaskCancellationSource!.Token);

            ResetExtractionState();

            if (success)
            {
                SelectIsoCard.IsComplete = true;
                SelectIsoCard.Description = _localizationService.GetString("WIMUtil_Status_IsoExtractionSuccess");
                SelectIsoCard.DescriptionForeground = new SolidColorBrush(Color.FromArgb(255, 27, 94, 32));
                IsExtractionComplete = true;
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetString("WIMUtil_Msg_ExtractionComplete"),
                    _localizationService.GetString("WIMUtil_Status_IsoExtractionSuccess"));
            }
            else
            {
                SelectIsoCard.HasFailed = true;
                SelectIsoCard.Description = _localizationService.GetString("WIMUtil_Status_IsoExtractionFailed");
                SelectIsoCard.DescriptionForeground = new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_ExtractionFailed"),
                    _localizationService.GetString("WIMUtil_Status_IsoExtractionFailed"));
            }
        }
        catch (OperationCanceledException)
        {
            ResetExtractionState();
            SelectIsoCard.Description = _localizationService.GetString("WIMUtil_Status_IsoExtractionCancelled");
        }
        catch (InsufficientDiskSpaceException spaceEx)
        {
            ResetExtractionState();
            SelectIsoCard.HasFailed = true;
            SelectIsoCard.Description = string.Format(_localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace"), spaceEx.DriveName);
            await _dialogService.ShowWarningAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_InsufficientSpace"), spaceEx.DriveName, spaceEx.RequiredGB.ToString("F2"), spaceEx.AvailableGB.ToString("F2"), (spaceEx.RequiredGB - spaceEx.AvailableGB).ToString("F2")),
                string.Format(_localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace"), spaceEx.DriveName));
        }
        catch (Exception ex)
        {
            ResetExtractionState();
            SelectIsoCard.HasFailed = true;
            SelectIsoCard.Description = string.Format(_localizationService.GetString("WIMUtil_Status_ErrorPrefix"), ex.Message);
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_ExtractionError"), ex.Message),
                "Error");
        }
        finally
        {
            _taskProgressService.CompleteTask();
        }
    }

    private void ResetExtractionState()
    {
        SelectIsoCard.IsEnabled = true;
        SelectIsoCard.Opacity = 1.0;
        SelectDirectoryCard.IsEnabled = true;
        SelectDirectoryCard.Opacity = 1.0;
        IsExtracting = false;
    }

    [RelayCommand]
    private async Task OpenWindows10Download()
    {
        try { await Windows.System.Launcher.LaunchUriAsync(new Uri("https://www.microsoft.com/software-download/windows10")); }
        catch (Exception ex) { _logService.LogDebug($"Failed to launch Windows 10 download URL: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task OpenWindows11Download()
    {
        try { await Windows.System.Launcher.LaunchUriAsync(new Uri("https://www.microsoft.com/software-download/windows11")); }
        catch (Exception ex) { _logService.LogDebug($"Failed to launch Windows 11 download URL: {ex.Message}"); }
    }

    partial void OnHasExtractedIsoAlreadyChanged(bool value)
    {
        SelectDirectoryCard.Description = value
            ? _localizationService.GetString("WIMUtil_Label_SelectExtracted")
            : string.Format(_localizationService.GetString("WIMUtil_Card_SelectDirectory_Description_Default"), _fileSystemService.CombinePath(_fileSystemService.GetTempPath(), "WinhanceWIM"));
    }

    private static string GetResourceIconPath(string resourceKey)
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(resourceKey, out var value) && value is string path)
            return path;
        return string.Empty;
    }
}
