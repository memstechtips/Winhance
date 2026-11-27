using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private static readonly string[] LocalizedProperties =
    [
        nameof(Title), nameof(PageDescription), nameof(CheckboxExtractedAlreadyText), nameof(ButtonSelectFolderText),
        nameof(ButtonStartExtractionText), nameof(BothImagesTitle), nameof(BothImagesDescription),
        nameof(ButtonDeleteWimText), nameof(ButtonDeleteEsdText), nameof(DownloadIsoText), nameof(ButtonWindows10Text),
        nameof(ButtonWindows11Text), nameof(TooltipDownloadWindows10), nameof(TooltipDownloadWindows11),
        nameof(AnswerFileCardTitle), nameof(AnswerFileCardDescription), nameof(ChangeInAutounattendText),
        nameof(DriversHeadingText), nameof(InImageTagText), nameof(DuringSetupTagText), nameof(ModeTagText),
        nameof(CleanupCardTitle), nameof(CleanupCardDescription), nameof(CleanupResetBaseText),
        nameof(CleanupResetBaseWarning), nameof(CleanupDownloadedUpdatesText), nameof(CleanupServicingLogsText),
        nameof(CleanupTempFilesText), nameof(CleanupCrashReportsText), nameof(GenerateXmlFilesText),
        nameof(ButtonSchneegansText), nameof(TooltipSchneegans), nameof(ButtonCreateMediaText),
        nameof(ShowBuildDetailsText), nameof(DeleteWorkingFolderText), nameof(DestinationCardTitle),
        nameof(DestinationCardDescription), nameof(DestinationIsoText), nameof(DestinationUsbText),
        nameof(NoUsbSelectedText), nameof(UsbEraseWarningText), nameof(ShowUsbHardDrivesText),
        nameof(ShowUsbHardDrivesWarningText), nameof(BootManagerCardTitle), nameof(BootManagerOutdatedText),
        nameof(ServicingCardTitle), nameof(StepRailName), nameof(DuringSetupTitle), nameof(DuringSetupDescription),
        nameof(BuiltIntoImageTitle), nameof(BuiltIntoImageDescription), nameof(YourImageHeadingText),
        nameof(EditionCardTitle), nameof(EditionCardDescription), nameof(MountFolderWarningText),
        nameof(NeedsEditionText), nameof(AnswerFileBannerTitle), nameof(AnswerFileBannerSummary),
        nameof(AnswerFileBannerMessage), nameof(AnswerFileChoiceTitle), nameof(AnswerFileChoiceDescription),
        nameof(AnswerFileBuilderText), nameof(AnswerFileOwnXmlText), nameof(AnswerFileBuilderDescription),
        nameof(AnswerFileOwnXmlDescription), nameof(MediaAnswerFileText), nameof(DeleteMediaAnswerFileText),
        nameof(ChangeFileText), nameof(AddSystemDriversText), nameof(AddFolderDriversText), nameof(ChooseFolderText),
        nameof(RemoveDriversFolderText), nameof(AddUpdateText), nameof(FormatCardTitle), nameof(FormatCardDescription),
        nameof(FormatWimText), nameof(FormatEsdText),
    ];

    private readonly ILocalizationService _localizationService;
    private readonly IFileSystemService _fileSystemService;
    private readonly ILogService _logService;
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

    public bool CanChangeServicing => !Step5.IsCreatingMedia;
    public string CreatingMediaReason => _localizationService.GetString("WIMUtil_Disabled_CreatingMedia");

    public void SelectDuringSetup() => SelectServicing(builtIntoImage: false);

    public void SelectBuiltIntoImage() => SelectServicing(builtIntoImage: true);

    private void SelectServicing(bool builtIntoImage)
    {
        if (CanChangeServicing)
            ImageFormat.IsBuiltIntoImage = builtIntoImage;
    }

    public string AnswerFileCardTitle => Step2.UsesOwnAnswerFile
        ? Step2.OwnAnswerFileName
        : _localizationService.GetString("WIMUtil_Step3_AnswerFile_Title");
    public string AnswerFileCardDescription => _localizationService.GetString(
        Step2.UsesOwnAnswerFile ? "WIMUtil_Step3_AnswerFile_OwnFile" : "WIMUtil_Step3_AnswerFile_Description");
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
    public string ModeTagText => ImageFormat.IsBuiltIntoImage ? InImageTagText : DuringSetupTagText;

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

    public ImageCleanupOptions CleanupOptions => ImageFormat.IsBuiltIntoImage
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

    // Deleting one of the two image files is offered on step 2 ("Your image").
    public string? FormatLockedReason(bool canChoose, bool bothExist) => canChoose
        ? null
        : bothExist
            ? string.Format(_localizationService.GetString("WIMUtil_Disabled_FormatBoth"), 2)
            : _localizationService.GetString("WIMUtil_Disabled_FormatUnknown");
    public string? DeleteFolderLockedReason(bool canDelete) => canDelete
        ? null
        : _localizationService.GetString(WimStep5IsoViewModel.IsWinhanceFolder(Step1.WorkingDirectory)
            ? "WIMUtil_Disabled_IsoInFolder"
            : "WIMUtil_Disabled_NotWinhanceFolder");

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
        _logService = logService;
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
        Step2.JoinSessionAsync().FireAndForget(_logService);
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
            Icon = FluentIcons.Common.Icon.Target,
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
        var driversAddedKey = ImageFormat.IsBuiltIntoImage ? "WIMUtil_Status_DriversReadyForImage" : "WIMUtil_Status_DriversAdded";
        var updateAddedKey = ImageFormat.IsBuiltIntoImage ? "WIMUtil_Status_UpdateReadyForImage" : "WIMUtil_Status_UpdateAdded";
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
    }

    private void OnSessionChangesChanged(object? sender, EventArgs e) => UpdateStepStates();

    // The counts describe the Winhance answer file, so they only apply while its summary cards show.
    private bool HasSelectedChanges =>
        Step2.IsBuilderChosen && _session.Changes is { } changes
        && (changes.RemovedApps > 0 || changes.InstalledApps > 0 || changes.Settings > 0);

    private string ChangesStatusText(string driversAddedKey, string updateAddedKey)
    {
        var added = new List<string>();
        if (Step2.IsBuilderChosen)
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
                || AnswerFileCheck.Subject == _fileSystemService.CombinePath(Step1.WorkingDirectory, "autounattend.xml"))
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

    private void OnMediaReady(object? sender, EventArgs e) => Step2.PickUpMediaAnswerFileAsync().FireAndForget(_logService);

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
                        ImageFormat.DetectImageFormatAsync().FireAndForget(_logService);
                        Step5.InspectBootManagerAsync().FireAndForget(_logService);
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
                ImageFormat.DetectImageFormatAsync().FireAndForget(_logService);
        }

        ReactToStepChange(sender, e);

        UpdateStepStates();
    }

    private void ReactToStepChange(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == ImageFormat)
        {
            switch (e.PropertyName)
            {
                case nameof(WimImageFormatViewModel.CurrentImageFormat):
                    OnPropertyChanged(nameof(FormatCardDescription));
                    Step4.SetMediaAsync(ImageFormat.CurrentImageFormat).FireAndForget(_logService);
                    PushMediaFormat();
                    break;
                case nameof(WimImageFormatViewModel.BothFormatsExist):
                    OnPropertyChanged(nameof(FormatCardDescription));
                    PushMediaFormat();
                    break;
                case nameof(WimImageFormatViewModel.SelectedEdition):
                    PushBuildChoices();
                    break;
                case nameof(WimImageFormatViewModel.IsBuiltIntoImage):
                    PushServicingMode();
                    OnPropertyChanged(nameof(ModeTagText));
                    break;
            }
        }
        else if (sender == Step2)
        {
            switch (e.PropertyName)
            {
                case nameof(WimStep2XmlViewModel.IsAutounattendSession):
                case nameof(WimStep2XmlViewModel.Choice):
                    PushBuildChoices();
                    break;
                case nameof(WimStep2XmlViewModel.UsesOwnAnswerFile):
                    OnPropertyChanged(nameof(AnswerFileCardTitle));
                    OnPropertyChanged(nameof(AnswerFileCardDescription));
                    break;
                case nameof(WimStep2XmlViewModel.OwnAnswerFileName):
                    OnPropertyChanged(nameof(AnswerFileCardTitle));
                    break;
            }
        }
        else if (sender == Step5)
        {
            switch (e.PropertyName)
            {
                case nameof(WimStep5IsoViewModel.IsCreatingMedia): OnPropertyChanged(nameof(CanChangeServicing)); break;
                case nameof(WimStep5IsoViewModel.IsUsbDestination): OnPropertyChanged(nameof(ButtonCreateMediaText)); break;
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
        Step5.WritesAnswerFile = Step2.IsAutounattendSession && Step2.Choice == AnswerFileChoice.Builder;
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

    private void OnImageFormatChanged(object? sender, EventArgs e) => ImageFormat.DetectImageFormatAsync().FireAndForget(_logService);

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
        foreach (var name in LocalizedProperties)
            OnPropertyChanged(name);
        Step3.RefreshText();
        Step4.ShowDescription();
        Step5.RefreshBootManagerText();
        Step1.RelabelActionCards();
        Step5.RelabelActionCards();
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
        Step5.Dispose();
        GC.SuppressFinalize(this);
    }
}
