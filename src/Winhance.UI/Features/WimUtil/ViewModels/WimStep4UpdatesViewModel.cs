using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Winhance.UI.Features.WimUtil.Models;

namespace Winhance.UI.Features.WimUtil.ViewModels;

public partial class WimStep4UpdatesViewModel : ObservableObject
{
    private readonly IWimCustomizationService _wimCustomizationService;
    private readonly ITaskProgressService _taskProgressService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IFileSystemService _fileSystemService;
    private readonly ILogService _logService;
    private readonly IAnswerFileValidator _answerFileValidator;
    private readonly AnswerFileCheckState _checkState;
    private readonly IWindowsUpdateListService _updateListService;
    private readonly IWindowsUpdateStager _updateStager;
    private WindowsUpdate? _offer;
    private int _lookupVersion;
    private bool _isLookingUp;

    public string WorkingDirectory { get; set; } = string.Empty;

    public bool IsBuiltIntoImage
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            // A running lookup shows Looking, and writes the text for whichever mode is current when it ends.
            if (!AddWindowsUpdateCard.IsProcessing && !_isLookingUp)
                ShowDescription();
        }
    }

    [ObservableProperty]
    public partial bool IsUpdateAdded { get; set; }

    public WizardActionCard AddWindowsUpdateCard { get; }

    public WimStep4UpdatesViewModel(
        IWimCustomizationService wimCustomizationService,
        ITaskProgressService taskProgressService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IFileSystemService fileSystemService,
        ILogService logService,
        IResourceService resourceService,
        IAnswerFileValidator answerFileValidator,
        AnswerFileCheckState checkState,
        IWindowsUpdateListService updateListService,
        IWindowsUpdateStager updateStager)
    {
        _wimCustomizationService = wimCustomizationService;
        _taskProgressService = taskProgressService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _fileSystemService = fileSystemService;
        _logService = logService;
        _answerFileValidator = answerFileValidator;
        _checkState = checkState;
        _updateListService = updateListService;
        _updateStager = updateStager;

        AddWindowsUpdateCard = new WizardActionCard
        {
            IconPath = resourceService.GetResourceIconPath("MemoryArrowDownIconPath"),
            Title = _localizationService.GetString("WIMUtil_Card_AddUpdate_Title"),
            Description = _localizationService.GetString("WIMUtil_Card_AddUpdate_Description_None"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_AddUpdate_Button"),
            ButtonCommand = AddWindowsUpdateCommand,
            IsEnabled = false
        };
    }

    // x86 and media without a reported build or architecture have no update on the list.
    public async Task SetMediaAsync(ImageFormatInfo? media)
    {
        var version = ++_lookupVersion;
        _isLookingUp = false;
        _offer = null;

        if (media is not { Build: > 0, Architecture: "x64" or "arm64" })
        {
            ShowUpdateState();
            return;
        }

        AddWindowsUpdateCard.IsEnabled = false;
        AddWindowsUpdateCard.IsComplete = false;
        AddWindowsUpdateCard.HasFailed = false;
        AddWindowsUpdateCard.Description = _localizationService.GetString("WIMUtil_Card_AddUpdate_Description_Looking");

        _isLookingUp = true;
        WindowsUpdate? offer;
        try
        {
            offer = await _updateListService.FindUpdateAsync(media.Build, media.Ubr, media.Architecture);
        }
        finally
        {
            if (version == _lookupVersion)
                _isLookingUp = false;
        }
        if (version != _lookupVersion)
            return;

        _offer = offer;
        ShowUpdateState();
    }

    public void ShowUpdateState()
    {
        if (_isLookingUp)
            return;
        AddWindowsUpdateCard.HasFailed = false;
        AddWindowsUpdateCard.IsEnabled = _offer is not null;

        var staged = !string.IsNullOrEmpty(WorkingDirectory) && _updateStager.IsStaged(WorkingDirectory);
        AddWindowsUpdateCard.IsComplete = staged;
        IsUpdateAdded = staged;
        ShowDescription();
    }

    private void ShowDescription()
    {
        if (IsUpdateAdded)
        {
            var stagedKey = IsBuiltIntoImage ? "WIMUtil_Card_AddUpdate_Description_Staged_Image" : "WIMUtil_Card_AddUpdate_Description_Staged";
            AddWindowsUpdateCard.Description = _localizationService.GetString(stagedKey);
        }
        else if (_offer is { } offer)
        {
            var offerKey = IsBuiltIntoImage ? "WIMUtil_Card_AddUpdate_Description_Offer_Image" : "WIMUtil_Card_AddUpdate_Description_Offer";
            AddWindowsUpdateCard.Description = _localizationService.GetString(
                offerKey,
                offer.Kb,
                offer.ReleaseDate.ToString("d", CultureInfo.CurrentCulture),
                AppItemViewModel.FormatSize(offer.DownloadBytes));
        }
        else
        {
            AddWindowsUpdateCard.Description = _localizationService.GetString("WIMUtil_Card_AddUpdate_Description_None");
        }
    }

    [RelayCommand]
    private async Task AddWindowsUpdate()
    {
        var addedStep = false;
        try
        {
            AddWindowsUpdateCard.IsComplete = false;
            AddWindowsUpdateCard.HasFailed = false;

            if (string.IsNullOrEmpty(WorkingDirectory))
            {
                await _dialogService.ShowWarningAsync(
                    _localizationService.GetString("WIMUtil_Msg_WorkingDirectoryRequired"),
                    _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
                return;
            }

            if (_offer is not { } update)
                return;

            AddWindowsUpdateCard.IsProcessing = true;
            AddWindowsUpdateCard.IsEnabled = false;

            _taskProgressService.StartTask(_localizationService.GetString("WIMUtil_Status_AddingUpdate"), true);
            var progress = _taskProgressService.CreateDetailedProgress();

            var result = await _updateStager.StageAsync(
                WorkingDirectory, update, progress,
                _taskProgressService.CurrentTaskCancellationSource!.Token);

            AddWindowsUpdateCard.IsProcessing = false;
            AddWindowsUpdateCard.IsEnabled = true;

            if (result == UpdateStageResult.Staged)
            {
                var step = await _wimCustomizationService.EnsureUpdateInstallStepAsync(WorkingDirectory);
                AddWindowsUpdateCard.IsComplete = true;
                IsUpdateAdded = true;
                ShowDescription();
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetString(IsBuiltIntoImage ? "WIMUtil_Msg_UpdateAdded_Image" : "WIMUtil_Msg_UpdateAdded"),
                    _localizationService.GetStringOrDefault("Dialog_Success", "Success"));
                addedStep = step is UpdateInstallStepResult.Added or UpdateInstallStepResult.CreatedXml;
            }
            else
            {
                AddWindowsUpdateCard.HasFailed = true;
                IsUpdateAdded = false;
                var messageKey = result switch
                {
                    UpdateStageResult.HashMismatch => "WIMUtil_Msg_UpdateHashMismatch",
                    UpdateStageResult.SignatureInvalid => "WIMUtil_Msg_UpdateSignatureInvalid",
                    _ => "WIMUtil_Msg_UpdateDownloadFailed"
                };
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString(messageKey),
                    _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
            }
        }
        catch (OperationCanceledException)
        {
            AddWindowsUpdateCard.IsProcessing = false;
            AddWindowsUpdateCard.IsEnabled = true;
            IsUpdateAdded = false;
        }
        catch (Exception ex)
        {
            _taskProgressService.FailTask();
            _logService.LogError($"Error adding the Windows update: {ex.Message}", ex);
            AddWindowsUpdateCard.IsProcessing = false;
            AddWindowsUpdateCard.IsEnabled = true;
            AddWindowsUpdateCard.HasFailed = true;
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_UpdateError"), ex.Message),
                _localizationService.GetStringOrDefault("Dialog_Error", "Error"));
        }
        finally
        {
            _taskProgressService.CompleteTask();
        }

        if (addedStep)
        {
            await _checkState.RecheckAfterChangeAsync(
                _fileSystemService.CombinePath(WorkingDirectory, "autounattend.xml"),
                _answerFileValidator, _dialogService, _localizationService, _logService);
        }
    }
}
