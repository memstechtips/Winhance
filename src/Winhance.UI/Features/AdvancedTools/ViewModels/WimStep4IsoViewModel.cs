using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.AdvancedTools.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.AdvancedTools.ViewModels;

/// <summary>
/// Sub-ViewModel for WIM Utility Step 4: oscdimg download, output path selection, and ISO creation.
/// </summary>
public partial class WimStep4IsoViewModel : ObservableObject
{
    private readonly IWimUtilService _wimUtilService;
    private readonly ITaskProgressService _taskProgressService;
    private readonly IProcessExecutor _processExecutor;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IFilePickerService _filePickerService;
    private readonly ILogService _logService;
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// The working directory, set by the parent when Step 1 completes.
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsOscdimgAvailable { get; set; }

    [ObservableProperty]
    public partial string OutputIsoPath { get; set; }

    [ObservableProperty]
    public partial bool IsIsoCreated { get; set; }

    public WizardActionCard DownloadOscdimgCard { get; private set; } = new();
    public WizardActionCard SelectOutputCard { get; private set; } = new();

    public WimStep4IsoViewModel(
        IWimUtilService wimUtilService,
        ITaskProgressService taskProgressService,
        IProcessExecutor processExecutor,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IFileSystemService fileSystemService,
        IFilePickerService filePickerService,
        ILogService logService)
    {
        _wimUtilService = wimUtilService;
        _taskProgressService = taskProgressService;
        _processExecutor = processExecutor;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _fileSystemService = fileSystemService;
        _filePickerService = filePickerService;
        _logService = logService;

        OutputIsoPath = string.Empty;

        CreateActionCards();
    }

    private void CreateActionCards()
    {
        DownloadOscdimgCard = new WizardActionCard
        {
            IconPath = GetResourceIconPath("ToolBoxIconPath"),
            Title = _localizationService.GetString("WIMUtil_Card_DownloadOscdimg_Title"),
            Description = _localizationService.GetString("WIMUtil_Card_DownloadOscdimg_Description"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_DownloadOscdimg_Button"),
            ButtonCommand = DownloadOscdimgCommand,
            IsEnabled = true
        };

        SelectOutputCard = new WizardActionCard
        {
            Icon = "\uE74E",
            Title = _localizationService.GetString("WIMUtil_Card_SelectOutput_Title"),
            Description = _localizationService.GetString("WIMUtil_Label_NoLocation"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_SelectOutput_Button"),
            ButtonCommand = SelectIsoOutputLocationCommand,
            IsEnabled = true
        };
    }

    [RelayCommand]
    private async Task DownloadOscdimg()
    {
        try
        {
            DownloadOscdimgCard.IsComplete = false;
            DownloadOscdimgCard.HasFailed = false;
            DownloadOscdimgCard.IsProcessing = true;
            DownloadOscdimgCard.IsEnabled = false;

            _cancellationTokenSource = new CancellationTokenSource();
            var progress = new Progress<TaskProgressDetail>(detail => { });

            var success = await _wimUtilService.EnsureOscdimgAvailableAsync(progress, _cancellationTokenSource.Token);

            DownloadOscdimgCard.IsProcessing = false;

            if (success)
            {
                IsOscdimgAvailable = true;
                DownloadOscdimgCard.IsComplete = true;
                DownloadOscdimgCard.IsEnabled = false;
                DownloadOscdimgCard.ButtonText = _localizationService.GetString("WIMUtil_Button_OscdimgFound");
                DownloadOscdimgCard.Description = _localizationService.GetString("WIMUtil_Desc_OscdimgInstalled");
                DownloadOscdimgCard.IconPath = GetResourceIconPath("CheckCircleIconPath");
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetString("WIMUtil_Msg_AdkInstallComplete"),
                    "Success");
            }
            else
            {
                DownloadOscdimgCard.IsEnabled = true;
                DownloadOscdimgCard.HasFailed = true;
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_AdkInstallFailed"),
                    "Error");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error installing ADK: {ex.Message}", ex);
            DownloadOscdimgCard.IsProcessing = false;
            DownloadOscdimgCard.IsEnabled = true;
            DownloadOscdimgCard.HasFailed = true;
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_AdkInstallError"), ex.Message),
                "Error");
        }
    }

    [RelayCommand]
    private void SelectIsoOutputLocation()
    {
        var path = _filePickerService.PickSaveFile(
            ["ISO Files", "*.iso"],
            "Winhance_Windows.iso",
            "iso");
        if (!string.IsNullOrEmpty(path))
        {
            OutputIsoPath = path;
            SelectOutputCard.Description = $"{_localizationService.GetString("WIMUtil_Label_Output")}: {_fileSystemService.GetFileName(OutputIsoPath)}";
        }
    }

    [RelayCommand]
    private async Task CreateIso()
    {
        try
        {
            if (!IsOscdimgAvailable)
            {
                await _dialogService.ShowWarningAsync(
                    _localizationService.GetString("WIMUtil_Msg_OscdimgRequired"),
                    "Required");
                return;
            }

            if (string.IsNullOrEmpty(OutputIsoPath))
            {
                await _dialogService.ShowWarningAsync(
                    _localizationService.GetString("WIMUtil_Msg_OutputRequired"),
                    "Required");
                return;
            }

            SelectOutputCard.IsEnabled = false;
            SelectOutputCard.Opacity = 0.5;

            _taskProgressService.StartTask(_localizationService.GetString("WIMUtil_Status_CreatingIso"), true);
            var progress = _taskProgressService.CreatePowerShellProgress();

            var success = await _wimUtilService.CreateIsoAsync(WorkingDirectory, OutputIsoPath, progress, _taskProgressService.CurrentTaskCancellationSource!.Token);

            SelectOutputCard.IsEnabled = true;
            SelectOutputCard.Opacity = 1.0;

            if (success)
            {
                IsIsoCreated = true;
                SelectOutputCard.Description = _localizationService.GetString("WIMUtil_Desc_IsoCreatedSuccess");
                SelectOutputCard.DescriptionForeground = new SolidColorBrush(Color.FromArgb(255, 27, 94, 32));

                var openFolder = await _dialogService.ShowConfirmationAsync(
                    string.Format(_localizationService.GetString("WIMUtil_Msg_IsoCreatedSuccess"), OutputIsoPath),
                    _localizationService.GetString("WIMUtil_Desc_IsoCreatedSuccess"),
                    _localizationService.GetString("WIMUtil_Button_OpenFolder"),
                    _localizationService.GetString("Button_Close"));
                if (openFolder)
                {
                    _processExecutor.ShellExecuteAsync("explorer.exe", $"/select,\"{OutputIsoPath}\"").FireAndForget(_logService);
                }
            }
            else
            {
                SelectOutputCard.Description = _localizationService.GetString("WIMUtil_Desc_IsoCreateFailed");
                SelectOutputCard.DescriptionForeground = new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_IsoCreationFailed"),
                    "Error");
            }
        }
        catch (OperationCanceledException)
        {
            SelectOutputCard.IsEnabled = true;
            SelectOutputCard.Opacity = 1.0;
            try { if (_fileSystemService.FileExists(OutputIsoPath)) _fileSystemService.DeleteFile(OutputIsoPath); } catch (Exception ex) { _logService.LogDebug($"Best-effort incomplete ISO cleanup failed: {ex.Message}"); }
            SelectOutputCard.Description = _localizationService.GetString("WIMUtil_Desc_IsoCreateCancelled");
        }
        catch (InsufficientDiskSpaceException spaceEx)
        {
            SelectOutputCard.IsEnabled = true;
            SelectOutputCard.Opacity = 1.0;
            SelectOutputCard.Description = string.Format(_localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace"), spaceEx.DriveName);
            await _dialogService.ShowWarningAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_InsufficientSpace_Create"), spaceEx.DriveName, spaceEx.RequiredGB.ToString("F2"), spaceEx.AvailableGB.ToString("F2"), (spaceEx.RequiredGB - spaceEx.AvailableGB).ToString("F2")),
                string.Format(_localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace"), spaceEx.DriveName));
        }
        catch (Exception ex)
        {
            SelectOutputCard.IsEnabled = true;
            SelectOutputCard.Opacity = 1.0;
            SelectOutputCard.Description = string.Format(_localizationService.GetString("WIMUtil_Status_ErrorPrefix"), ex.Message);
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_IsoCreationError"), ex.Message),
                "Error");
        }
        finally
        {
            _taskProgressService.CompleteTask();
        }
    }

    public void UpdateDownloadOscdimgCardState()
    {
        if (IsOscdimgAvailable)
        {
            DownloadOscdimgCard.IsEnabled = false;
            DownloadOscdimgCard.IsComplete = true;
            DownloadOscdimgCard.ButtonText = _localizationService.GetString("WIMUtil_Button_OscdimgFound");
            DownloadOscdimgCard.Description = _localizationService.GetString("WIMUtil_Desc_OscdimgFound");
            DownloadOscdimgCard.IconPath = GetResourceIconPath("CheckCircleIconPath");
        }
        else
        {
            DownloadOscdimgCard.IsEnabled = true;
            DownloadOscdimgCard.IsComplete = false;
            DownloadOscdimgCard.ButtonText = _localizationService.GetString("WIMUtil_Button_Download");
            DownloadOscdimgCard.Description = _localizationService.GetString("WIMUtil_Card_DownloadOscdimg_Description");
            DownloadOscdimgCard.IconPath = GetResourceIconPath("ToolBoxIconPath");
        }
    }

    private static string GetResourceIconPath(string resourceKey)
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(resourceKey, out var value) && value is string path)
            return path;
        return string.Empty;
    }
}
