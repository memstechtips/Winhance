using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.WimUtil.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.Features.WimUtil.ViewModels;

public partial class WimImageFormatViewModel : ObservableObject, IDisposable
{
    private const string EditionSettingId = "autounattend-edition";

    private readonly IWimImageService _wimImageService;
    private readonly ITaskProgressService _taskProgressService;
    private readonly IDialogService _dialogService;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogService _logService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IAutounattendReader _autounattendReader;
    private readonly IOfflineServicingService _offlineServicingService;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _disposed;
    private bool _rebuildingChoices;

    public string WorkingDirectory
    {
        get;
        set
        {
            if (field == value) return;
            field = value;

            // Another folder is another image, so the mode goes back to the one that works with any image.
            SelectedServicing = ServicingChoices.FirstOrDefault(choice => !choice.BuiltIntoImage);
        }
    } = string.Empty;

    public bool IsImageLocked
    {
        get;
        set
        {
            field = value;
            UpdateConversionCardState();
            PrepareImageForServicingCommand.NotifyCanExecuteChanged();
        }
    }

    public ObservableCollection<ServicingChoice> ServicingChoices { get; } = [];

    [ObservableProperty]
    public partial ServicingChoice? SelectedServicing { get; set; }

    public ObservableCollection<ImageEdition> EditionChoices { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrepareImageForServicingCommand))]
    public partial ImageEdition? SelectedEdition { get; set; }

    [ObservableProperty]
    public partial bool ShowHostDismWarning { get; set; }

    [ObservableProperty]
    public partial bool ShowMountFolderWarning { get; set; }

    public bool IsBuiltIntoImage => SelectedServicing?.BuiltIntoImage == true;

    // One edition in a WIM is the only shape DISM can mount and service at index 1.
    public bool IsImageReadyForServicing =>
        CurrentImageFormat is { Format: ImageFormat.Wim, Editions.Count: 1 } && !BothFormatsExist;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrepareImageForServicingCommand))]
    public partial ImageFormatInfo? CurrentImageFormat { get; set; }

    [ObservableProperty]
    public partial bool ShowConversionCard { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrepareImageForServicingCommand))]
    public partial bool IsConverting { get; set; }

    [ObservableProperty]
    public partial string ConversionStatus { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrepareImageForServicingCommand))]
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
        ILogService logService,
        IFileSystemService fileSystemService,
        IAutounattendReader autounattendReader,
        IOfflineServicingService offlineServicingService)
    {
        _wimImageService = wimImageService;
        _taskProgressService = taskProgressService;
        _dialogService = dialogService;
        _dispatcherService = dispatcherService;
        _localizationService = localizationService;
        _logService = logService;
        _fileSystemService = fileSystemService;
        _autounattendReader = autounattendReader;
        _offlineServicingService = offlineServicingService;

        ConversionStatus = string.Empty;
        WimFileSize = string.Empty;
        EsdFileSize = string.Empty;

        CreateActionCards();
        RefreshLocalizedText();
    }

    public void RefreshLocalizedText()
    {
        var builtIn = IsBuiltIntoImage;
        _rebuildingChoices = true;
        try
        {
            ServicingChoices.Clear();
            ServicingChoices.Add(new ServicingChoice(false, _localizationService.GetString("WIMUtil_Servicing_DuringSetup")));
            ServicingChoices.Add(new ServicingChoice(true, _localizationService.GetString("WIMUtil_Servicing_BuiltIn")));
        }
        finally
        {
            _rebuildingChoices = false;
        }
        SelectedServicing = ServicingChoices.First(choice => choice.BuiltIntoImage == builtIn);
    }

    // Clearing the list makes the bound ComboBox write null back; passed on, that flips every step to During setup and back.
    partial void OnSelectedServicingChanged(ServicingChoice? oldValue, ServicingChoice? newValue)
    {
        if (_rebuildingChoices || oldValue?.BuiltIntoImage == newValue?.BuiltIntoImage)
            return;

        OnPropertyChanged(nameof(IsBuiltIntoImage));
        UpdateHostDismWarning();
        ShowMountFolderWarning = IsBuiltIntoImage
            && !string.IsNullOrEmpty(WorkingDirectory)
            && !_offlineServicingService.CanMountFor(WorkingDirectory);
        if (CurrentImageFormat is not null)
            UpdateConversionCardState();
    }

    partial void OnCurrentImageFormatChanged(ImageFormatInfo? value)
    {
        var kept = SelectedEdition?.Name;
        EditionChoices.Clear();
        foreach (var edition in value?.Editions ?? [])
            EditionChoices.Add(edition);
        SelectedEdition = EditionChoices.FirstOrDefault(e => e.Name == kept) ?? EditionChoices.FirstOrDefault();

        OnPropertyChanged(nameof(IsImageReadyForServicing));
        UpdateHostDismWarning();
    }

    partial void OnBothFormatsExistChanged(bool value) => OnPropertyChanged(nameof(IsImageReadyForServicing));

    // An older DISM may not service a newer image; the service falls back to setup when it cannot.
    private void UpdateHostDismWarning() =>
        ShowHostDismWarning = IsBuiltIntoImage
            && CurrentImageFormat is { } image
            && Environment.OSVersion.Version.Build < image.Build;

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
        if (CurrentImageFormat == null || IsImageLocked) return;

        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString("WIMUtil_Msg_WorkingDirectoryRequired"),
                _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
            return;
        }

        try
        {
            var targetFormat = CurrentImageFormat.Format == ImageFormat.Wim ? ImageFormat.Esd : ImageFormat.Wim;
            var targetFormatName = targetFormat == ImageFormat.Wim ? "WIM" : "ESD";
            var currentFormatName = CurrentImageFormat.Format == ImageFormat.Wim ? "WIM" : "ESD";

            var confirmKey = targetFormat == ImageFormat.Esd ? "WIMUtil_Msg_ConvertConfirm_Esd" : "WIMUtil_Msg_ConvertConfirm_Wim";
            var currentSize = CurrentImageFormat.FileSizeBytes / (1024.0 * 1024 * 1024);
            var estimatedTargetSize = CurrentImageFormat.Format == ImageFormat.Wim ? currentSize * 0.65 : currentSize * 1.50;
            var diff = Math.Abs(estimatedTargetSize - currentSize);
            var confirmed = (await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
            {
                Message = string.Format(_localizationService.GetString(confirmKey), diff.ToString("F2")),
                Title = string.Format(_localizationService.GetString("WIMUtil_Card_ConvertImage_Button_Dynamic"), targetFormatName),
                ConfirmButtonText = _localizationService.GetString("Button_Yes"),
                CancelButtonText = _localizationService.GetString("Button_No"),
            })).Confirmed;
            if (!confirmed) return;

            IsConverting = true;
            ConvertImageCard.IsProcessing = true;
            ConvertImageCard.IsEnabled = false;
            ConversionStatus = string.Format(_localizationService.GetString("WIMUtil_Status_ConvertingToFormat"), currentFormatName, targetFormatName);

            _taskProgressService.StartTask(string.Format(_localizationService.GetString("WIMUtil_Status_ConvertingToFormat"), currentFormatName, targetFormatName), true);
            var progress = _taskProgressService.CreateDetailedProgress();

            var success = await _wimImageService.ConvertImageAsync(WorkingDirectory, targetFormat, progress, _taskProgressService.CurrentTaskCancellationSource!.Token);

            if (success)
            {
                ConvertImageCard.IsComplete = true;
                ConversionStatus = string.Format(_localizationService.GetString("WIMUtil_Status_ConversionSuccess"), targetFormatName);
                await DetectImageFormatAsync();
                await _dialogService.ShowInformationAsync(
                    string.Format(_localizationService.GetString("WIMUtil_Msg_ConversionSuccess"), targetFormatName),
                    _localizationService.GetStringOrDefault("Dialog_Success", "Success"));
            }
            else
            {
                _taskProgressService.FailTask();
                ConvertImageCard.HasFailed = true;
                ConversionStatus = _localizationService.GetString("WIMUtil_Status_ConversionFailed");
                await _dialogService.ShowErrorAsync(
                    string.Format(_localizationService.GetString("WIMUtil_Msg_ConversionFailed"), targetFormatName),
                    _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
            }
        }
        catch (OperationCanceledException)
        {
            _taskProgressService.CancelTask();
            ConversionStatus = _localizationService.GetString("WIMUtil_Status_ConversionCancelled");
        }
        catch (Exception ex)
        {
            _taskProgressService.FailTask();
            _logService.LogError($"Error during conversion: {ex.Message}", ex);
            ConvertImageCard.HasFailed = true;
            ConversionStatus = string.Format(_localizationService.GetString("WIMUtil_Status_ErrorPrefix"), ex.Message);
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_ConversionError"), ex.Message),
                _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
        }
        finally
        {
            IsConverting = false;
            ConvertImageCard.IsProcessing = false;
            ConvertImageCard.IsEnabled = !IsImageLocked;
            _taskProgressService.CompleteTask();
        }
    }

    private bool CanPrepareImageForServicing() =>
        SelectedEdition is not null && !IsConverting && !IsImageLocked && !BothFormatsExist && !IsImageReadyForServicing;

    [RelayCommand(CanExecute = nameof(CanPrepareImageForServicing))]
    private async Task PrepareImageForServicing()
    {
        if (!CanPrepareImageForServicing() || SelectedEdition is not { } edition) return;

        var confirmed = (await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
        {
            Message = _localizationService.GetString("WIMUtil_Msg_PrepareServicing_Confirm", edition.Name),
            Title = _localizationService.GetString("WIMUtil_Card_PrepareServicing_Title"),
            ConfirmButtonText = _localizationService.GetString("Button_Yes"),
            CancelButtonText = _localizationService.GetString("Button_No"),
        })).Confirmed;
        if (!confirmed) return;

        try
        {
            IsConverting = true;
            var cancellation = _taskProgressService.StartTask(_localizationService.GetString("WIMUtil_Status_PreparingImage"), true);
            var result = await _wimImageService.KeepEditionAsWimAsync(
                WorkingDirectory, edition.Name, _taskProgressService.CreateDetailedProgress(), cancellation.Token);

            await DetectImageFormatAsync();
            if (!result.Removed)
            {
                _taskProgressService.FailTask();
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_PrepareServicing_Failed"),
                    _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
                return;
            }

            if (result.LeftoverImage is { } leftover)
            {
                await _dialogService.ShowWarningAsync(
                    _localizationService.GetString("WIMUtil_Generate_Summary_OldImageLeft", leftover),
                    _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
            }
            await WarnIfAnswerFilePicksAnotherAsync(edition);
        }
        catch (OperationCanceledException)
        {
            _taskProgressService.CancelTask();
        }
        catch (InsufficientDiskSpaceException spaceEx)
        {
            _taskProgressService.FailTask();
            await _dialogService.ShowWarningAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_InsufficientSpace_Prepare"), spaceEx.DriveName, spaceEx.RequiredGB.ToString("F2"), spaceEx.AvailableGB.ToString("F2"), (spaceEx.RequiredGB - spaceEx.AvailableGB).ToString("F2")),
                string.Format(_localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace"), spaceEx.DriveName));
        }
        catch (Exception ex)
        {
            _taskProgressService.FailTask();
            _logService.LogError($"Error keeping one edition: {ex.Message}", ex);
            await _dialogService.ShowErrorAsync(
                _localizationService.GetString("WIMUtil_Msg_PrepareServicing_Failed"),
                _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
        }
        finally
        {
            IsConverting = false;
            _taskProgressService.CompleteTask();
        }
    }

    // Only a file Winhance made says which edition it picks; any other answer file is left alone.
    private async Task WarnIfAnswerFilePicksAnotherAsync(ImageEdition kept)
    {
        var answerFile = _fileSystemService.CombinePath(WorkingDirectory, "autounattend.xml");
        if (!_fileSystemService.FileExists(answerFile))
            return;

        int? picked;
        try
        {
            picked = (await _autounattendReader.ReadAsync(answerFile))?.Config?.Autounattend.Features.Values
                .SelectMany(section => section.Items)
                .FirstOrDefault(item => item.Id == EditionSettingId)?.SelectedIndex;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not read the edition autounattend.xml picks: {ex.Message}");
            return;
        }

        if (MediaEditions.PicksAnother(picked, kept.EditionId))
        {
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString("WIMUtil_Msg_AnswerFilePicksAnotherEdition", kept.Name),
                _localizationService.GetString("WIMUtil_Generate_Plan_ChosenNotOnMedia"));
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
        if (IsImageLocked) return;

        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString("WIMUtil_Msg_WorkingDirectoryRequired"),
                _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
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
                    _localizationService.GetString("WIMUtil_Msg_DeleteFailed"),
                    _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error deleting WIM: {ex.Message}", ex);
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_DeleteError"), ex.Message),
                _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
        }
    }

    [RelayCommand]
    private async Task DeleteEsd()
    {
        if (IsImageLocked) return;

        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString("WIMUtil_Msg_WorkingDirectoryRequired"),
                _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
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
                    _localizationService.GetString("WIMUtil_Msg_DeleteFailed"),
                    _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error deleting ESD: {ex.Message}", ex);
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_DeleteError"), ex.Message),
                _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
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
        ConvertImageCard.IsEnabled = !IsConverting && !IsImageLocked && !IsBuiltIntoImage;
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

    internal static string FormatFileSize(long bytes) => $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
}
