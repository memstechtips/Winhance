using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.WimUtil.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.Features.WimUtil.ViewModels;

public partial class WimUtilViewModel : ObservableObject, IDisposable
{
    private readonly ILocalizationService _localizationService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IOfflineServicingService _offlineServicingService;
    private bool _disposed;

    // Steps 2-5 auto-expand once per completed extraction; the flag stops a
    // later refresh from re-opening steps the user has manually collapsed.
    private bool _autoExpandApplied;

    public WimStep1ViewModel Step1 { get; }
    public WimImageFormatViewModel ImageFormat { get; }
    public WimStep2XmlViewModel Step2 { get; }
    public WimStep3DriversViewModel Step3 { get; }
    public WimStep4UpdatesViewModel Step4 { get; }
    public WimStep5IsoViewModel Step5 { get; }

    public AnswerFileCheckState AnswerFileCheck { get; } = new();

    [ObservableProperty]
    public partial WizardStepState Step1State { get; set; }

    [ObservableProperty]
    public partial WizardStepState Step2State { get; set; }

    [ObservableProperty]
    public partial WizardStepState Step3State { get; set; }

    [ObservableProperty]
    public partial WizardStepState Step4State { get; set; }

    [ObservableProperty]
    public partial WizardStepState Step5State { get; set; }

    public string Title => _localizationService.GetStringOrDefault("WIMUtil_Title", "Windows Installation Media Utility");

    public string PageDescription => _localizationService.GetStringOrDefault("WIMUtil_Subtitle", "Create Custom Windows Installation Media");
    public string CheckboxExtractedAlreadyText => _localizationService.GetString("WIMUtil_CheckboxExtractedAlready");
    public string ButtonSelectFolderText => _localizationService.GetString("WIMUtil_ButtonSelectFolder");
    public string ButtonStartExtractionText => _localizationService.GetString("WIMUtil_ButtonStartExtraction");
    public string OptionalConvertText => _localizationService.GetString("WIMUtil_OptionalConvert");
    public string BothImagesTitle => _localizationService.GetString("WIMUtil_Card_BothImages_Title");
    public string BothImagesDescription => _localizationService.GetString("WIMUtil_Card_BothImages_Description");
    public string ButtonDeleteWimText => _localizationService.GetString("WIMUtil_Button_DeleteWim");
    public string ButtonDeleteEsdText => _localizationService.GetString("WIMUtil_Button_DeleteEsd");
    public string DownloadIsoText => _localizationService.GetString("WIMUtil_DownloadISO");
    public string ButtonWindows10Text => _localizationService.GetString("WIMUtil_ButtonWindows10");
    public string ButtonWindows11Text => _localizationService.GetString("WIMUtil_ButtonWindows11");
    public string TooltipDownloadWindows10 => _localizationService.GetString("WIMUtil_Tooltip_DownloadWindows10");
    public string TooltipDownloadWindows11 => _localizationService.GetString("WIMUtil_Tooltip_DownloadWindows11");
    public string SelectOneOptionText => _localizationService.GetString("WIMUtil_SelectOneOption");
    public string ButtonGenerateText => _localizationService.GetString("WIMUtil_ButtonGenerate");
    public string GenerateXmlFilesText => _localizationService.GetString("WIMUtil_GenerateXMLFiles");
    public string ButtonSchneegansText => _localizationService.GetString("WIMUtil_ButtonSchneegans");
    public string TooltipSchneegans => _localizationService.GetString("WIMUtil_Tooltip_Schneegans");
    public string ButtonCreateMediaText => _localizationService.GetString(
        Step5.IsUsbDestination ? "WIMUtil_ButtonWriteUsb" : "WIMUtil_ButtonCreateISO");

    public string DestinationCardTitle => _localizationService.GetString("WIMUtil_Card_Destination_Title");

    public string DestinationCardDescription => _localizationService.GetString("WIMUtil_Card_Destination_Description");

    public string DestinationIsoText => _localizationService.GetString("WIMUtil_Destination_Iso");

    public string DestinationUsbText => _localizationService.GetString("WIMUtil_Destination_Usb");

    public string NoUsbSelectedText => _localizationService.GetString("WIMUtil_Label_NoUsbSelected");

    public string UsbEraseWarningText => _localizationService.GetString("WIMUtil_Msg_UsbEraseWarning");

    public string ShowUsbHardDrivesText => _localizationService.GetString("WIMUtil_CheckboxShowUsbHardDrives");

    public string ShowUsbHardDrivesWarningText => _localizationService.GetString("WIMUtil_Msg_ShowUsbHardDrivesWarning");

    public string BootManagerCardTitle => _localizationService.GetString("WIMUtil_Card_BootManager_Title");

    public string BootManagerOutdatedText => _localizationService.GetString("WIMUtil_Msg_BootManagerOutdated");

    public string ServicingCardTitle => _localizationService.GetString("WIMUtil_Card_Servicing_Title");

    public string ServicingCardDescription
    {
        get
        {
            var key = IsBuiltIntoImage ? "WIMUtil_Servicing_BuiltIn_Detail" : "WIMUtil_Card_Servicing_Description";
            return _localizationService.GetString(key);
        }
    }

    public string EditionCardTitle => _localizationService.GetString("WIMUtil_Card_Edition_Title");

    public string EditionCardDescription
    {
        get
        {
            var key = ImageFormat.IsImageReadyForServicing ? "WIMUtil_Card_Edition_Description_Ready" : "WIMUtil_Card_Edition_Description";
            return _localizationService.GetString(key);
        }
    }

    public string PrepareServicingButtonText => _localizationService.GetString("WIMUtil_Card_PrepareServicing_Button");

    public string HostDismWarningText => _localizationService.GetString("WIMUtil_Msg_HostDismOlder");
    public string MountFolderWarningText => _localizationService.GetString("WIMUtil_Msg_MountFolderUnsupported");

    public string ServicingTimeText => _localizationService.GetString("WIMUtil_Info_ServicingTime");

    public bool ShowServicingTimeNote => IsBuiltIntoImage && _offlineServicingService.HasStagedItems(WorkingDirectory);

    // Forwarders so existing XAML can keep binding to ViewModel.PropertyName instead of the sub-VMs.
    public string WorkingDirectory => Step1.WorkingDirectory;
    public bool CanStartExtraction => Step1.CanStartExtraction;
    public bool HasExtractedIsoAlready { get => Step1.HasExtractedIsoAlready; set => Step1.HasExtractedIsoAlready = value; }

    public WizardActionCard SelectIsoCard => Step1.SelectIsoCard;
    public WizardActionCard SelectDirectoryCard => Step1.SelectDirectoryCard;

    public bool ShowConversionCard => ImageFormat.ShowConversionCard;
    public bool BothFormatsExist => ImageFormat.BothFormatsExist;
    public string WimFileSize => ImageFormat.WimFileSize;
    public string EsdFileSize => ImageFormat.EsdFileSize;
    public WizardActionCard ConvertImageCard => ImageFormat.ConvertImageCard;

    public ObservableCollection<ServicingChoice> ServicingChoices => ImageFormat.ServicingChoices;

    public ServicingChoice? SelectedServicing
    {
        get => ImageFormat.SelectedServicing;
        set => ImageFormat.SelectedServicing = value;
    }

    public ObservableCollection<ImageEdition> EditionChoices => ImageFormat.EditionChoices;

    public ImageEdition? SelectedEdition
    {
        get => ImageFormat.SelectedEdition;
        set => ImageFormat.SelectedEdition = value;
    }

    public bool IsBuiltIntoImage => ImageFormat.IsBuiltIntoImage;
    public bool ShowHostDismWarning => ImageFormat.ShowHostDismWarning;
    public bool ShowMountFolderWarning => ImageFormat.ShowMountFolderWarning;
    public bool ShowEditionCard => IsBuiltIntoImage && ShowConversionCard;
    public bool ShowConvertImageCard => !IsBuiltIntoImage && !BothFormatsExist;
    public bool ShowOptionalConvertText => ShowConvertImageCard || BothFormatsExist;
    public bool CanChangeServicing => !IsImageBeingRewritten && !Step5.IsCreatingMedia;

    public WizardActionCard GenerateWinhanceXmlCard => Step2.GenerateWinhanceXmlCard;
    public WizardActionCard DownloadXmlCard => Step2.DownloadXmlCard;
    public WizardActionCard SelectXmlCard => Step2.SelectXmlCard;
    public bool IsAutounattendSession => Step2.IsAutounattendSession;
    public string StartButtonText => Step2.StartButtonText;

    public bool AnswerFileBannerOpen => AnswerFileCheck.LastReport is { Findings.Count: > 0 };

    public InfoBarSeverity AnswerFileBannerSeverity =>
        AnswerFileCheck.LastReport?.Findings.Any(f => f.Severity == AnswerFileSeverity.Error) == true
            ? InfoBarSeverity.Error
            : InfoBarSeverity.Warning;

    public string AnswerFileBannerTitle => AnswerFileCheck.LastReport is { Findings.Count: > 0 } report
        ? AnswerFileReportDialog.Verdict(report, _localizationService)
        : string.Empty;

    public string AnswerFileBannerSummary => AnswerFileCheck.LastReport is { Findings.Count: > 0 } report
        ? AnswerFileReportDialog.Summary(report, _localizationService)
        : string.Empty;

    public string AnswerFileBannerMessage => AnswerFileCheck.LastReport is { Findings.Count: > 0 } report
        ? string.Join(Environment.NewLine + Environment.NewLine, AnswerFileReportDialog.Items(report, _localizationService))
        : string.Empty;

    public WizardActionCard ExtractSystemDriversCard => Step3.ExtractSystemDriversCard;
    public WizardActionCard SelectCustomDriversCard => Step3.SelectCustomDriversCard;

    public WizardActionCard AddWindowsUpdateCard => Step4.AddWindowsUpdateCard;

    public bool IsIsoDestination => Step5.IsIsoDestination;
    public bool IsUsbDestination => Step5.IsUsbDestination;
    public WizardActionCard SelectOutputCard => Step5.SelectOutputCard;
    public WizardActionCard SelectUsbCard => Step5.SelectUsbCard;
    public ObservableCollection<RemovableDrive> UsbTargets => Step5.UsbTargets;

    public bool ShowUsbHardDrives
    {
        get => Step5.ShowUsbHardDrives;
        set => Step5.ShowUsbHardDrives = value;
    }

    public RemovableDrive? SelectedUsbTarget
    {
        get => Step5.SelectedUsbTarget;
        set => Step5.SelectedUsbTarget = value;
    }

    public ObservableCollection<BootManagerChoice> BootManagerChoices => Step5.BootManagerChoices;

    public BootManagerChoice? SelectedBootManager
    {
        get => Step5.SelectedBootManager;
        set => Step5.SelectedBootManager = value;
    }

    public bool ShowBootManagerChoice => Step5.ShowBootManagerChoice;
    public string BootManagerDescription => Step5.BootManagerDescription;
    public bool IsBootManagerOutdated => Step5.IsBootManagerOutdated;

    public IAsyncRelayCommand SelectWorkingDirectoryCommand => Step1.SelectWorkingDirectoryCommand;
    public IAsyncRelayCommand StartIsoExtractionCommand => Step1.StartIsoExtractionCommand;
    public IAsyncRelayCommand OpenWindows10DownloadCommand => Step1.OpenWindows10DownloadCommand;
    public IAsyncRelayCommand OpenWindows11DownloadCommand => Step1.OpenWindows11DownloadCommand;

    public IAsyncRelayCommand DeleteWimCommand => ImageFormat.DeleteWimCommand;
    public IAsyncRelayCommand DeleteEsdCommand => ImageFormat.DeleteEsdCommand;
    public IAsyncRelayCommand PrepareImageForServicingCommand => ImageFormat.PrepareImageForServicingCommand;

    public IAsyncRelayCommand GenerateWinhanceXmlCommand => Step2.GenerateWinhanceXmlCommand;
    public IAsyncRelayCommand StartAutounattendSessionCommand => Step2.StartAutounattendSessionCommand;
    public IAsyncRelayCommand OpenSchneegansXmlGeneratorCommand => Step2.OpenSchneegansXmlGeneratorCommand;

    public IAsyncRelayCommand CreateMediaCommand => Step5.CreateMediaCommand;
    public IRelayCommand SelectIsoDestinationCommand => Step5.SelectIsoDestinationCommand;
    public IAsyncRelayCommand SelectUsbDestinationCommand => Step5.SelectUsbDestinationCommand;

    public WimUtilViewModel(
        IIsoService isoService,
        IUsbMediaWriter usbMediaWriter,
        IBootManagerService bootManagerService,
        IWimImageService wimImageService,
        IWimCustomizationService wimCustomizationService,
        ITaskProgressService taskProgressService,
        IDialogService dialogService,
        ILogService logService,
        ISelectionSaveService saves,
        ISelectionSetBuilder selections,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IProcessExecutor processExecutor,
        IFileSystemService fileSystemService,
        IFilePickerService filePickerService,
        IResourceService resourceService,
        IAnswerFileValidator answerFileValidator,
        IAutounattendReader autounattendReader,
        IApplicationModeService applicationModeService,
        IBuilderModeEntry builderModeEntry,
        IEventBus eventBus,
        WimUtilSession session,
        ITargetWindowsService targetWindows,
        IWindowsUpdateListService updateListService,
        IWindowsUpdateStager updateStager,
        IOfflineServicingService offlineServicingService)
    {
        _localizationService = localizationService;
        _fileSystemService = fileSystemService;
        _offlineServicingService = offlineServicingService;

        Step1 = new WimStep1ViewModel(
            isoService, taskProgressService, dialogService,
            localizationService, fileSystemService, filePickerService, logService,
            resourceService);

        ImageFormat = new WimImageFormatViewModel(
            wimImageService, taskProgressService, dialogService,
            dispatcherService, localizationService, logService, fileSystemService, autounattendReader, offlineServicingService);

        Step2 = new WimStep2XmlViewModel(
            saves, wimCustomizationService, selections,
            dialogService, localizationService, fileSystemService, filePickerService, logService,
            resourceService, answerFileValidator, autounattendReader, applicationModeService, builderModeEntry, dispatcherService,
            eventBus, AnswerFileCheck, session, wimImageService, targetWindows, taskProgressService);

        Step3 = new WimStep3DriversViewModel(
            wimCustomizationService, taskProgressService, dialogService,
            localizationService, fileSystemService, filePickerService, logService,
            resourceService, answerFileValidator, AnswerFileCheck);

        Step4 = new WimStep4UpdatesViewModel(
            wimCustomizationService, taskProgressService, dialogService,
            localizationService, fileSystemService, logService,
            resourceService, answerFileValidator, AnswerFileCheck,
            updateListService, updateStager);

        Step5 = new WimStep5IsoViewModel(
            wimCustomizationService, isoService, usbMediaWriter, bootManagerService, taskProgressService, processExecutor,
            dialogService, localizationService, fileSystemService, filePickerService, logService, answerFileValidator, AnswerFileCheck,
            offlineServicingService);

        Step1State = new WizardStepState();
        Step2State = new WizardStepState();
        Step3State = new WizardStepState();
        Step4State = new WizardStepState();
        Step5State = new WizardStepState();

        InitializeStepStates();

        _localizationService.LanguageChanged += OnLanguageChanged;

        Step1.PropertyChanged += OnSubViewModelPropertyChanged;
        ImageFormat.PropertyChanged += OnSubViewModelPropertyChanged;
        Step2.PropertyChanged += OnSubViewModelPropertyChanged;
        Step3.PropertyChanged += OnSubViewModelPropertyChanged;
        Step4.PropertyChanged += OnSubViewModelPropertyChanged;
        Step5.PropertyChanged += OnSubViewModelPropertyChanged;
        Step5.ImageServiced += OnImageServiced;
        Step1.MediaReady += OnMediaReady;
        AnswerFileCheck.PropertyChanged += OnAnswerFileCheckChanged;

        // Propagate Step1's initial WorkingDirectory to sub-VMs.
        // The constructor assignment above fires before subscriptions,
        // so without this, sub-VMs never receive the default value.
        var initialWorkingDir = Step1.WorkingDirectory;
        ImageFormat.WorkingDirectory = initialWorkingDir;
        Step2.WorkingDirectory = initialWorkingDir;
        Step3.WorkingDirectory = initialWorkingDir;
        Step4.WorkingDirectory = initialWorkingDir;
        Step5.WorkingDirectory = initialWorkingDir;
        PushServicingMode();
    }

    public void OnNavigatedTo()
    {
        UpdateStepStates();
        RefreshStepExpansion();
    }

    [RelayCommand]
    private void NavigateToStep(string? stepParameter)
    {
        if (string.IsNullOrEmpty(stepParameter) || !int.TryParse(stepParameter, out int targetStep)) return;

        var state = GetStepState(targetStep);
        if (state is null || !IsStepAvailable(targetStep)) return;

        // Steps toggle independently — collapsing one leaves the others as-is.
        state.IsExpanded = !state.IsExpanded;
    }

    private WizardStepState? GetStepState(int step) => step switch
    {
        1 => Step1State,
        2 => Step2State,
        3 => Step3State,
        4 => Step4State,
        5 => Step5State,
        _ => null
    };

    private bool IsStepAvailable(int step) => step switch
    {
        1 => !IsImageLocked,
        2 or 3 or 4 or 5 => Step1.IsExtractionComplete && !IsImageBeingRewritten,
        _ => false
    };

    private bool IsImageBeingRewritten => ImageFormat.IsConverting || Step2.IsRemovingEditions;

    // Removing editions rewrites install.wim and creating media reads or services it, so Step 1 cannot touch it meanwhile.
    private bool IsImageLocked => Step2.IsRemovingEditions || Step5.IsCreatingMedia;

    private void InitializeStepStates()
    {
        Step1State = new WizardStepState
        {
            StepNumber = 1,
            Title = _localizationService.GetStringOrDefault("WIMUtil_Step1_Title", "Select Windows ISO"),
            Icon = "DiscPlayer",
            StatusText = _localizationService.GetString("WIMUtil_Status_NoIsoSelected"),
            IsExpanded = true,
            IsAvailable = true
        };

        Step2State = new WizardStepState
        {
            StepNumber = 2,
            Title = _localizationService.GetStringOrDefault("WIMUtil_Step2_Title", "Add XML File"),
            Icon = "FileCode",
            StatusText = _localizationService.GetString("WIMUtil_Status_CompleteStep1")
        };

        Step3State = new WizardStepState
        {
            StepNumber = 3,
            Title = _localizationService.GetStringOrDefault("WIMUtil_Step3_Title", "Extract and Add Drivers"),
            Icon = "Chip",
            StatusText = _localizationService.GetString("WIMUtil_Status_CompleteStep1")
        };

        Step4State = new WizardStepState
        {
            StepNumber = 4,
            Title = _localizationService.GetStringOrDefault("WIMUtil_Step4_Title", "Add Windows Update"),
            Icon = "ArrowSync",
            StatusText = _localizationService.GetString("WIMUtil_Status_CompleteStep1")
        };

        Step5State = new WizardStepState
        {
            StepNumber = 5,
            Title = _localizationService.GetStringOrDefault("WIMUtil_Step5_Title", "Create New ISO"),
            Icon = "WrenchClock",
            StatusText = _localizationService.GetString("WIMUtil_Status_CompleteStep1")
        };
    }

    private void UpdateStepStates()
    {
        var extractionComplete = Step1.IsExtractionComplete;
        var isConverting = ImageFormat.IsConverting;
        var imageBusy = IsImageBeingRewritten;
        var busyStatus = isConverting
            ? _localizationService.GetString("WIMUtil_Status_WaitForConversion")
            : _localizationService.GetString("WIMUtil_Status_RemovingEditions");
        var isExtracting = Step1.IsExtracting;
        var driversAddedKey = IsBuiltIntoImage ? "WIMUtil_Status_DriversReadyForImage" : "WIMUtil_Status_DriversAdded";
        var updateAddedKey = IsBuiltIntoImage ? "WIMUtil_Status_UpdateReadyForImage" : "WIMUtil_Status_UpdateAdded";

        Step1State.IsAvailable = !IsImageLocked;
        Step1State.IsComplete = extractionComplete && !imageBusy;
        Step1State.StatusText = isConverting
            ? _localizationService.GetString("WIMUtil_Status_Converting")
            : imageBusy
                ? busyStatus
                : extractionComplete
                    ? _localizationService.GetString("WIMUtil_Status_IsoExtracted")
                    : isExtracting
                        ? _localizationService.GetString("WIMUtil_Status_Extracting")
                        : !string.IsNullOrEmpty(Step1.SelectedIsoPath)
                            ? _localizationService.GetString("WIMUtil_Status_IsoSelected")
                            : _localizationService.GetString("WIMUtil_Status_NoIsoSelected");

        Step2State.IsAvailable = extractionComplete && !imageBusy;
        Step2State.IsComplete = Step2.IsXmlAdded;
        Step2State.StatusText = imageBusy
            ? busyStatus
            : !extractionComplete
                ? _localizationService.GetString("WIMUtil_Status_CompleteStep1")
                : Step2.IsXmlAdded
                    ? Step2StatusText()
                    : _localizationService.GetString("WIMUtil_Status_NoXmlAdded");

        Step3State.IsAvailable = extractionComplete && !imageBusy;
        Step3State.IsComplete = Step3.AreDriversAdded;
        Step3State.StatusText = imageBusy
            ? busyStatus
            : !extractionComplete
                ? _localizationService.GetString("WIMUtil_Status_CompleteStep1")
                : Step3.AreDriversAdded
                    ? _localizationService.GetString(driversAddedKey)
                    : _localizationService.GetString("WIMUtil_Status_NoDriversAdded");

        Step4State.IsAvailable = extractionComplete && !imageBusy;
        Step4State.IsComplete = Step4.IsUpdateAdded;
        Step4State.StatusText = imageBusy
            ? busyStatus
            : !extractionComplete
                ? _localizationService.GetString("WIMUtil_Status_CompleteStep1")
                : Step4.IsUpdateAdded
                    ? _localizationService.GetString(updateAddedKey)
                    : _localizationService.GetString("WIMUtil_Status_NoUpdateAdded");

        Step5State.IsAvailable = extractionComplete && !imageBusy;
        Step5State.IsComplete = Step5.IsIsoCreated;
        Step5State.StatusText = imageBusy
            ? busyStatus
            : !extractionComplete
                ? _localizationService.GetString("WIMUtil_Status_CompleteStep1")
                : Step5.IsIsoCreated
                    ? _localizationService.GetString("WIMUtil_Status_IsoCreated")
                    : !string.IsNullOrEmpty(Step5.OutputIsoPath)
                        ? $"{_localizationService.GetString("WIMUtil_Label_Output")}: {_fileSystemService.GetFileName(Step5.OutputIsoPath)}"
                        : _localizationService.GetString("WIMUtil_Status_ReadyToCreateIso");

        OnPropertyChanged(nameof(Step1State));
        OnPropertyChanged(nameof(Step2State));
        OnPropertyChanged(nameof(Step3State));
        OnPropertyChanged(nameof(Step4State));
        OnPropertyChanged(nameof(Step5State));
    }

    // Steps 3, 4 and 5 re-check the same file, so the header reads the shared verdict rather than
    // step 2's own wording -- but only when the report is about the copy on the media. A refused
    // file's report describes something Setup will never see; it belongs to the banner alone.
    private string Step2StatusText() =>
        AnswerFileCheck.LastReport is { } report
        && AnswerFileCheck.Subject == _fileSystemService.CombinePath(WorkingDirectory, "autounattend.xml")
            ? AnswerFileReportDialog.Verdict(report, _localizationService)
            : Step2.XmlStatus;

    // Once the ISO is extracted, steps 2-5 expand so the user sees every remaining task; step 1 stays as
    // is. Applied once per completed extraction; restarting extraction resets it.
    private void RefreshStepExpansion()
    {
        if (Step1.IsExtractionComplete)
        {
            if (_autoExpandApplied) return;
            _autoExpandApplied = true;

            Step2State.IsExpanded = true;
            Step3State.IsExpanded = true;
            Step4State.IsExpanded = true;
            Step5State.IsExpanded = true;
        }
        else
        {
            _autoExpandApplied = false;

            Step1State.IsExpanded = true;
            Step2State.IsExpanded = false;
            Step3State.IsExpanded = false;
            Step4State.IsExpanded = false;
            Step5State.IsExpanded = false;
        }
    }

    private void OnMediaReady(object? sender, EventArgs e) => _ = Step2.PickUpMediaAnswerFileAsync();

    private void OnSubViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == Step1)
        {
            switch (e.PropertyName)
            {
                case nameof(WimStep1ViewModel.WorkingDirectory):
                    var wd = Step1.WorkingDirectory;
                    ImageFormat.WorkingDirectory = wd;
                    Step2.WorkingDirectory = wd;
                    Step3.WorkingDirectory = wd;
                    Step4.WorkingDirectory = wd;
                    Step5.WorkingDirectory = wd;
                    PushServicingMode();
                    // The old report described a file in the previous working directory.
                    AnswerFileCheck.Publish(null, null);
                    break;

                case nameof(WimStep1ViewModel.IsExtractionComplete):
                    if (Step1.IsExtractionComplete)
                    {
                        _ = ImageFormat.SafeDetectImageFormatAsync();
                        _ = Step5.InspectBootManagerAsync();
                    }
                    RefreshStepExpansion();
                    break;
            }
        }

        if (sender == Step5 && e.PropertyName == nameof(WimStep5IsoViewModel.IsCreatingMedia))
            ImageFormat.IsImageLocked = IsImageLocked;

        if (sender == Step2 && e.PropertyName == nameof(WimStep2XmlViewModel.IsRemovingEditions))
        {
            ImageFormat.IsImageLocked = IsImageLocked;

            // The convert card names the image's size, which removing editions changes.
            if (!Step2.IsRemovingEditions)
                _ = ImageFormat.SafeDetectImageFormatAsync();
        }

        ForwardPropertyChange(sender, e);

        UpdateStepStates();
    }

    private void ForwardPropertyChange(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == Step1)
        {
            switch (e.PropertyName)
            {
                case nameof(WimStep1ViewModel.WorkingDirectory):
                    OnPropertyChanged(nameof(WorkingDirectory));
                    OnPropertyChanged(nameof(ShowServicingTimeNote));
                    break;
                case nameof(WimStep1ViewModel.CanStartExtraction): OnPropertyChanged(nameof(CanStartExtraction)); break;
                case nameof(WimStep1ViewModel.HasExtractedIsoAlready): OnPropertyChanged(nameof(HasExtractedIsoAlready)); break;
            }
        }
        else if (sender == ImageFormat)
        {
            switch (e.PropertyName)
            {
                case nameof(WimImageFormatViewModel.CurrentImageFormat):
                    OnPropertyChanged(nameof(EditionChoices));
                    _ = Step4.SetMediaAsync(ImageFormat.CurrentImageFormat);
                    break;
                case nameof(WimImageFormatViewModel.ShowConversionCard):
                    OnPropertyChanged(nameof(ShowConversionCard));
                    OnPropertyChanged(nameof(ShowEditionCard));
                    break;
                case nameof(WimImageFormatViewModel.IsConverting): OnPropertyChanged(nameof(CanChangeServicing)); break;
                case nameof(WimImageFormatViewModel.BothFormatsExist):
                    OnPropertyChanged(nameof(BothFormatsExist));
                    OnPropertyChanged(nameof(ShowConvertImageCard));
                    OnPropertyChanged(nameof(ShowOptionalConvertText));
                    break;
                case nameof(WimImageFormatViewModel.SelectedServicing): OnPropertyChanged(nameof(SelectedServicing)); break;
                case nameof(WimImageFormatViewModel.SelectedEdition): OnPropertyChanged(nameof(SelectedEdition)); break;
                case nameof(WimImageFormatViewModel.ShowHostDismWarning): OnPropertyChanged(nameof(ShowHostDismWarning)); break;
                case nameof(WimImageFormatViewModel.ShowMountFolderWarning): OnPropertyChanged(nameof(ShowMountFolderWarning)); break;
                case nameof(WimImageFormatViewModel.IsBuiltIntoImage):
                    PushServicingMode();
                    OnPropertyChanged(nameof(IsBuiltIntoImage));
                    OnPropertyChanged(nameof(ShowEditionCard));
                    OnPropertyChanged(nameof(ShowConvertImageCard));
                    OnPropertyChanged(nameof(ShowOptionalConvertText));
                    OnPropertyChanged(nameof(ServicingCardDescription));
                    OnPropertyChanged(nameof(ShowServicingTimeNote));
                    break;
                case nameof(WimImageFormatViewModel.IsImageReadyForServicing):
                    PushServicingMode();
                    OnPropertyChanged(nameof(EditionCardDescription));
                    break;
                case nameof(WimImageFormatViewModel.WimFileSize): OnPropertyChanged(nameof(WimFileSize)); break;
                case nameof(WimImageFormatViewModel.EsdFileSize): OnPropertyChanged(nameof(EsdFileSize)); break;
            }
        }
        else if (sender == Step2)
        {
            switch (e.PropertyName)
            {
                case nameof(WimStep2XmlViewModel.IsAutounattendSession): OnPropertyChanged(nameof(IsAutounattendSession)); break;
                case nameof(WimStep2XmlViewModel.IsRemovingEditions): OnPropertyChanged(nameof(CanChangeServicing)); break;
            }
        }
        else if (sender == Step3)
        {
            switch (e.PropertyName)
            {
                case nameof(WimStep3DriversViewModel.AreDriversAdded): OnPropertyChanged(nameof(ShowServicingTimeNote)); break;
            }
        }
        else if (sender == Step4)
        {
            switch (e.PropertyName)
            {
                case nameof(WimStep4UpdatesViewModel.IsUpdateAdded): OnPropertyChanged(nameof(ShowServicingTimeNote)); break;
            }
        }
        else if (sender == Step5)
        {
            switch (e.PropertyName)
            {
                case nameof(WimStep5IsoViewModel.SelectedUsbTarget): OnPropertyChanged(nameof(SelectedUsbTarget)); break;
                case nameof(WimStep5IsoViewModel.ShowUsbHardDrives): OnPropertyChanged(nameof(ShowUsbHardDrives)); break;
                case nameof(WimStep5IsoViewModel.SelectedBootManager): OnPropertyChanged(nameof(SelectedBootManager)); break;
                case nameof(WimStep5IsoViewModel.ShowBootManagerChoice): OnPropertyChanged(nameof(ShowBootManagerChoice)); break;
                case nameof(WimStep5IsoViewModel.BootManagerDescription): OnPropertyChanged(nameof(BootManagerDescription)); break;
                case nameof(WimStep5IsoViewModel.IsBootManagerOutdated): OnPropertyChanged(nameof(IsBootManagerOutdated)); break;
                case nameof(WimStep5IsoViewModel.IsCreatingMedia): OnPropertyChanged(nameof(CanChangeServicing)); break;
                case nameof(WimStep5IsoViewModel.IsUsbDestination):
                    OnPropertyChanged(nameof(IsIsoDestination));
                    OnPropertyChanged(nameof(IsUsbDestination));
                    OnPropertyChanged(nameof(ButtonCreateMediaText));
                    break;
            }
        }
    }

    // Steps 2-5 read the Step 1 mode; Step 5 also needs to know the image is in a shape it can service.
    private void PushServicingMode()
    {
        var builtIn = ImageFormat.IsBuiltIntoImage;
        Step2.IsBuiltIntoImage = builtIn;
        Step3.IsBuiltIntoImage = builtIn;
        Step4.IsBuiltIntoImage = builtIn;
        Step5.IsBuiltIntoImage = builtIn;
        Step5.IsReadyForServicing = ImageFormat.IsImageReadyForServicing;
    }

    // Servicing tidied what it built in out of the media folder, so Steps 3 and 4 no longer hold it.
    private void OnImageServiced(object? sender, OfflineServicingResult result)
    {
        Step3.RefreshDriversAdded();
        Step4.ShowUpdateState();
        OnPropertyChanged(nameof(ShowServicingTimeNote));
    }

    private void OnAnswerFileCheckChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateStepStates();
        OnPropertyChanged(nameof(AnswerFileBannerOpen));
        OnPropertyChanged(nameof(AnswerFileBannerSeverity));
        OnPropertyChanged(nameof(AnswerFileBannerTitle));
        OnPropertyChanged(nameof(AnswerFileBannerSummary));
        OnPropertyChanged(nameof(AnswerFileBannerMessage));
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Step1State.Title = _localizationService.GetStringOrDefault("WIMUtil_Step1_Title", "Select Windows ISO");
        Step2State.Title = _localizationService.GetStringOrDefault("WIMUtil_Step2_Title", "Add XML File");
        Step3State.Title = _localizationService.GetStringOrDefault("WIMUtil_Step3_Title", "Extract and Add Drivers");
        Step4State.Title = _localizationService.GetStringOrDefault("WIMUtil_Step4_Title", "Add Windows Update");
        Step5State.Title = _localizationService.GetStringOrDefault("WIMUtil_Step5_Title", "Create New ISO");
        ImageFormat.RefreshLocalizedText();
        UpdateStepStates();

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(PageDescription));
        OnPropertyChanged(nameof(CheckboxExtractedAlreadyText));
        OnPropertyChanged(nameof(ButtonSelectFolderText));
        OnPropertyChanged(nameof(ButtonStartExtractionText));
        OnPropertyChanged(nameof(OptionalConvertText));
        OnPropertyChanged(nameof(BothImagesTitle));
        OnPropertyChanged(nameof(BothImagesDescription));
        OnPropertyChanged(nameof(ButtonDeleteWimText));
        OnPropertyChanged(nameof(ButtonDeleteEsdText));
        OnPropertyChanged(nameof(DownloadIsoText));
        OnPropertyChanged(nameof(ButtonWindows10Text));
        OnPropertyChanged(nameof(ButtonWindows11Text));
        OnPropertyChanged(nameof(TooltipDownloadWindows10));
        OnPropertyChanged(nameof(TooltipDownloadWindows11));
        OnPropertyChanged(nameof(SelectOneOptionText));
        OnPropertyChanged(nameof(ButtonGenerateText));
        OnPropertyChanged(nameof(StartButtonText));
        OnPropertyChanged(nameof(GenerateXmlFilesText));
        OnPropertyChanged(nameof(ButtonSchneegansText));
        OnPropertyChanged(nameof(TooltipSchneegans));
        OnPropertyChanged(nameof(ButtonCreateMediaText));
        OnPropertyChanged(nameof(DestinationCardTitle));
        OnPropertyChanged(nameof(DestinationCardDescription));
        OnPropertyChanged(nameof(DestinationIsoText));
        OnPropertyChanged(nameof(DestinationUsbText));
        OnPropertyChanged(nameof(NoUsbSelectedText));
        OnPropertyChanged(nameof(UsbEraseWarningText));
        OnPropertyChanged(nameof(ShowUsbHardDrivesText));
        OnPropertyChanged(nameof(ShowUsbHardDrivesWarningText));
        OnPropertyChanged(nameof(BootManagerCardTitle));
        OnPropertyChanged(nameof(BootManagerOutdatedText));
        OnPropertyChanged(nameof(ServicingCardTitle));
        OnPropertyChanged(nameof(ServicingCardDescription));
        OnPropertyChanged(nameof(EditionCardTitle));
        OnPropertyChanged(nameof(EditionCardDescription));
        OnPropertyChanged(nameof(PrepareServicingButtonText));
        OnPropertyChanged(nameof(HostDismWarningText));
        OnPropertyChanged(nameof(MountFolderWarningText));
        OnPropertyChanged(nameof(ServicingTimeText));
        OnPropertyChanged(nameof(AnswerFileBannerTitle));
        OnPropertyChanged(nameof(AnswerFileBannerSummary));
        OnPropertyChanged(nameof(AnswerFileBannerMessage));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _localizationService.LanguageChanged -= OnLanguageChanged;

        Step1.PropertyChanged -= OnSubViewModelPropertyChanged;
        ImageFormat.PropertyChanged -= OnSubViewModelPropertyChanged;
        Step2.PropertyChanged -= OnSubViewModelPropertyChanged;
        Step3.PropertyChanged -= OnSubViewModelPropertyChanged;
        Step4.PropertyChanged -= OnSubViewModelPropertyChanged;
        Step5.PropertyChanged -= OnSubViewModelPropertyChanged;
        Step5.ImageServiced -= OnImageServiced;
        Step1.MediaReady -= OnMediaReady;
        AnswerFileCheck.PropertyChanged -= OnAnswerFileCheckChanged;

        (Step2 as IDisposable)?.Dispose();
        (Step3 as IDisposable)?.Dispose();
        (Step4 as IDisposable)?.Dispose();
        (ImageFormat as IDisposable)?.Dispose();
        GC.SuppressFinalize(this);
    }
}
