using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.WimUtil.Models;

namespace Winhance.UI.Features.WimUtil.ViewModels;

public partial class WimStep5IsoViewModel : ObservableObject
{
    private readonly IWimCustomizationService _wimCustomizationService;
    private readonly IIsoService _isoService;
    private readonly IUsbMediaWriter _usbMediaWriter;
    private readonly IBootManagerService _bootManagerService;
    private readonly ITaskProgressService _taskProgressService;
    private readonly IProcessExecutor _processExecutor;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IFilePickerService _filePickerService;
    private readonly ILogService _logService;
    private readonly IAnswerFileValidator _answerFileValidator;
    private readonly AnswerFileCheckState _checkState;
    private readonly IOfflineServicingService _offlineServicingService;

    public string WorkingDirectory { get; set; } = string.Empty;

    public bool IsBuiltIntoImage { get; set; }

    public bool IsReadyForServicing { get; set; }

    // Raised once servicing has moved staged items out of the media folder and into the image.
    public event EventHandler<OfflineServicingResult>? ImageServiced;

    [ObservableProperty]
    public partial bool IsCreatingMedia { get; set; }

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

    [ObservableProperty]
    public partial bool ShowUsbHardDrives { get; set; }

    public bool IsIsoDestination => Destination == WimOutputDestination.Iso;

    public bool IsUsbDestination => Destination == WimOutputDestination.Usb;

    public ObservableCollection<RemovableDrive> UsbTargets { get; } = [];

    public ObservableCollection<BootManagerChoice> BootManagerChoices { get; } = [];

    [ObservableProperty]
    public partial BootManagerChoice? SelectedBootManager { get; set; }

    [ObservableProperty]
    public partial bool ShowBootManagerChoice { get; set; }

    [ObservableProperty]
    public partial string BootManagerDescription { get; set; }

    [ObservableProperty]
    public partial bool IsBootManagerOutdated { get; set; }

    public WizardActionCard SelectOutputCard { get; private set; } = new();

    public WizardActionCard SelectUsbCard { get; private set; } = new();

    public WimStep5IsoViewModel(
        IWimCustomizationService wimCustomizationService,
        IIsoService isoService,
        IUsbMediaWriter usbMediaWriter,
        IBootManagerService bootManagerService,
        ITaskProgressService taskProgressService,
        IProcessExecutor processExecutor,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IFileSystemService fileSystemService,
        IFilePickerService filePickerService,
        ILogService logService,
        IAnswerFileValidator answerFileValidator,
        AnswerFileCheckState checkState,
        IOfflineServicingService offlineServicingService)
    {
        _wimCustomizationService = wimCustomizationService;
        _isoService = isoService;
        _usbMediaWriter = usbMediaWriter;
        _bootManagerService = bootManagerService;
        _taskProgressService = taskProgressService;
        _processExecutor = processExecutor;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _fileSystemService = fileSystemService;
        _filePickerService = filePickerService;
        _logService = logService;
        _answerFileValidator = answerFileValidator;
        _checkState = checkState;
        _offlineServicingService = offlineServicingService;

        OutputIsoPath = string.Empty;
        BootManagerDescription = string.Empty;

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

    // Run once the ISO is extracted. On failure the card stays hidden and the media keeps its own boot
    // manager.
    public async Task InspectBootManagerAsync()
    {
        try
        {
            var options = await _bootManagerService.InspectAsync(WorkingDirectory, CancellationToken.None);

            BootManagerChoices.Clear();
            if (options.Offers2023)
            {
                BootManagerChoices.Add(new BootManagerChoice(BootManager.Pca2011, _localizationService.GetString("WIMUtil_BootManager_Pca2011")));
                BootManagerChoices.Add(new BootManagerChoice(BootManager.Ca2023, _localizationService.GetString("WIMUtil_BootManager_Ca2023")));
            }

            SelectedBootManager = BootManagerChoices.FirstOrDefault(choice => choice.Value == options.Default);
            BootManagerDescription = _localizationService.GetString(options.Default == BootManager.Ca2023
                ? "WIMUtil_Card_BootManager_DescriptionRevoked"
                : "WIMUtil_Card_BootManager_Description");
            ShowBootManagerChoice = options.Offers2023;
            IsBootManagerOutdated = options.IsOutdated;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not read the media's boot manager: {ex.Message}");
            BootManagerChoices.Clear();
            SelectedBootManager = null;
            ShowBootManagerChoice = false;
            IsBootManagerOutdated = false;
        }
    }

    private Task<BootFileReplacements> PrepareBootFilesAsync(CancellationToken cancellationToken) =>
        _bootManagerService.PrepareAsync(WorkingDirectory, SelectedBootManager?.Value ?? BootManager.Pca2011, cancellationToken);

    [RelayCommand]
    private void SelectIsoDestination() => Destination = WimOutputDestination.Iso;

    [RelayCommand]
    private async Task SelectUsbDestination()
    {
        Destination = WimOutputDestination.Usb;
        await RefreshUsbTargets();
    }

    partial void OnShowUsbHardDrivesChanged(bool value)
    {
        if (IsUsbDestination)
            _ = RefreshUsbTargetsCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task RefreshUsbTargets()
    {
        try
        {
            SelectUsbCard.IsProcessing = true;
            var previous = SelectedUsbTarget?.DiskNumber;
            var includeHardDrives = ShowUsbHardDrives;
            var targets = await Task.Run(() => _usbMediaWriter.GetCandidateTargets(includeHardDrives));

            UsbTargets.Clear();
            foreach (var target in targets)
            {
                UsbTargets.Add(target);
            }

            // Never a default on a USB hard drive: it is the likelier one to hold someone's backups.
            SelectedUsbTarget = UsbTargets.FirstOrDefault(t => t.DiskNumber == previous) ?? UsbTargets.FirstOrDefault(t => t.HasRemovableMedia);
            SelectUsbCard.Description = UsbTargets.Count == 0
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
            SelectOutputCard.Description = $"{_localizationService.GetString("WIMUtil_Label_Output")}: {OutputIsoPath}";
        }
    }

    [RelayCommand]
    private async Task CreateMedia()
    {
        IsCreatingMedia = true;
        try
        {
            await CreateMediaCoreAsync();
        }
        finally
        {
            IsCreatingMedia = false;
        }
    }

    private async Task CreateMediaCoreAsync()
    {
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString("WIMUtil_Msg_WorkingDirectoryRequired"),
                _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
            return;
        }

        if (IsBuiltIntoImage && !IsReadyForServicing && _offlineServicingService.HasStagedItems(WorkingDirectory))
        {
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString("WIMUtil_Msg_ServicingNotPrepared"),
                _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
            return;
        }

        // Steps 2 and 3 already ensured this; here it only self-heals and must never block output.
        // Image mode skips it: servicing removes these steps, or re-ensures the ones it could not build in.
        if (!IsBuiltIntoImage)
        {
            try
            {
                var driverStep = await _wimCustomizationService.EnsureDriverInstallStepAsync(WorkingDirectory);
                if (driverStep is DriverInstallStepResult.Added or DriverInstallStepResult.CreatedXml)
                    _logService.LogWarning($"Driver install step was missing at media-creation time; repaired as {driverStep}");
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Could not verify the driver install step: {ex.Message}");
            }

            try
            {
                var updateStep = await _wimCustomizationService.EnsureUpdateInstallStepAsync(WorkingDirectory);
                if (updateStep is UpdateInstallStepResult.Added or UpdateInstallStepResult.CreatedXml)
                    _logService.LogWarning($"Update install step was missing at media-creation time; repaired as {updateStep}");
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Could not verify the update install step: {ex.Message}");
            }
        }

        if (Destination == WimOutputDestination.Usb)
        {
            await WriteUsbMedia();
            return;
        }

        var answerFileChanged = false;
        try
        {
            if (string.IsNullOrEmpty(OutputIsoPath))
            {
                await _dialogService.ShowWarningAsync(
                    _localizationService.GetString("WIMUtil_Msg_OutputRequired"),
                    _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
                return;
            }

            if (!await AnswerFileAcceptedAsync())
                return;

            SelectOutputCard.IsEnabled = false;
            SelectOutputCard.Opacity = 0.5;

            _taskProgressService.StartTask(_localizationService.GetString("WIMUtil_Status_CreatingIso"), true);
            var progress = _taskProgressService.CreateDetailedProgress();
            var token = _taskProgressService.CurrentTaskCancellationSource!.Token;
            answerFileChanged = await ServiceImageIfNeededAsync(progress, token);
            var replacements = await PrepareBootFilesAsync(token);

            var success = await _isoService.CreateIsoAsync(WorkingDirectory, OutputIsoPath, replacements, progress, token);

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

        if (answerFileChanged)
            await RecheckServicedAnswerFileAsync();
    }

    // True when servicing rewrote autounattend.xml (built-in items drop their setup steps), so the file
    // AnswerFileAcceptedAsync checked is no longer the one on the media.
    private async Task<bool> ServiceImageIfNeededAsync(IProgress<TaskProgressDetail> progress, CancellationToken token)
    {
        if (!IsBuiltIntoImage || !_offlineServicingService.HasStagedItems(WorkingDirectory))
            return false;

        progress.Report(new TaskProgressDetail { StatusText = _localizationService.GetString("WIMUtil_Status_Servicing") });
        var result = await _offlineServicingService.ServiceAsync(WorkingDirectory, progress, token);
        ImageServiced?.Invoke(this, result);
        if (result.AnyFellBack)
        {
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString(FellBackMessageKey(result)),
                _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
        }
        return result.Drivers != ServicedItemOutcome.NotStaged || result.Update != ServicedItemOutcome.NotStaged;
    }

    private Task RecheckServicedAnswerFileAsync() =>
        _checkState.RecheckAfterChangeAsync(
            _fileSystemService.CombinePath(WorkingDirectory, "autounattend.xml"),
            _answerFileValidator, _dialogService, _localizationService, _logService);

    private static string FellBackMessageKey(OfflineServicingResult result) => (result.Drivers, result.Update) switch
    {
        (ServicedItemOutcome.FellBackToSetup, ServicedItemOutcome.FellBackToSetup) => "WIMUtil_Msg_ServicingFellBack_Both",
        (ServicedItemOutcome.FellBackToSetup, _) => "WIMUtil_Msg_ServicingFellBack_Drivers",
        _ => "WIMUtil_Msg_ServicingFellBack_Update",
    };

    // The last look at the file Setup will read, once the destination itself is valid. Findings are
    // shown with Continue anyway / Cancel; a check that cannot run is logged and never stops the media.
    private async Task<bool> AnswerFileAcceptedAsync()
    {
        var answerFile = _fileSystemService.CombinePath(WorkingDirectory, "autounattend.xml");
        if (!_fileSystemService.FileExists(answerFile))
            return true;

        try
        {
            var report = await _answerFileValidator.ValidateAsync(answerFile);
            _checkState.Publish(answerFile, report);
            if (report.Findings.Count == 0)
                return true;

            return await _dialogService.ShowSummaryAsync(AnswerFileReportDialog.Request(report, _localizationService) with
            {
                ConfirmButtonText = _localizationService.GetString("WIMUtil_Button_ContinueAnyway"),
            });
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not check autounattend.xml: {ex.Message}");
            return true;
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

        if (!await AnswerFileAcceptedAsync())
            return;

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

        var answerFileChanged = false;
        try
        {
            SelectUsbCard.IsEnabled = false;
            SelectUsbCard.Opacity = 0.5;

            _taskProgressService.StartTask(_localizationService.GetString("WIMUtil_Status_WritingUsb"), true);
            var progress = _taskProgressService.CreateDetailedProgress();
            var token = _taskProgressService.CurrentTaskCancellationSource!.Token;

            // Before the writer erases the stick, so a servicing failure leaves the drive as it was.
            answerFileChanged = await ServiceImageIfNeededAsync(progress, token);
            var replacements = await PrepareBootFilesAsync(token);

            await Task.Run(() => _usbMediaWriter.Write(target, WorkingDirectory, replacements, progress, token), token);

            IsIsoCreated = true;
            SelectUsbCard.IsComplete = true;
            SelectUsbCard.Description = _localizationService.GetString("WIMUtil_Desc_UsbWriteSuccess");

            await _dialogService.ShowInformationAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_UsbWriteSuccess"), target.Model),
                _localizationService.GetStringOrDefault("Dialog_Success", "Success"));
        }
        catch (InsufficientDiskSpaceException spaceEx)
        {
            _taskProgressService.FailTask();
            SelectUsbCard.HasFailed = true;
            SelectUsbCard.Description = string.Format(_localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace"), spaceEx.DriveName);
            await _dialogService.ShowWarningAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_InsufficientSpace_Create"), spaceEx.DriveName, spaceEx.RequiredGB.ToString("F2"), spaceEx.AvailableGB.ToString("F2"), (spaceEx.RequiredGB - spaceEx.AvailableGB).ToString("F2")),
                string.Format(_localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace"), spaceEx.DriveName));
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

        if (answerFileChanged)
            await RecheckServicedAnswerFileAsync();
    }
}

public enum WimOutputDestination
{
    Iso,
    Usb,
}
