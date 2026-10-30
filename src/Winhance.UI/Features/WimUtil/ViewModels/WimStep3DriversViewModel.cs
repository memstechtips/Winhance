using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.WimUtil.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.Features.WimUtil.ViewModels;

public partial class WimStep3DriversViewModel : ObservableObject, IDisposable
{
    private readonly IWimCustomizationService _wimCustomizationService;
    private readonly ITaskProgressService _taskProgressService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IFilePickerService _filePickerService;
    private readonly ILogService _logService;
    private readonly IResourceService _resourceService;
    private readonly IAnswerFileValidator _answerFileValidator;
    private readonly AnswerFileCheckState _checkState;
    private string? _customDriversFolder;
    private bool _disposed;

    public string WorkingDirectory
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            RefreshDriversAdded();
        }
    } = string.Empty;

    public bool IsBuiltIntoImage
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            RefreshCardText();
        }
    }

    [ObservableProperty]
    public partial bool AreDriversAdded { get; set; }

    public WizardActionCard ExtractSystemDriversCard { get; private set; } = new();
    public WizardActionCard SelectCustomDriversCard { get; private set; } = new();

    public WimStep3DriversViewModel(
        IWimCustomizationService wimCustomizationService,
        ITaskProgressService taskProgressService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IFileSystemService fileSystemService,
        IFilePickerService filePickerService,
        ILogService logService,
        IResourceService resourceService,
        IAnswerFileValidator answerFileValidator,
        AnswerFileCheckState checkState)
    {
        _wimCustomizationService = wimCustomizationService;
        _taskProgressService = taskProgressService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _fileSystemService = fileSystemService;
        _filePickerService = filePickerService;
        _logService = logService;
        _resourceService = resourceService;
        _answerFileValidator = answerFileValidator;
        _checkState = checkState;

        CreateActionCards();
    }

    private void CreateActionCards()
    {
        ExtractSystemDriversCard = new WizardActionCard
        {
            IconPath = _resourceService.GetResourceIconPath("MemoryArrowDownIconPath"),
            Title = _localizationService.GetString("WIMUtil_Card_ExtractDrivers_Title"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_ExtractDrivers_Button"),
            ButtonCommand = ExtractAndAddSystemDriversCommand,
            IsEnabled = true
        };

        SelectCustomDriversCard = new WizardActionCard
        {
            IconPath = _resourceService.GetResourceIconPath("ExplorerIconPath"),
            Title = _localizationService.GetString("WIMUtil_Card_CustomDrivers_Title"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_CustomDrivers_Button"),
            ButtonCommand = SelectAndAddCustomDriversCommand,
            IsEnabled = true
        };

        RefreshCardText();
    }

    // Read from the media, so a reopened folder that already holds drivers shows them and servicing clearing them shows too.
    public void RefreshDriversAdded()
    {
        AreDriversAdded = !string.IsNullOrEmpty(WorkingDirectory) && _wimCustomizationService.AreDriversOnMedia(WorkingDirectory);
        if (AreDriversAdded)
            return;

        ExtractSystemDriversCard.IsComplete = false;
        SelectCustomDriversCard.IsComplete = false;
    }

    // The XAML binds these card instances, so a mode change rewrites their text in place.
    private void RefreshCardText()
    {
        var extractKey = IsBuiltIntoImage ? "WIMUtil_Card_ExtractDrivers_Description_Image" : "WIMUtil_Card_ExtractDrivers_Description";
        var customKey = IsBuiltIntoImage ? "WIMUtil_Card_CustomDrivers_Description_Image" : "WIMUtil_Card_CustomDrivers_Description";
        ExtractSystemDriversCard.Description = _localizationService.GetString(extractKey);
        SelectCustomDriversCard.Description = _customDriversFolder is { } folder
            ? $"{_localizationService.GetString("WIMUtil_Label_Selected")}: {folder}"
            : _localizationService.GetString(customKey);
    }

    [RelayCommand]
    private async Task ExtractAndAddSystemDrivers()
    {
        var addedDrivers = false;
        try
        {
            ExtractSystemDriversCard.IsComplete = false;
            ExtractSystemDriversCard.HasFailed = false;

            if (string.IsNullOrEmpty(WorkingDirectory))
            {
                await _dialogService.ShowWarningAsync(
                    _localizationService.GetString("WIMUtil_Msg_WorkingDirectoryRequired"),
                    _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
                return;
            }

            var confirmed = (await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
            {
                Message = _localizationService.GetString("WIMUtil_Msg_ExtractDriversConfirm"),
                Title = _localizationService.GetString("WIMUtil_Card_ExtractDrivers_Title"),
                ConfirmButtonText = _localizationService.GetString("Button_Yes"),
                CancelButtonText = _localizationService.GetString("Button_No"),
            })).Confirmed;
            if (!confirmed) return;

            ExtractSystemDriversCard.IsProcessing = true;
            ExtractSystemDriversCard.IsEnabled = false;

            _taskProgressService.StartTask(_localizationService.GetString("WIMUtil_Status_ExportingDrivers"), true);
            var progress = _taskProgressService.CreateDetailedProgress();

            var success = await _wimCustomizationService.AddDriversAsync(
                WorkingDirectory, null, progress,
                _taskProgressService.CurrentTaskCancellationSource!.Token);

            ExtractSystemDriversCard.IsProcessing = false;
            ExtractSystemDriversCard.IsEnabled = true;

            if (success)
            {
                ExtractSystemDriversCard.IsComplete = true;
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetString(IsBuiltIntoImage ? "WIMUtil_Msg_DriversSuccess_Image" : "WIMUtil_Msg_DriversSuccess"),
                    _localizationService.GetStringOrDefault("Dialog_Success", "Success"));
                addedDrivers = true;
            }
            else
            {
                ExtractSystemDriversCard.HasFailed = true;
                await _dialogService.ShowWarningAsync(
                    _localizationService.GetString("WIMUtil_Msg_NoDriversFound"),
                    _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
            }
        }
        catch (Exception ex)
        {
            _taskProgressService.FailTask();
            _logService.LogError($"Error extracting system drivers: {ex.Message}", ex);
            ExtractSystemDriversCard.IsProcessing = false;
            ExtractSystemDriversCard.IsEnabled = true;
            ExtractSystemDriversCard.HasFailed = true;
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_DriverExtractionError"), ex.Message),
                _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
        }
        finally
        {
            _taskProgressService.CompleteTask();
            RefreshDriversAdded();
        }

        if (addedDrivers)
            await CheckAnswerFileAsync();
    }

    [RelayCommand]
    private async Task SelectAndAddCustomDrivers()
    {
        var addedDrivers = false;
        try
        {
            SelectCustomDriversCard.IsComplete = false;
            SelectCustomDriversCard.HasFailed = false;

            if (string.IsNullOrEmpty(WorkingDirectory))
            {
                await _dialogService.ShowWarningAsync(
                    _localizationService.GetString("WIMUtil_Msg_WorkingDirectoryRequired"),
                    _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
                return;
            }

            var selectedPath = _filePickerService.PickFolder(_localizationService.GetString("WIMUtil_FolderDialog_SelectDrivers"));
            if (string.IsNullOrEmpty(selectedPath)) return;

            if (!_fileSystemService.DirectoryExists(selectedPath))
            {
                SelectCustomDriversCard.HasFailed = true;
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_InvalidFolder"),
                    _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
                return;
            }

            var hasFiles = _fileSystemService.GetFiles(selectedPath, "*", SearchOption.AllDirectories).Length > 0 || _fileSystemService.GetDirectories(selectedPath, "*", SearchOption.AllDirectories).Length > 0;
            if (!hasFiles)
            {
                SelectCustomDriversCard.HasFailed = true;
                await _dialogService.ShowWarningAsync(
                    _localizationService.GetString("WIMUtil_Msg_EmptyFolder"),
                    _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
                return;
            }

            SelectCustomDriversCard.IsProcessing = true;
            SelectCustomDriversCard.IsEnabled = false;
            _customDriversFolder = selectedPath;
            RefreshCardText();

            _taskProgressService.StartTask(_localizationService.GetString("WIMUtil_Status_AddingCustomDrivers"), true);
            var progress = _taskProgressService.CreateDetailedProgress();

            var success = await _wimCustomizationService.AddDriversAsync(
                WorkingDirectory, selectedPath, progress,
                _taskProgressService.CurrentTaskCancellationSource!.Token);

            SelectCustomDriversCard.IsProcessing = false;
            SelectCustomDriversCard.IsEnabled = true;

            if (success)
            {
                SelectCustomDriversCard.IsComplete = true;
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetString(IsBuiltIntoImage ? "WIMUtil_Msg_DriverFilesAdded_Image" : "WIMUtil_Msg_DriverFilesAdded"),
                    _localizationService.GetStringOrDefault("Dialog_Success", "Success"));
                addedDrivers = true;
            }
            else
            {
                SelectCustomDriversCard.HasFailed = true;
                await _dialogService.ShowWarningAsync(
                    string.Format(_localizationService.GetString("WIMUtil_Msg_NoCustomDrivers"), selectedPath),
                    _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
            }
        }
        catch (Exception ex)
        {
            _taskProgressService.FailTask();
            _logService.LogError($"Error adding custom drivers: {ex.Message}", ex);
            SelectCustomDriversCard.IsProcessing = false;
            SelectCustomDriversCard.IsEnabled = true;
            SelectCustomDriversCard.HasFailed = true;
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_DriverAdditionError"), ex.Message),
                _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
        }
        finally
        {
            _taskProgressService.CompleteTask();
            RefreshDriversAdded();
        }

        if (addedDrivers)
            await CheckAnswerFileAsync();
    }

    // Adding drivers rewrites autounattend.xml, so the copy on the media is re-checked once the
    // progress task is over (the task-output dialog would otherwise attach to a running task).
    // Storage-only drivers stage nothing under $OEM$ and create no XML; then there is nothing to check.
    private async Task CheckAnswerFileAsync()
    {
        var answerFile = _fileSystemService.CombinePath(WorkingDirectory, "autounattend.xml");
        if (_fileSystemService.FileExists(answerFile))
            await _checkState.RecheckAfterChangeAsync(answerFile, _answerFileValidator, _dialogService, _localizationService, _logService);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

}
