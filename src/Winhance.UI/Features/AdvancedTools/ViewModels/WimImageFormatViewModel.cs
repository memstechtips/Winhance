using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.AdvancedTools.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.AdvancedTools.ViewModels;

/// <summary>
/// Sub-ViewModel for WIM image format detection, WIM/ESD conversion, and deletion.
/// Receives WorkingDirectory from the parent.
/// </summary>
public partial class WimImageFormatViewModel : ObservableObject
{
    private readonly IWimImageService _wimImageService;
    private readonly ITaskProgressService _taskProgressService;
    private readonly IDialogService _dialogService;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogService _logService;
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// The working directory, set by the parent when Step 1 completes.
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ImageFormatInfo? CurrentImageFormat { get; set; }

    [ObservableProperty]
    public partial bool ShowConversionCard { get; set; }

    [ObservableProperty]
    public partial bool IsConverting { get; set; }

    [ObservableProperty]
    public partial string ConversionStatus { get; set; }

    [ObservableProperty]
    public partial bool BothFormatsExist { get; set; }

    [ObservableProperty]
    public partial string WimFileSize { get; set; }

    [ObservableProperty]
    public partial string EsdFileSize { get; set; }

    [ObservableProperty]
    public partial ImageDetectionResult? DetectionResult { get; set; }

    public WizardActionCard ConvertImageCard { get; private set; } = new();

    public WimImageFormatViewModel(
        IWimImageService wimImageService,
        ITaskProgressService taskProgressService,
        IDialogService dialogService,
        IDispatcherService dispatcherService,
        ILocalizationService localizationService,
        ILogService logService)
    {
        _wimImageService = wimImageService;
        _taskProgressService = taskProgressService;
        _dialogService = dialogService;
        _dispatcherService = dispatcherService;
        _localizationService = localizationService;
        _logService = logService;

        ConversionStatus = string.Empty;
        WimFileSize = string.Empty;
        EsdFileSize = string.Empty;

        CreateActionCards();
    }

    private void CreateActionCards()
    {
        ConvertImageCard = new WizardActionCard
        {
            Icon = "\uE8AB",
            Title = _localizationService.GetString("WIMUtil_Card_ConvertImage_Title"),
            Description = _localizationService.GetString("WIMUtil_Label_Detecting"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_ConvertImage_Button"),
            ButtonCommand = ConvertImageFormatCommand,
            IsEnabled = false
        };
    }

    [RelayCommand]
    private async Task ConvertImageFormat()
    {
        if (CurrentImageFormat == null) return;

        try
        {
            var targetFormat = CurrentImageFormat.Format == ImageFormat.Wim ? ImageFormat.Esd : ImageFormat.Wim;
            var targetFormatName = targetFormat == ImageFormat.Wim ? "WIM" : "ESD";
            var currentFormatName = CurrentImageFormat.Format == ImageFormat.Wim ? "WIM" : "ESD";

            var confirmKey = targetFormat == ImageFormat.Esd ? "WIMUtil_Msg_ConvertConfirm_Esd" : "WIMUtil_Msg_ConvertConfirm_Wim";
            var currentSize = CurrentImageFormat.FileSizeBytes / (1024.0 * 1024 * 1024);
            var estimatedTargetSize = CurrentImageFormat.Format == ImageFormat.Wim ? currentSize * 0.65 : currentSize * 1.50;
            var diff = Math.Abs(estimatedTargetSize - currentSize);
            var confirmed = await _dialogService.ShowConfirmationAsync(
                string.Format(_localizationService.GetString(confirmKey), diff.ToString("F2")),
                string.Format(_localizationService.GetString("WIMUtil_Card_ConvertImage_Button_Dynamic"), targetFormatName),
                "Yes", "No");
            if (!confirmed) return;

            IsConverting = true;
            ConvertImageCard.IsProcessing = true;
            ConvertImageCard.IsEnabled = false;
            ConversionStatus = string.Format(_localizationService.GetString("WIMUtil_Status_ConvertingToFormat"), currentFormatName, targetFormatName);

            _taskProgressService.StartTask(string.Format(_localizationService.GetString("WIMUtil_Status_ConvertingToFormat"), currentFormatName, targetFormatName), true);
            var progress = _taskProgressService.CreatePowerShellProgress();

            var success = await _wimImageService.ConvertImageAsync(WorkingDirectory, targetFormat, progress, _taskProgressService.CurrentTaskCancellationSource!.Token);

            if (success)
            {
                ConvertImageCard.IsComplete = true;
                ConversionStatus = string.Format(_localizationService.GetString("WIMUtil_Status_ConversionSuccess"), targetFormatName);
                await DetectImageFormatAsync();
                await _dialogService.ShowInformationAsync(
                    string.Format(_localizationService.GetString("WIMUtil_Msg_ConversionSuccess"), targetFormatName),
                    "Success");
            }
            else
            {
                ConvertImageCard.HasFailed = true;
                ConversionStatus = _localizationService.GetString("WIMUtil_Status_ConversionFailed");
                await _dialogService.ShowErrorAsync(
                    string.Format(_localizationService.GetString("WIMUtil_Msg_ConversionFailed"), targetFormatName),
                    "Error");
            }
        }
        catch (OperationCanceledException)
        {
            ConversionStatus = _localizationService.GetString("WIMUtil_Status_ConversionCancelled");
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error during conversion: {ex.Message}", ex);
            ConvertImageCard.HasFailed = true;
            ConversionStatus = string.Format(_localizationService.GetString("WIMUtil_Status_ErrorPrefix"), ex.Message);
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_ConversionError"), ex.Message),
                "Error");
        }
        finally
        {
            IsConverting = false;
            ConvertImageCard.IsProcessing = false;
            ConvertImageCard.IsEnabled = true;
            _taskProgressService.CompleteTask();
        }
    }

    public async Task DetectImageFormatAsync()
    {
        try
        {
            var detection = await _wimImageService.DetectAllImageFormatsAsync(WorkingDirectory);
            _dispatcherService.RunOnUIThread(() =>
            {
                DetectionResult = detection;
                BothFormatsExist = detection.BothExist;
                CurrentImageFormat = detection.PrimaryFormat;
                ShowConversionCard = detection.HasAnyFormat;
                if (detection.BothExist)
                {
                    WimFileSize = FormatFileSize(detection.WimInfo!.FileSizeBytes);
                    EsdFileSize = FormatFileSize(detection.EsdInfo!.FileSizeBytes);
                }
                UpdateConversionCardState();
            });
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error detecting image formats: {ex.Message}", ex);
            ShowConversionCard = false;
        }
    }

    public async Task SafeDetectImageFormatAsync()
    {
        try
        {
            await DetectImageFormatAsync();
        }
        catch (Exception ex)
        {
            _logService.LogError($"Unhandled error in image format detection: {ex.Message}", ex);
        }
    }

    [RelayCommand]
    private async Task DeleteWim()
    {
        try
        {
            var confirmed = await _dialogService.ShowConfirmationAsync(
                _localizationService.GetString("WIMUtil_Msg_DeleteWimConfirm"),
                _localizationService.GetString("WIMUtil_Button_DeleteWim"),
                _localizationService.GetString("Button_Delete"),
                _localizationService.GetString("Button_Cancel"));
            if (!confirmed) return;

            _cancellationTokenSource = new CancellationTokenSource();
            var progress = new Progress<TaskProgressDetail>(detail => { });

            var success = await _wimImageService.DeleteImageFileAsync(
                WorkingDirectory,
                ImageFormat.Wim,
                progress,
                _cancellationTokenSource.Token);

            if (success)
            {
                await DetectImageFormatAsync();
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetString("WIMUtil_Msg_DeleteWimSuccess"),
                    _localizationService.GetString("WIMUtil_Button_DeleteWim"));
            }
            else
            {
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_DeleteFailed"),
                    "Error");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error deleting WIM: {ex.Message}", ex);
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_DeleteError"), ex.Message),
                "Error");
        }
    }

    [RelayCommand]
    private async Task DeleteEsd()
    {
        try
        {
            var confirmed = await _dialogService.ShowConfirmationAsync(
                _localizationService.GetString("WIMUtil_Msg_DeleteEsdConfirm"),
                _localizationService.GetString("WIMUtil_Button_DeleteEsd"),
                _localizationService.GetString("Button_Delete"),
                _localizationService.GetString("Button_Cancel"));
            if (!confirmed) return;

            _cancellationTokenSource = new CancellationTokenSource();
            var progress = new Progress<TaskProgressDetail>(detail => { });

            var success = await _wimImageService.DeleteImageFileAsync(
                WorkingDirectory,
                ImageFormat.Esd,
                progress,
                _cancellationTokenSource.Token);

            if (success)
            {
                await DetectImageFormatAsync();
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetString("WIMUtil_Msg_DeleteEsdSuccess"),
                    _localizationService.GetString("WIMUtil_Button_DeleteEsd"));
            }
            else
            {
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_DeleteFailed"),
                    "Error");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error deleting ESD: {ex.Message}", ex);
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_DeleteError"), ex.Message),
                "Error");
        }
    }

    internal void UpdateConversionCardState()
    {
        if (BothFormatsExist || CurrentImageFormat == null)
        {
            ConvertImageCard.IsEnabled = CurrentImageFormat != null && !BothFormatsExist;
            ConvertImageCard.Description = CurrentImageFormat == null
                ? _localizationService.GetString("WIMUtil_Label_NoImageDetected")
                : _localizationService.GetString("WIMUtil_Card_BothImages_Title");
            return;
        }

        var currentFormat = CurrentImageFormat.Format == ImageFormat.Wim ? "WIM" : "ESD";
        var targetFormat = CurrentImageFormat.Format == ImageFormat.Wim ? "ESD" : "WIM";
        var currentSize = CurrentImageFormat.FileSizeBytes / (1024.0 * 1024 * 1024);
        var estimatedTargetSize = CurrentImageFormat.Format == ImageFormat.Wim ? currentSize * 0.65 : currentSize * 1.50;
        var diff = Math.Abs(estimatedTargetSize - currentSize);
        var sizeChange = CurrentImageFormat.Format == ImageFormat.Wim
            ? $"{_localizationService.GetString("WIMUtil_Label_Save")} ~{diff:F2} GB"
            : string.Format(_localizationService.GetString("WIMUtil_Label_RequiresMore"), diff.ToString("F2"));

        ConvertImageCard.Icon = CurrentImageFormat.Format == ImageFormat.Wim ? "\uE740" : "\uE741";
        ConvertImageCard.Title = string.Format(_localizationService.GetString("WIMUtil_Card_ConvertImage_Title_Dynamic"), currentFormat, targetFormat);
        ConvertImageCard.Description = $"{_localizationService.GetString("WIMUtil_Label_Current")}: install.{currentFormat.ToLower()} ({currentSize:F2} GB)\n{_localizationService.GetString("WIMUtil_Label_AfterConversion")}: ~{estimatedTargetSize:F2} GB ({sizeChange})";
        ConvertImageCard.ButtonText = string.Format(_localizationService.GetString("WIMUtil_Card_ConvertImage_Button_Dynamic"), targetFormat);
        ConvertImageCard.IsEnabled = !IsConverting;
    }

    internal static string FormatFileSize(long bytes) => $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
}
