using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.AdvancedTools.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.AdvancedTools.ViewModels;

public partial class WimStep4IsoViewModel : ObservableObject
{
    private readonly IIsoService _isoService;
    private readonly IUsbMediaWriter _usbMediaWriter;
    private readonly ITaskProgressService _taskProgressService;
    private readonly IProcessExecutor _processExecutor;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IFilePickerService _filePickerService;
    private readonly ILogService _logService;

    public string WorkingDirectory { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OutputIsoPath { get; set; }

    [ObservableProperty]
    public partial bool IsIsoCreated { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIsoDestination))]
    [NotifyPropertyChangedFor(nameof(IsUsbDestination))]
    public partial WimOutputDestination Destination { get; set; }

    [ObservableProperty]
    public partial RemovableDrive? SelectedUsbTarget { get; set; }

    public bool IsIsoDestination => Destination == WimOutputDestination.Iso;

    public bool IsUsbDestination => Destination == WimOutputDestination.Usb;

    public ObservableCollection<RemovableDrive> UsbTargets { get; } = [];

    public WizardActionCard SelectOutputCard { get; private set; } = new();

    public WizardActionCard SelectUsbCard { get; private set; } = new();

    public WimStep4IsoViewModel(
        IIsoService isoService,
        IUsbMediaWriter usbMediaWriter,
        ITaskProgressService taskProgressService,
        IProcessExecutor processExecutor,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IFileSystemService fileSystemService,
        IFilePickerService filePickerService,
        ILogService logService)
    {
        _isoService = isoService;
        _usbMediaWriter = usbMediaWriter;
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
        SelectOutputCard = new WizardActionCard
        {
            Icon = "\uE74E",
            Title = _localizationService.GetString("WIMUtil_Card_SelectOutput_Title"),
            Description = _localizationService.GetString("WIMUtil_Label_NoLocation"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_SelectOutput_Button"),
            ButtonCommand = SelectIsoOutputLocationCommand,
            IsEnabled = true
        };

        SelectUsbCard = new WizardActionCard
        {
            Icon = "\uE88E",
            Title = _localizationService.GetString("WIMUtil_Card_SelectUsb_Title"),
            Description = _localizationService.GetString("WIMUtil_Card_SelectUsb_Description"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_SelectUsb_Button"),
            ButtonCommand = RefreshUsbTargetsCommand,
            IsEnabled = true
        };
    }

    [RelayCommand]
    private void SelectIsoDestination() => Destination = WimOutputDestination.Iso;

    [RelayCommand]
    private async Task SelectUsbDestination()
    {
        Destination = WimOutputDestination.Usb;
        await RefreshUsbTargets();
    }

    [RelayCommand]
    private async Task RefreshUsbTargets()
    {
        try
        {
            SelectUsbCard.IsProcessing = true;
            var previous = SelectedUsbTarget?.DiskNumber;
            var targets = await Task.Run(_usbMediaWriter.GetCandidateTargets);

            UsbTargets.Clear();
            foreach (var target in targets)
            {
                UsbTargets.Add(target);
            }

            SelectedUsbTarget = UsbTargets.FirstOrDefault(t => t.DiskNumber == previous) ?? UsbTargets.FirstOrDefault();
            SelectUsbCard.Description = SelectedUsbTarget is null
                ? _localizationService.GetString("WIMUtil_Label_NoUsbFound")
                : _localizationService.GetString("WIMUtil_Card_SelectUsb_Description");
        }
        catch (Exception ex)
        {
            _logService.LogError($"Could not list USB drives: {ex.Message}", ex);
            SelectUsbCard.Description = string.Format(_localizationService.GetString("WIMUtil_Status_ErrorPrefix"), ex.Message);
        }
        finally
        {
            SelectUsbCard.IsProcessing = false;
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
    private async Task CreateMedia()
    {
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString("WIMUtil_Msg_WorkingDirectoryRequired"),
                _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
            return;
        }

        if (Destination == WimOutputDestination.Usb)
        {
            await WriteUsbMedia();
            return;
        }

        try
        {
            if (string.IsNullOrEmpty(OutputIsoPath))
            {
                await _dialogService.ShowWarningAsync(
                    _localizationService.GetString("WIMUtil_Msg_OutputRequired"),
                    _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
                return;
            }

            SelectOutputCard.IsEnabled = false;
            SelectOutputCard.Opacity = 0.5;

            _taskProgressService.StartTask(_localizationService.GetString("WIMUtil_Status_CreatingIso"), true);
            var progress = _taskProgressService.CreateDetailedProgress();

            var success = await _isoService.CreateIsoAsync(WorkingDirectory, OutputIsoPath, progress, _taskProgressService.CurrentTaskCancellationSource!.Token);

            SelectOutputCard.IsEnabled = true;
            SelectOutputCard.Opacity = 1.0;

            if (success)
            {
                IsIsoCreated = true;
                SelectOutputCard.Description = _localizationService.GetString("WIMUtil_Desc_IsoCreatedSuccess");

                var openFolder = (await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
                {
                    Message = string.Format(_localizationService.GetString("WIMUtil_Msg_IsoCreatedSuccess"), OutputIsoPath),
                    Title = _localizationService.GetString("WIMUtil_Desc_IsoCreatedSuccess"),
                    ConfirmButtonText = _localizationService.GetString("WIMUtil_Button_OpenFolder"),
                    CancelButtonText = _localizationService.GetString("Button_Close"),
                })).Confirmed;
                if (openFolder)
                {
                    _processExecutor.ShellExecuteAsync("explorer.exe", $"/select,\"{OutputIsoPath}\"").FireAndForget(_logService);
                }
            }
            else
            {
                _taskProgressService.FailTask();
                SelectOutputCard.Description = _localizationService.GetString("WIMUtil_Desc_IsoCreateFailed");
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_IsoCreationFailed"),
                    _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
            }
        }
        catch (OperationCanceledException)
        {
            _taskProgressService.CancelTask();
            SelectOutputCard.IsEnabled = true;
            SelectOutputCard.Opacity = 1.0;
            try { if (_fileSystemService.FileExists(OutputIsoPath)) _fileSystemService.DeleteFile(OutputIsoPath); } catch (Exception ex) { _logService.LogDebug($"Best-effort incomplete ISO cleanup failed: {ex.Message}"); }
            SelectOutputCard.Description = _localizationService.GetString("WIMUtil_Desc_IsoCreateCancelled");
        }
        catch (InsufficientDiskSpaceException spaceEx)
        {
            _taskProgressService.FailTask();
            SelectOutputCard.IsEnabled = true;
            SelectOutputCard.Opacity = 1.0;
            SelectOutputCard.Description = string.Format(_localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace"), spaceEx.DriveName);
            await _dialogService.ShowWarningAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_InsufficientSpace_Create"), spaceEx.DriveName, spaceEx.RequiredGB.ToString("F2"), spaceEx.AvailableGB.ToString("F2"), (spaceEx.RequiredGB - spaceEx.AvailableGB).ToString("F2")),
                string.Format(_localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace"), spaceEx.DriveName));
        }
        catch (Exception ex)
        {
            _taskProgressService.FailTask();
            SelectOutputCard.IsEnabled = true;
            SelectOutputCard.Opacity = 1.0;
            SelectOutputCard.Description = string.Format(_localizationService.GetString("WIMUtil_Status_ErrorPrefix"), ex.Message);
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_IsoCreationError"), ex.Message),
                _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
        }
        finally
        {
            _taskProgressService.CompleteTask();
        }
    }

    private async Task WriteUsbMedia()
    {
        var target = SelectedUsbTarget;
        if (target is null)
        {
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString("WIMUtil_Msg_UsbRequired"),
                _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
            return;
        }

        // Winhance's one destructive operation. The dialog names the device and its size, because
        // "are you sure" next to a drive picker is how people wipe the wrong stick.
        var confirmed = (await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
        {
            Message = string.Format(
                _localizationService.GetString("WIMUtil_Msg_UsbEraseConfirm"),
                target.Model,
                target.SizeGigabytes.ToString("F1"),
                target.DiskNumber.ToString()),
            Title = _localizationService.GetString("WIMUtil_Msg_UsbEraseTitle"),
            ConfirmButtonText = _localizationService.GetString("WIMUtil_Button_EraseAndWrite"),
            CancelButtonText = _localizationService.GetString("Button_Close"),
        })).Confirmed;

        if (!confirmed)
        {
            return;
        }

        try
        {
            SelectUsbCard.IsEnabled = false;
            SelectUsbCard.Opacity = 0.5;

            _taskProgressService.StartTask(_localizationService.GetString("WIMUtil_Status_WritingUsb"), true);
            var progress = _taskProgressService.CreateDetailedProgress();
            var token = _taskProgressService.CurrentTaskCancellationSource!.Token;

            await Task.Run(() => _usbMediaWriter.Write(target, WorkingDirectory, progress, token), token);

            IsIsoCreated = true;
            SelectUsbCard.IsComplete = true;
            SelectUsbCard.Description = _localizationService.GetString("WIMUtil_Desc_UsbWriteSuccess");

            await _dialogService.ShowInformationAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_UsbWriteSuccess"), target.Model),
                _localizationService.GetStringOrDefault("Dialog_Success", "Success"));
        }
        catch (UsbMediaErasedException erased) when (erased.WasCancelled)
        {
            _taskProgressService.CancelTask();
            SelectUsbCard.Description = _localizationService.GetString("WIMUtil_Desc_UsbWriteCancelled");
            await _dialogService.ShowWarningAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_UsbDriveErased"), erased.Target?.Model),
                _localizationService.GetString("WIMUtil_Desc_UsbWriteCancelled"));
        }
        catch (OperationCanceledException)
        {
            _taskProgressService.CancelTask();
            SelectUsbCard.Description = _localizationService.GetString("WIMUtil_Desc_UsbWriteCancelled");
        }
        catch (Exception ex)
        {
            _taskProgressService.FailTask();
            _logService.LogError($"Could not write the USB drive: {ex.Message}", ex);
            SelectUsbCard.HasFailed = true;
            SelectUsbCard.Description = string.Format(_localizationService.GetString("WIMUtil_Status_ErrorPrefix"), ex.Message);

            // Once the drive has been wiped, "could not write" on its own reads as "nothing
            // happened", and the user unplugs a blank stick.
            var message = string.Format(_localizationService.GetString("WIMUtil_Msg_UsbWriteFailed"), ex.Message);
            if (ex is UsbMediaErasedException failedAfterErase)
            {
                message += Environment.NewLine + Environment.NewLine
                    + string.Format(_localizationService.GetString("WIMUtil_Msg_UsbDriveErased"), failedAfterErase.Target?.Model);
            }

            await _dialogService.ShowErrorAsync(message, _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
        }
        finally
        {
            SelectUsbCard.IsEnabled = true;
            SelectUsbCard.Opacity = 1.0;
            _taskProgressService.CompleteTask();
        }
    }
}
