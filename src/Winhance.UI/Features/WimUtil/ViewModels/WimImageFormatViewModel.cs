using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.WimUtil.ViewModels;

public partial class WimImageFormatViewModel : ObservableObject, IDisposable
{
    private const double BytesPerGB = 1024.0 * 1024 * 1024;

    private readonly IWimImageService _wimImageService;
    private readonly IDialogService _dialogService;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogService _logService;
    private readonly IOfflineServicingService _offlineServicingService;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _disposed;

    public string WorkingDirectory
    {
        get;
        set
        {
            if (field == value) return;
            field = value;

            // Another folder is another image, so the mode goes back to the one that works with any image.
            IsBuiltIntoImage = false;
        }
    } = string.Empty;

    public bool IsImageLocked { get; set; }

    [ObservableProperty]
    public partial bool IsBuiltIntoImage { get; set; }

    public ObservableCollection<ImageEdition> EditionChoices { get; } = [];

    [ObservableProperty]
    public partial ImageEdition? SelectedEdition { get; set; }

    [ObservableProperty]
    public partial bool ShowMountFolderWarning { get; set; }

    [ObservableProperty]
    public partial ImageFormatInfo? CurrentImageFormat { get; set; }

    [ObservableProperty]
    public partial bool ShowConversionCard { get; set; }

    [ObservableProperty]
    public partial bool BothFormatsExist { get; set; }

    [ObservableProperty]
    public partial string WimFileSize { get; set; }

    [ObservableProperty]
    public partial string EsdFileSize { get; set; }

    [ObservableProperty]
    public partial ImageDetectionResult? DetectionResult { get; set; }

    public WimImageFormatViewModel(
        IWimImageService wimImageService,
        IDialogService dialogService,
        IDispatcherService dispatcherService,
        ILocalizationService localizationService,
        ILogService logService,
        IOfflineServicingService offlineServicingService)
    {
        _wimImageService = wimImageService;
        _dialogService = dialogService;
        _dispatcherService = dispatcherService;
        _localizationService = localizationService;
        _logService = logService;
        _offlineServicingService = offlineServicingService;

        WimFileSize = string.Empty;
        EsdFileSize = string.Empty;
    }

    partial void OnIsBuiltIntoImageChanged(bool value)
    {
        ShowMountFolderWarning = value
            && !string.IsNullOrEmpty(WorkingDirectory)
            && !_offlineServicingService.CanMountFor(WorkingDirectory);
    }

    partial void OnCurrentImageFormatChanged(ImageFormatInfo? value)
    {
        var kept = SelectedEdition?.Name;
        EditionChoices.Clear();
        foreach (var edition in value?.Editions ?? [])
            EditionChoices.Add(edition);
        SelectedEdition = EditionChoices.FirstOrDefault(e => e.Name == kept) ?? EditionChoices.FirstOrDefault();
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
    private async Task DeleteWimAsync()
    {
        if (IsImageLocked) return;

        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString("WIMUtil_Msg_WorkingDirectoryRequired"));
            return;
        }

        try
        {
            var confirmed = (await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
            {
                Message = _localizationService.GetString("WIMUtil_Msg_DeleteWimConfirm"),
                Title = _localizationService.GetString("WIMUtil_Button_DeleteWim"),
                ConfirmButtonText = _localizationService.GetString("Button_Delete"),
                CancelButtonText = _localizationService.GetString("Button_Cancel"),
            })).Confirmed;
            if (!confirmed) return;

            _cancellationTokenSource?.Dispose();
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
                    _localizationService.GetString("WIMUtil_Msg_DeleteFailed"));
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error deleting WIM: {ex.Message}", ex);
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_DeleteError"), ex.Message));
        }
    }

    [RelayCommand]
    private async Task DeleteEsdAsync()
    {
        if (IsImageLocked) return;

        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString("WIMUtil_Msg_WorkingDirectoryRequired"));
            return;
        }

        try
        {
            var confirmed = (await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
            {
                Message = _localizationService.GetString("WIMUtil_Msg_DeleteEsdConfirm"),
                Title = _localizationService.GetString("WIMUtil_Button_DeleteEsd"),
                ConfirmButtonText = _localizationService.GetString("Button_Delete"),
                CancelButtonText = _localizationService.GetString("Button_Cancel"),
            })).Confirmed;
            if (!confirmed) return;

            _cancellationTokenSource?.Dispose();
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
                    _localizationService.GetString("WIMUtil_Msg_DeleteFailed"));
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error deleting ESD: {ex.Message}", ex);
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_DeleteError"), ex.Message));
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
        GC.SuppressFinalize(this);
    }

    internal static string FormatFileSize(long bytes) => $"{bytes / BytesPerGB:F2} GB";
}
