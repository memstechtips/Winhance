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
    private readonly WimUtilSession _session;
    private bool _disposed;

    // Steps 2-4 auto-expand once per completed extraction; the flag stops a
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

    public IReadOnlyList<WizardStepState> Steps { get; private set; } = [];

    public string StepRailName => _localizationService.GetString("WIMUtil_Rail_Name");

    public string Title => _localizationService.GetStringOrDefault("WIMUtil_Title", "Windows Installation Media Utility");

    public string PageDescription => _localizationService.GetStringOrDefault("WIMUtil_Subtitle", "Create Custom Windows Installation Media");
    public string CheckboxExtractedAlreadyText => _localizationService.GetString("WIMUtil_CheckboxExtractedAlready");
    public string ButtonSelectFolderText => _localizationService.GetString("WIMUtil_ButtonSelectFolder");
    public string ButtonStartExtractionText => _localizationService.GetString("WIMUtil_ButtonStartExtraction");
    public string BothImagesTitle => _localizationService.GetString("WIMUtil_Card_BothImages_Title");
    public string BothImagesDescription => _localizationService.GetString("WIMUtil_Card_BothImages_Description");
    public string ButtonDeleteWimText => _localizationService.GetString("WIMUtil_Button_DeleteWim");
    public string ButtonDeleteEsdText => _localizationService.GetString("WIMUtil_Button_DeleteEsd");
    public string DownloadIsoText => _localizationService.GetString("WIMUtil_DownloadISO");
    public string ButtonWindows10Text => _localizationService.GetString("WIMUtil_ButtonWindows10");
    public string ButtonWindows11Text => _localizationService.GetString("WIMUtil_ButtonWindows11");
    public string TooltipDownloadWindows10 => _localizationService.GetString("WIMUtil_Tooltip_DownloadWindows10");
    public string TooltipDownloadWindows11 => _localizationService.GetString("WIMUtil_Tooltip_DownloadWindows11");
    public string GenerateXmlFilesText => _localizationService.GetString("WIMUtil_GenerateXMLFiles");
    public string ButtonSchneegansText => _localizationService.GetString("WIMUtil_ButtonSchneegans");
    public string TooltipSchneegans => _localizationService.GetString("WIMUtil_Tooltip_Schneegans");
    public string ButtonCreateMediaText => _localizationService.GetString(
        Step5.IsUsbDestination ? "WIMUtil_ButtonWriteUsb" : "WIMUtil_ButtonCreateISO");

    public string ShowBuildDetailsText => _localizationService.GetString("WIMUtil_Build_ShowDetails");

    public string DeleteWorkingFolderText => _localizationService.GetString("WIMUtil_Build_DeleteWorkingFolder");

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

    public string NeedsEditionText => _localizationService.GetString("WIMUtil_Msg_PickEditionToBuild");

    public string ServicingCardTitle => _localizationService.GetString("WIMUtil_Card_Servicing_Title");

    public string DuringSetupTitle => _localizationService.GetString("WIMUtil_Servicing_DuringSetup");

    public string DuringSetupDescription => _localizationService.GetString("WIMUtil_Servicing_DuringSetup_Detail");

    public string BuiltIntoImageTitle => _localizationService.GetString("WIMUtil_Servicing_BuiltIn");

    public string BuiltIntoImageDescription => _localizationService.GetString("WIMUtil_Servicing_BuiltIn_Detail");

    public string YourImageHeadingText => _localizationService.GetString("WIMUtil_Heading_YourImage");

    public string EditionCardTitle => _localizationService.GetString("WIMUtil_Card_Edition_Title");

    public string EditionCardDescription => _localizationService.GetString("WIMUtil_Card_Edition_Description");

    public string MountFolderWarningText => _localizationService.GetString("WIMUtil_Msg_MountFolderUnsupported");

    // Step 2's answer-file choice.
    public string AnswerFileChoiceTitle => _localizationService.GetString("WIMUtil_AnswerFile_Title");
    public string AnswerFileChoiceDescription => _localizationService.GetString("WIMUtil_AnswerFile_Description");
    public string AnswerFileBuilderText => _localizationService.GetString("WIMUtil_AnswerFile_Builder");
    public string AnswerFileOwnXmlText => _localizationService.GetString("WIMUtil_AnswerFile_OwnXml");
    public string AnswerFileBuilderDescription => _localizationService.GetString("WIMUtil_AnswerFile_Builder_Description");
    public string AnswerFileOwnXmlDescription => _localizationService.GetString("WIMUtil_AnswerFile_OwnXml_Description");
    public string MediaAnswerFileText => _localizationService.GetString("WIMUtil_AnswerFile_OnMedia");
    public string DeleteMediaAnswerFileText => _localizationService.GetString("WIMUtil_AnswerFile_DeleteToStartOver");
    public string ChangeFileText => _localizationService.GetString("WIMUtil_AnswerFile_ChangeFile");

    // Step 3's drivers and update, which are ticked now and added when the media is created.
    public string AddSystemDriversText => _localizationService.GetString("WIMUtil_Drivers_System");
    public string AddFolderDriversText => _localizationService.GetString("WIMUtil_Drivers_Folder");
    public string ChooseFolderText => _localizationService.GetString("WIMUtil_Drivers_ChooseFolder");
    public string RemoveDriversFolderText => _localizationService.GetString("WIMUtil_Drivers_RemoveFolder");
    public string AddUpdateText => _localizationService.GetString("WIMUtil_Update_Add");

    // Step 4's image format.
    public string FormatCardTitle => _localizationService.GetString("WIMUtil_Format_Title");
    public string FormatCardDescription => CurrentFormatText() is { } current
        ? current + " " + _localizationService.GetString("WIMUtil_Format_Description")
        : _localizationService.GetString("WIMUtil_Format_Description");
    public string FormatWimText => _localizationService.GetString("WIMUtil_Format_Wim");
    public string FormatEsdText => _localizationService.GetString("WIMUtil_Format_Esd");

    // Forwarders so existing XAML can keep binding to ViewModel.PropertyName instead of the sub-VMs.
    public string WorkingDirectory => Step1.WorkingDirectory;
    public bool CanStartExtraction => Step1.CanStartExtraction;
    public bool ShowWorkingDirectorySpaceWarning => Step1.ShowSpaceWarning;
    public string WorkingDirectorySpaceWarningText => Step1.SpaceWarningText;
    public bool HasExtractedIsoAlready { get => Step1.HasExtractedIsoAlready; set => Step1.HasExtractedIsoAlready = value; }

    public WizardActionCard SelectIsoCard => Step1.SelectIsoCard;
    public WizardActionCard SelectDirectoryCard => Step1.SelectDirectoryCard;

    public bool ShowConversionCard => ImageFormat.ShowConversionCard;
    public bool BothFormatsExist => ImageFormat.BothFormatsExist;
    public string WimFileSize => ImageFormat.WimFileSize;
    public string EsdFileSize => ImageFormat.EsdFileSize;

    public ObservableCollection<ImageEdition> EditionChoices => ImageFormat.EditionChoices;

    public ImageEdition? SelectedEdition
    {
        get => ImageFormat.SelectedEdition;
        set => ImageFormat.SelectedEdition = value;
    }

    public bool IsBuiltIntoImage => ImageFormat.IsBuiltIntoImage;
    public bool ShowMountFolderWarning => ImageFormat.ShowMountFolderWarning;
    public bool ShowEditionCard => IsBuiltIntoImage && ShowConversionCard;
    public bool IsDuringSetup => !IsBuiltIntoImage;
    public bool CanChangeServicing => !Step5.IsCreatingMedia;
    public string CreatingMediaReason => _localizationService.GetString("WIMUtil_Disabled_CreatingMedia");

    public void SelectDuringSetup() => SelectServicing(builtIntoImage: false);

    public void SelectBuiltIntoImage() => SelectServicing(builtIntoImage: true);

    private void SelectServicing(bool builtIntoImage)
    {
        if (CanChangeServicing)
            ImageFormat.IsBuiltIntoImage = builtIntoImage;
    }

    public bool IsAutounattendSession => Step2.IsAutounattendSession;
    public bool UsesOwnAnswerFile => Step2.UsesOwnAnswerFile;
    public string OwnAnswerFileName => Step2.OwnAnswerFileName;

    // Step 3 shows what step 2 chose: the summary of Winhance's own file, or the name of the file the user picked, which
    // Winhance does not read. Nothing shows when no answer file was chosen.
    public bool ShowsSummaryCards => Step2.Choice == AnswerFileChoice.Builder;
    public bool ShowsAnswerFileCard => Step2.Choice != AnswerFileChoice.None;
    public bool ShowsCleanupCard => IsBuiltIntoImage;

    public string AnswerFileCardTitle => UsesOwnAnswerFile
        ? OwnAnswerFileName
        : _localizationService.GetString("WIMUtil_Step3_AnswerFile_Title");
    public string AnswerFileCardDescription => _localizationService.GetString(
        UsesOwnAnswerFile ? "WIMUtil_Step3_AnswerFile_OwnFile" : "WIMUtil_Step3_AnswerFile_Description");
    public string ChangeInAutounattendText
    {
        get
        {
            var format = _localizationService.GetString("Autounattend_Included_Change");
            return string.Format(format, _localizationService.GetString("Nav_Autounattend"));
        }
    }
    public string DriversHeadingText => _localizationService.GetString("WIMUtil_Heading_DriversAndUpdates");

    // Built-in mode puts the apps, the settings and the drivers into the image; the answer file and the apps Winhance
    // installs always wait for setup.
    public string InImageTagText => _localizationService.GetString("WIMUtil_Tag_InImage");
    public string DuringSetupTagText => _localizationService.GetString("WIMUtil_Tag_DuringSetup");
    public string ModeTagText => IsBuiltIntoImage ? InImageTagText : DuringSetupTagText;

    public string CleanupCardTitle => _localizationService.GetString("WIMUtil_Cleanup_Title");
    public string CleanupCardDescription => _localizationService.GetString("WIMUtil_Cleanup_Description");
    public string CleanupResetBaseText => _localizationService.GetString("WIMUtil_Cleanup_ResetBase");
    public string CleanupResetBaseWarning => _localizationService.GetString("WIMUtil_Cleanup_ResetBase_Warning");
    public string CleanupDownloadedUpdatesText => _localizationService.GetString("WIMUtil_Cleanup_DownloadedUpdates");
    public string CleanupServicingLogsText => _localizationService.GetString("WIMUtil_Cleanup_ServicingLogs");
    public string CleanupTempFilesText => _localizationService.GetString("WIMUtil_Cleanup_TempFiles");
    public string CleanupCrashReportsText => _localizationService.GetString("WIMUtil_Cleanup_CrashReports");

    // All ticked: the cleanup only frees space, and the card shows only when the changes are built in.
    [ObservableProperty]
    public partial bool CleanupResetBase { get; set; } = true;

    [ObservableProperty]
    public partial bool CleanupDownloadedUpdates { get; set; } = true;

    [ObservableProperty]
    public partial bool CleanupServicingLogs { get; set; } = true;

    [ObservableProperty]
    public partial bool CleanupTempFiles { get; set; } = true;

    [ObservableProperty]
    public partial bool CleanupCrashReports { get; set; } = true;

    public ImageCleanupOptions CleanupOptions => IsBuiltIntoImage
        ? new ImageCleanupOptions(CleanupResetBase, CleanupDownloadedUpdates, CleanupServicingLogs, CleanupTempFiles, CleanupCrashReports)
        : ImageCleanupOptions.None;

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

    public bool IsIsoDestination => Step5.IsIsoDestination;
    public bool IsUsbDestination => Step5.IsUsbDestination;
    public bool IsWimFormat => Step5.IsWimFormat;
    public bool IsEsdFormat => Step5.IsEsdFormat;
    public bool CanChooseFormat => Step5.CanChooseFormat;

    // Deleting one of the two image files is offered on step 2 ("Your image").
    public string? FormatLockedReason(bool canChoose, bool bothExist) => canChoose
        ? null
        : bothExist
            ? string.Format(_localizationService.GetString("WIMUtil_Disabled_FormatBoth"), 2)
            : _localizationService.GetString("WIMUtil_Disabled_FormatUnknown");
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
    public ObservableCollection<WizardActionCard> BuildCards => Step5.BuildCards;
    public bool HasBuildCards => Step5.HasBuildCards;
    public bool CanDeleteWorkingFolder => Step5.CanDeleteWorkingFolder;

    public string? DeleteFolderLockedReason(bool canDelete) => canDelete
        ? null
        : _localizationService.GetString(WimStep5IsoViewModel.IsWinhanceFolder(WorkingDirectory)
            ? "WIMUtil_Disabled_IsoInFolder"
            : "WIMUtil_Disabled_NotWinhanceFolder");

    public bool DeleteWorkingFolderAfterBuild
    {
        get => Step5.DeleteWorkingFolderAfterBuild;
        set => Step5.DeleteWorkingFolderAfterBuild = value;
    }

    public BootManagerChoice? SelectedBootManager
    {
        get => Step5.SelectedBootManager;
        set => Step5.SelectedBootManager = value;
    }

    public bool ShowBootManagerChoice => Step5.ShowBootManagerChoice;
    public string BootManagerDescription => Step5.BootManagerDescription;
    public bool IsBootManagerOutdated => Step5.IsBootManagerOutdated;

    public bool NeedsEdition => Step5.NeedsEdition;

    public IAsyncRelayCommand SelectWorkingDirectoryCommand => Step1.SelectWorkingDirectoryCommand;
    public IAsyncRelayCommand StartIsoExtractionCommand => Step1.StartIsoExtractionCommand;
    public IAsyncRelayCommand OpenWindows10DownloadCommand => Step1.OpenWindows10DownloadCommand;
    public IAsyncRelayCommand OpenWindows11DownloadCommand => Step1.OpenWindows11DownloadCommand;

    public IAsyncRelayCommand DeleteWimCommand => ImageFormat.DeleteWimCommand;
    public IAsyncRelayCommand DeleteEsdCommand => ImageFormat.DeleteEsdCommand;

    public IAsyncRelayCommand OpenAutounattendCommand => Step2.OpenAutounattendCommand;
    public IAsyncRelayCommand OpenSchneegansXmlGeneratorCommand => Step2.OpenSchneegansXmlGeneratorCommand;

    public IAsyncRelayCommand CreateMediaCommand => Step5.CreateMediaCommand;
    public IAsyncRelayCommand ShowBuildDetailsCommand => Step5.ShowBuildDetailsCommand;
    public IRelayCommand SelectWimFormatCommand => Step5.SelectWimFormatCommand;
    public IRelayCommand SelectEsdFormatCommand => Step5.SelectEsdFormatCommand;
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
        IAnswerFileValidator answerFileValidator,
        IAutounattendReader autounattendReader,
        IApplicationModeService applicationModeService,
        IBuilderModeEntry builderModeEntry,
        IEventBus eventBus,
        WimUtilSession session,
        ITargetWindowsService targetWindows,
        IWindowsUpdateListService updateListService,
        IWindowsUpdateStager updateStager,
        IOfflineServicingService offlineServicingService,
        IUserPreferencesService userPreferencesService)
    {
        _localizationService = localizationService;
        _fileSystemService = fileSystemService;
        _session = session;

        Step1 = new WimStep1ViewModel(
            isoService, taskProgressService, dialogService,
            localizationService, fileSystemService, filePickerService, logService, userPreferencesService);

        ImageFormat = new WimImageFormatViewModel(
            wimImageService, dialogService,
            dispatcherService, localizationService, logService, offlineServicingService);

        Step2 = new WimStep2XmlViewModel(
            wimCustomizationService,
            dialogService, localizationService, fileSystemService, filePickerService, logService,
            answerFileValidator, autounattendReader, applicationModeService, builderModeEntry, dispatcherService,
            eventBus, AnswerFileCheck, session, wimImageService, targetWindows);

        Step3 = new WimStep3DriversViewModel(
            wimCustomizationService, dialogService,
            localizationService, fileSystemService, filePickerService, logService);

        Step4 = new WimStep4UpdatesViewModel(
            wimCustomizationService, localizationService, logService,
            updateListService, updateStager);

        Step5 = new WimStep5IsoViewModel(
            wimCustomizationService, isoService, usbMediaWriter, bootManagerService, taskProgressService, processExecutor,
            dialogService, localizationService, fileSystemService, filePickerService, logService, answerFileValidator, AnswerFileCheck,
            offlineServicingService, saves, selections, session, wimImageService, [Step2, Step3, Step4]);

        Step1State = new WizardStepState();
        Step2State = new WizardStepState();
        Step3State = new WizardStepState();
        Step4State = new WizardStepState();

        InitializeStepStates();
        Steps = [Step1State, Step2State, Step3State, Step4State];
        SetCurrentStep(1);

        _localizationService.LanguageChanged += OnLanguageChanged;

        Step1.PropertyChanged += OnSubViewModelPropertyChanged;
        ImageFormat.PropertyChanged += OnSubViewModelPropertyChanged;
        Step2.PropertyChanged += OnSubViewModelPropertyChanged;
        Step3.PropertyChanged += OnSubViewModelPropertyChanged;
        Step4.PropertyChanged += OnSubViewModelPropertyChanged;
        Step5.PropertyChanged += OnSubViewModelPropertyChanged;
        Step5.ImageServiced += OnImageServiced;
        Step5.ImageFormatChanged += OnImageFormatChanged;
        Step5.WorkingFilesDeleted += OnWorkingFilesDeleted;
        Step1.MediaReady += OnMediaReady;
        AnswerFileCheck.PropertyChanged += OnAnswerFileCheckChanged;
        _session.ChangesChanged += OnSessionChangesChanged;

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
        _ = Step2.JoinSessionAsync();
    }

    // With both files on the media there is no one current format, and step 2 already warns about it.
    private string? CurrentFormatText() =>
        ImageFormat is { BothFormatsExist: false, CurrentImageFormat: { FileSizeBytes: > 0 } image }
            ? _localizationService.GetString("WIMUtil_Format_Current", WimStep5IsoViewModel.FormatName(image.Format),
                WimImageFormatViewModel.FormatFileSize(image.FileSizeBytes))
            : null;

    [RelayCommand]
    private void NavigateToStep(string? stepParameter)
    {
        if (string.IsNullOrEmpty(stepParameter) || !int.TryParse(stepParameter, out int targetStep)) return;

        var state = GetStepState(targetStep);
        if (state is null || !IsStepAvailable(targetStep)) return;

        // Steps toggle independently: collapsing one leaves the others as-is. An opened step takes the rail's
        // ring, since opening a step low on the page scrolls nothing.
        state.IsExpanded = !state.IsExpanded;
        if (state.IsExpanded)
            SetCurrentStep(targetStep);
    }

    public void SetCurrentStep(int step)
    {
        foreach (var state in Steps)
            state.IsCurrent = state.StepNumber == step;
    }

    // The rail opens a collapsed step so the jump lands on content; a locked step stays shut.
    public bool ShowStep(int step)
    {
        var state = GetStepState(step);
        if (state is null || !IsStepAvailable(step)) return false;

        state.IsExpanded = true;
        SetCurrentStep(step);
        return true;
    }

    private WizardStepState? GetStepState(int step) => step switch
    {
        1 => Step1State,
        2 => Step2State,
        3 => Step3State,
        4 => Step4State,
        _ => null
    };

    private bool IsStepAvailable(int step) => step switch
    {
        1 => !IsImageLocked,
        2 or 3 => Step1.IsExtractionComplete && !IsImageLocked,
        4 => Step1.IsExtractionComplete,
        _ => false
    };

    // Creating media reads, services and trims install.wim, so Step 1 cannot touch it meanwhile.
    private bool IsImageLocked => Step5.IsCreatingMedia;

    private void InitializeStepStates()
    {
        Step1State = new WizardStepState
        {
            StepNumber = 1,
            Glyph = "\uE958", // CD, in Segoe Fluent Icons
            HasNextStep = true,
            StatusText = _localizationService.GetString("WIMUtil_Status_NoIsoSelected"),
            IsExpanded = true,
            IsAvailable = true
        };

        Step2State = new WizardStepState
        {
            StepNumber = 2,
            Icon = FluentIcons.Common.Icon.WrenchScrewdriver,
            HasNextStep = true,
            StatusText = _localizationService.GetString("WIMUtil_Status_CompleteStep1")
        };

        Step3State = new WizardStepState
        {
            StepNumber = 3,
            Icon = FluentIcons.Common.Icon.Options,
            HasNextStep = true,
            StatusText = _localizationService.GetString("WIMUtil_Status_CompleteStep1")
        };

        Step4State = new WizardStepState
        {
            StepNumber = 4,
            Icon = FluentIcons.Common.Icon.Rocket,
            StatusText = _localizationService.GetString("WIMUtil_Status_CompleteStep1")
        };
    }

    private void UpdateStepStates()
    {
        var extractionComplete = Step1.IsExtractionComplete;
        var isExtracting = Step1.IsExtracting;
        var driversAddedKey = IsBuiltIntoImage ? "WIMUtil_Status_DriversReadyForImage" : "WIMUtil_Status_DriversAdded";
        var updateAddedKey = IsBuiltIntoImage ? "WIMUtil_Status_UpdateReadyForImage" : "WIMUtil_Status_UpdateAdded";
        var toUsb = Step5.IsUsbDestination;

        SetStepTitle(Step1State, "WIMUtil_Step1_Title");
        SetStepTitle(Step2State, "WIMUtil_Step2_Title");
        SetStepTitle(Step3State, "WIMUtil_Step3_Title");
        SetStepTitle(Step4State, toUsb ? "WIMUtil_ButtonWriteUsb" : "WIMUtil_Step4_Title");
        Step4State.Icon = toUsb ? FluentIcons.Common.Icon.UsbStick : FluentIcons.Common.Icon.Rocket;

        Step1State.IsAvailable = !IsImageLocked;
        Step1State.IsComplete = extractionComplete;
        Step1State.StatusText = extractionComplete
            ? _localizationService.GetString("WIMUtil_Status_IsoExtracted")
            : isExtracting
                ? _localizationService.GetString("WIMUtil_Status_Extracting")
                : !string.IsNullOrEmpty(Step1.SelectedIsoPath)
                    ? _localizationService.GetString("WIMUtil_Status_IsoSelected")
                    : _localizationService.GetString("WIMUtil_Status_NoIsoSelected");

        // A tick on steps 2 and 3 means the user chose something there. Create does the work. A locked step shows
        // none: a Builder answer file or Builder changes are chosen before any ISO is.
        Step2State.IsAvailable = extractionComplete && !IsImageLocked;
        Step2State.IsComplete = extractionComplete && Step2.Choice != AnswerFileChoice.None;
        Step2State.StatusText = !extractionComplete
            ? _localizationService.GetString("WIMUtil_Status_CompleteStep1")
            : Step2StatusText();

        Step3State.IsAvailable = extractionComplete && !IsImageLocked;
        Step3State.IsComplete = extractionComplete && (Step3.HasDriverChoice || Step4.AddUpdate || HasSelectedChanges);
        Step3State.StatusText = !extractionComplete
            ? _localizationService.GetString("WIMUtil_Status_CompleteStep1")
            : ChangesStatusText(driversAddedKey, updateAddedKey);

        Step4State.IsAvailable = extractionComplete;
        Step4State.IsComplete = Step5.IsIsoCreated;
        Step4State.StatusText = !extractionComplete
            ? _localizationService.GetString("WIMUtil_Status_CompleteStep1")
            : Step5.IsIsoCreated
                ? _localizationService.GetString(toUsb ? "WIMUtil_Desc_UsbWriteSuccess" : "WIMUtil_Status_IsoCreated")
                : toUsb
                    ? _localizationService.GetString(Step5.SelectedUsbTarget is null ? "WIMUtil_Label_NoUsbSelected" : "WIMUtil_Status_ReadyToWriteUsb")
                    : !string.IsNullOrEmpty(Step5.OutputIsoPath)
                        ? $"{_localizationService.GetString("WIMUtil_Label_Output")}: {_fileSystemService.GetFileName(Step5.OutputIsoPath)}"
                        : _localizationService.GetString("WIMUtil_Status_ReadyToCreateIso");

        OnPropertyChanged(nameof(Step1State));
        OnPropertyChanged(nameof(Step2State));
        OnPropertyChanged(nameof(Step3State));
        OnPropertyChanged(nameof(Step4State));
    }

    private void OnSessionChangesChanged(object? sender, EventArgs e) => UpdateStepStates();

    // The counts describe the Winhance answer file, so they only apply while its summary cards show.
    private bool HasSelectedChanges =>
        ShowsSummaryCards && _session.Changes is { } changes
        && (changes.RemovedApps > 0 || changes.InstalledApps > 0 || changes.Settings > 0);

    private string ChangesStatusText(string driversAddedKey, string updateAddedKey)
    {
        var added = new List<string>();
        if (ShowsSummaryCards)
        {
            var changes = _session.Changes;
            AddCount(added, changes.RemovedApps, "Autounattend_Included_WindowsApps_One", "Autounattend_Included_WindowsApps");
            AddCount(added, changes.InstalledApps, "Autounattend_Included_ExternalApps_One", "Autounattend_Included_ExternalApps");
            AddCount(added, changes.Settings, "Autounattend_Included_Settings_One", "Autounattend_Included_Settings");
        }
        if (Step3.HasDriverChoice)
            added.Add(_localizationService.GetString(driversAddedKey));
        if (Step4.AddUpdate)
            added.Add(_localizationService.GetString(updateAddedKey));
        return added.Count > 0
            ? string.Join(", ", added)
            : _localizationService.GetString("WIMUtil_Status_NoChangesAdded");
    }

    private void AddCount(List<string> added, int count, string oneKey, string manyKey)
    {
        if (count > 0)
            added.Add(count == 1 ? _localizationService.GetString(oneKey, count) : _localizationService.GetString(manyKey, count));
    }

    private void SetStepTitle(WizardStepState step, string titleKey)
    {
        step.Title = _localizationService.GetString(titleKey);
        step.AccessibleName = _localizationService.GetString("WIMUtil_Step_Header", step.StepNumber, step.Title);
    }

    // The header reads the shared verdict on the file step 2 holds rather than step 2's own wording. A refused
    // file's report describes something Setup will never see; it belongs to the banner alone.
    private string Step2StatusText() => Step2.Choice switch
    {
        AnswerFileChoice.Builder => _localizationService.GetString("WIMUtil_Status_AnswerFileBuilder"),
        AnswerFileChoice.OwnXml => AnswerFileCheck.LastReport is { } report
            && (AnswerFileCheck.Subject == Step2.SelectedXmlPath
                || AnswerFileCheck.Subject == _fileSystemService.CombinePath(WorkingDirectory, "autounattend.xml"))
                ? AnswerFileReportDialog.Verdict(report, _localizationService)
                : Step2.XmlStatus,
        _ => _localizationService.GetString("WIMUtil_Status_NoXmlAdded"),
    };

    // Once the ISO is extracted, steps 2-4 expand so the user sees every remaining task; step 1 stays as
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
        }
        else
        {
            _autoExpandApplied = false;

            Step1State.IsExpanded = true;
            Step2State.IsExpanded = false;
            Step3State.IsExpanded = false;
            Step4State.IsExpanded = false;
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
        {
            ImageFormat.IsImageLocked = IsImageLocked;

            // The format and sizes shown come from the image, which building the media can change.
            if (!Step5.IsCreatingMedia)
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
                    break;
                case nameof(WimStep1ViewModel.CanStartExtraction): OnPropertyChanged(nameof(CanStartExtraction)); break;
                case nameof(WimStep1ViewModel.ShowSpaceWarning): OnPropertyChanged(nameof(ShowWorkingDirectorySpaceWarning)); break;
                case nameof(WimStep1ViewModel.SpaceWarningText): OnPropertyChanged(nameof(WorkingDirectorySpaceWarningText)); break;
                case nameof(WimStep1ViewModel.HasExtractedIsoAlready): OnPropertyChanged(nameof(HasExtractedIsoAlready)); break;
            }
        }
        else if (sender == ImageFormat)
        {
            switch (e.PropertyName)
            {
                case nameof(WimImageFormatViewModel.CurrentImageFormat):
                    OnPropertyChanged(nameof(EditionChoices));
                    OnPropertyChanged(nameof(FormatCardDescription));
                    _ = Step4.SetMediaAsync(ImageFormat.CurrentImageFormat);
                    PushMediaFormat();
                    break;
                case nameof(WimImageFormatViewModel.ShowConversionCard):
                    OnPropertyChanged(nameof(ShowConversionCard));
                    OnPropertyChanged(nameof(ShowEditionCard));
                    break;
                case nameof(WimImageFormatViewModel.BothFormatsExist):
                    OnPropertyChanged(nameof(BothFormatsExist));
                    OnPropertyChanged(nameof(FormatCardDescription));
                    PushMediaFormat();
                    break;
                case nameof(WimImageFormatViewModel.SelectedEdition):
                    PushBuildChoices();
                    OnPropertyChanged(nameof(SelectedEdition));
                    break;
                case nameof(WimImageFormatViewModel.ShowMountFolderWarning): OnPropertyChanged(nameof(ShowMountFolderWarning)); break;
                case nameof(WimImageFormatViewModel.IsBuiltIntoImage):
                    PushServicingMode();
                    OnPropertyChanged(nameof(IsBuiltIntoImage));
                    OnPropertyChanged(nameof(IsDuringSetup));
                    OnPropertyChanged(nameof(ShowEditionCard));
                    OnPropertyChanged(nameof(ShowsCleanupCard));
                    OnPropertyChanged(nameof(ModeTagText));
                    OnPropertyChanged(nameof(CleanupOptions));
                    break;
                case nameof(WimImageFormatViewModel.WimFileSize): OnPropertyChanged(nameof(WimFileSize)); break;
                case nameof(WimImageFormatViewModel.EsdFileSize): OnPropertyChanged(nameof(EsdFileSize)); break;
            }
        }
        else if (sender == Step2)
        {
            switch (e.PropertyName)
            {
                case nameof(WimStep2XmlViewModel.IsAutounattendSession):
                    PushBuildChoices();
                    OnPropertyChanged(nameof(IsAutounattendSession));
                    break;
                case nameof(WimStep2XmlViewModel.Choice):
                    PushBuildChoices();
                    OnPropertyChanged(nameof(ShowsSummaryCards));
                    OnPropertyChanged(nameof(ShowsAnswerFileCard));
                    break;
                case nameof(WimStep2XmlViewModel.UsesOwnAnswerFile):
                    OnPropertyChanged(nameof(UsesOwnAnswerFile));
                    OnPropertyChanged(nameof(AnswerFileCardTitle));
                    OnPropertyChanged(nameof(AnswerFileCardDescription));
                    break;
                case nameof(WimStep2XmlViewModel.OwnAnswerFileName):
                    OnPropertyChanged(nameof(OwnAnswerFileName));
                    OnPropertyChanged(nameof(AnswerFileCardTitle));
                    break;
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
                case nameof(WimStep5IsoViewModel.NeedsEdition): OnPropertyChanged(nameof(NeedsEdition)); break;
                case nameof(WimStep5IsoViewModel.IsCreatingMedia): OnPropertyChanged(nameof(CanChangeServicing)); break;
                case nameof(WimStep5IsoViewModel.HasBuildCards): OnPropertyChanged(nameof(HasBuildCards)); break;
                case nameof(WimStep5IsoViewModel.DeleteWorkingFolderAfterBuild): OnPropertyChanged(nameof(DeleteWorkingFolderAfterBuild)); break;
                case nameof(WimStep5IsoViewModel.CanDeleteWorkingFolder): OnPropertyChanged(nameof(CanDeleteWorkingFolder)); break;
                case nameof(WimStep5IsoViewModel.IsWimFormat): OnPropertyChanged(nameof(IsWimFormat)); break;
                case nameof(WimStep5IsoViewModel.IsEsdFormat): OnPropertyChanged(nameof(IsEsdFormat)); break;
                case nameof(WimStep5IsoViewModel.CanChooseFormat): OnPropertyChanged(nameof(CanChooseFormat)); break;
                case nameof(WimStep5IsoViewModel.IsUsbDestination):
                    OnPropertyChanged(nameof(IsIsoDestination));
                    OnPropertyChanged(nameof(IsUsbDestination));
                    OnPropertyChanged(nameof(ButtonCreateMediaText));
                    break;
            }
        }
    }

    // Steps 2-4 read the mode chosen in step 2; the session carries it to the Unattend page.
    private void PushServicingMode()
    {
        var builtIn = ImageFormat.IsBuiltIntoImage;
        _session.BuiltIntoImage = builtIn;
        Step3.IsBuiltIntoImage = builtIn;
        Step4.IsBuiltIntoImage = builtIn;
        Step5.IsBuiltIntoImage = builtIn;
        PushBuildChoices();
    }

    // Create reads these from step 5, which cannot see the cards and the image the facade holds.
    private void PushBuildChoices()
    {
        Step5.Cleanup = CleanupOptions;
        Step5.EditionIndex = ImageFormat.SelectedEdition?.Index;
        Step5.WritesAnswerFile = IsAutounattendSession && Step2.Choice == AnswerFileChoice.Builder;
    }

    // The output format follows the media's own until the user picks one.
    private void PushMediaFormat() =>
        Step5.ShowMediaFormat(ImageFormat.CurrentImageFormat?.Format, ImageFormat.BothFormatsExist);

    partial void OnCleanupResetBaseChanged(bool value) => PushBuildChoices();

    partial void OnCleanupDownloadedUpdatesChanged(bool value) => PushBuildChoices();

    partial void OnCleanupServicingLogsChanged(bool value) => PushBuildChoices();

    partial void OnCleanupTempFilesChanged(bool value) => PushBuildChoices();

    partial void OnCleanupCrashReportsChanged(bool value) => PushBuildChoices();

    // Servicing tidied what it built in out of the media folder, so the drivers and update choices no longer hold it.
    private void OnImageServiced(object? sender, OfflineServicingResult result)
    {
        Step3.PickUpMedia();
        Step4.ShowUpdateState();
    }

    private void OnImageFormatChanged(object? sender, EventArgs e) => _ = ImageFormat.SafeDetectImageFormatAsync();

    private void OnWorkingFilesDeleted(object? sender, EventArgs e) => Step1.ResetAfterWorkingFilesDeleted();

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
        UpdateStepStates();

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(PageDescription));
        OnPropertyChanged(nameof(CheckboxExtractedAlreadyText));
        OnPropertyChanged(nameof(ButtonSelectFolderText));
        OnPropertyChanged(nameof(ButtonStartExtractionText));
        OnPropertyChanged(nameof(BothImagesTitle));
        OnPropertyChanged(nameof(BothImagesDescription));
        OnPropertyChanged(nameof(ButtonDeleteWimText));
        OnPropertyChanged(nameof(ButtonDeleteEsdText));
        OnPropertyChanged(nameof(DownloadIsoText));
        OnPropertyChanged(nameof(ButtonWindows10Text));
        OnPropertyChanged(nameof(ButtonWindows11Text));
        OnPropertyChanged(nameof(TooltipDownloadWindows10));
        OnPropertyChanged(nameof(TooltipDownloadWindows11));
        OnPropertyChanged(nameof(AnswerFileCardTitle));
        OnPropertyChanged(nameof(AnswerFileCardDescription));
        OnPropertyChanged(nameof(ChangeInAutounattendText));
        OnPropertyChanged(nameof(DriversHeadingText));
        OnPropertyChanged(nameof(InImageTagText));
        OnPropertyChanged(nameof(DuringSetupTagText));
        OnPropertyChanged(nameof(ModeTagText));
        OnPropertyChanged(nameof(CleanupCardTitle));
        OnPropertyChanged(nameof(CleanupCardDescription));
        OnPropertyChanged(nameof(CleanupResetBaseText));
        OnPropertyChanged(nameof(CleanupResetBaseWarning));
        OnPropertyChanged(nameof(CleanupDownloadedUpdatesText));
        OnPropertyChanged(nameof(CleanupServicingLogsText));
        OnPropertyChanged(nameof(CleanupTempFilesText));
        OnPropertyChanged(nameof(CleanupCrashReportsText));
        OnPropertyChanged(nameof(GenerateXmlFilesText));
        OnPropertyChanged(nameof(ButtonSchneegansText));
        OnPropertyChanged(nameof(TooltipSchneegans));
        OnPropertyChanged(nameof(ButtonCreateMediaText));
        OnPropertyChanged(nameof(ShowBuildDetailsText));
        OnPropertyChanged(nameof(DeleteWorkingFolderText));
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
        OnPropertyChanged(nameof(StepRailName));
        OnPropertyChanged(nameof(DuringSetupTitle));
        OnPropertyChanged(nameof(DuringSetupDescription));
        OnPropertyChanged(nameof(BuiltIntoImageTitle));
        OnPropertyChanged(nameof(BuiltIntoImageDescription));
        OnPropertyChanged(nameof(YourImageHeadingText));
        OnPropertyChanged(nameof(EditionCardTitle));
        OnPropertyChanged(nameof(EditionCardDescription));
        OnPropertyChanged(nameof(MountFolderWarningText));
        OnPropertyChanged(nameof(NeedsEditionText));
        OnPropertyChanged(nameof(AnswerFileBannerTitle));
        OnPropertyChanged(nameof(AnswerFileBannerSummary));
        OnPropertyChanged(nameof(AnswerFileBannerMessage));
        OnPropertyChanged(nameof(AnswerFileChoiceTitle));
        OnPropertyChanged(nameof(AnswerFileChoiceDescription));
        OnPropertyChanged(nameof(AnswerFileBuilderText));
        OnPropertyChanged(nameof(AnswerFileOwnXmlText));
        OnPropertyChanged(nameof(AnswerFileBuilderDescription));
        OnPropertyChanged(nameof(AnswerFileOwnXmlDescription));
        OnPropertyChanged(nameof(MediaAnswerFileText));
        OnPropertyChanged(nameof(DeleteMediaAnswerFileText));
        OnPropertyChanged(nameof(ChangeFileText));
        OnPropertyChanged(nameof(AddSystemDriversText));
        OnPropertyChanged(nameof(AddFolderDriversText));
        OnPropertyChanged(nameof(ChooseFolderText));
        OnPropertyChanged(nameof(RemoveDriversFolderText));
        OnPropertyChanged(nameof(AddUpdateText));
        OnPropertyChanged(nameof(FormatCardTitle));
        OnPropertyChanged(nameof(FormatCardDescription));
        OnPropertyChanged(nameof(FormatWimText));
        OnPropertyChanged(nameof(FormatEsdText));
        Step3.RefreshText();
        Step4.ShowDescription();
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
        Step5.ImageFormatChanged -= OnImageFormatChanged;
        Step5.WorkingFilesDeleted -= OnWorkingFilesDeleted;
        Step1.MediaReady -= OnMediaReady;
        AnswerFileCheck.PropertyChanged -= OnAnswerFileCheckChanged;
        _session.ChangesChanged -= OnSessionChangesChanged;

        (Step2 as IDisposable)?.Dispose();
        (ImageFormat as IDisposable)?.Dispose();
        GC.SuppressFinalize(this);
    }
}
