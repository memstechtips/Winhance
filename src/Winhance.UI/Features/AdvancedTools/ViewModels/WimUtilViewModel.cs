using System;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.AdvancedTools.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.AdvancedTools.ViewModels;

/// <summary>
/// Thin orchestrator ViewModel for the WIM Utility wizard.
/// Owns wizard navigation state and delegates step-specific work to sub-ViewModels.
/// </summary>
public partial class WimUtilViewModel : ObservableObject
{
    private readonly IWimUtilService _wimUtilService;
    private readonly ILocalizationService _localizationService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IFileSystemService _fileSystemService;

    // ── Sub-ViewModels (public for XAML binding) ──────────────────────

    public WimStep1ViewModel Step1 { get; }
    public WimImageFormatViewModel ImageFormat { get; }
    public WimStep2XmlViewModel Step2 { get; }
    public WimStep3DriversViewModel Step3 { get; }
    public WimStep4IsoViewModel Step4 { get; }

    // ── Wizard navigation state ──────────────────────────────────────

    [ObservableProperty]
    public partial int CurrentStep { get; set; }

    [ObservableProperty]
    public partial WizardStepState Step1State { get; set; }

    [ObservableProperty]
    public partial WizardStepState Step2State { get; set; }

    [ObservableProperty]
    public partial WizardStepState Step3State { get; set; }

    [ObservableProperty]
    public partial WizardStepState Step4State { get; set; }

    // ── Localization labels (read-only, for XAML) ────────────────────

    public string Title => _localizationService?.GetString("WIMUtil_Title") ?? "Windows Installation Media Utility";
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
    public string ButtonCreateIsoText => _localizationService.GetString("WIMUtil_ButtonCreateISO");

    // ── Forwarded properties for backward-compatible XAML bindings ───
    // These delegate to the sub-VMs so existing XAML can still bind to
    // ViewModel.PropertyName while the XAML migration happens.

    public string SelectedIsoPath => Step1.SelectedIsoPath;
    public string WorkingDirectory => Step1.WorkingDirectory;
    public bool CanStartExtraction => Step1.CanStartExtraction;
    public bool IsExtractionComplete => Step1.IsExtractionComplete;
    public bool IsExtracting => Step1.IsExtracting;
    public bool HasExtractedIsoAlready { get => Step1.HasExtractedIsoAlready; set => Step1.HasExtractedIsoAlready = value; }

    public WizardActionCard SelectIsoCard => Step1.SelectIsoCard;
    public WizardActionCard SelectDirectoryCard => Step1.SelectDirectoryCard;

    public Core.Features.AdvancedTools.Models.ImageFormatInfo? CurrentImageFormat => ImageFormat.CurrentImageFormat;
    public bool ShowConversionCard => ImageFormat.ShowConversionCard;
    public bool IsConverting => ImageFormat.IsConverting;
    public string ConversionStatus => ImageFormat.ConversionStatus;
    public bool BothFormatsExist => ImageFormat.BothFormatsExist;
    public string WimFileSize => ImageFormat.WimFileSize;
    public string EsdFileSize => ImageFormat.EsdFileSize;
    public Core.Features.AdvancedTools.Models.ImageDetectionResult? DetectionResult => ImageFormat.DetectionResult;
    public WizardActionCard ConvertImageCard => ImageFormat.ConvertImageCard;

    public string SelectedXmlPath => Step2.SelectedXmlPath;
    public string XmlStatus => Step2.XmlStatus;
    public bool IsXmlAdded => Step2.IsXmlAdded;
    public WizardActionCard GenerateWinhanceXmlCard => Step2.GenerateWinhanceXmlCard;
    public WizardActionCard DownloadXmlCard => Step2.DownloadXmlCard;
    public WizardActionCard SelectXmlCard => Step2.SelectXmlCard;

    public bool AreDriversAdded => Step3.AreDriversAdded;
    public WizardActionCard ExtractSystemDriversCard => Step3.ExtractSystemDriversCard;
    public WizardActionCard SelectCustomDriversCard => Step3.SelectCustomDriversCard;

    public bool IsOscdimgAvailable => Step4.IsOscdimgAvailable;
    public string OutputIsoPath => Step4.OutputIsoPath;
    public bool IsIsoCreated => Step4.IsIsoCreated;
    public WizardActionCard DownloadOscdimgCard => Step4.DownloadOscdimgCard;
    public WizardActionCard SelectOutputCard => Step4.SelectOutputCard;

    // ── Forwarded commands for backward-compatible XAML bindings ─────

    public IRelayCommand SelectIsoFileCommand => Step1.SelectIsoFileCommand;
    public IAsyncRelayCommand SelectWorkingDirectoryCommand => Step1.SelectWorkingDirectoryCommand;
    public IAsyncRelayCommand StartIsoExtractionCommand => Step1.StartIsoExtractionCommand;
    public IAsyncRelayCommand OpenWindows10DownloadCommand => Step1.OpenWindows10DownloadCommand;
    public IAsyncRelayCommand OpenWindows11DownloadCommand => Step1.OpenWindows11DownloadCommand;

    public IAsyncRelayCommand ConvertImageFormatCommand => ImageFormat.ConvertImageFormatCommand;
    public IAsyncRelayCommand DeleteWimCommand => ImageFormat.DeleteWimCommand;
    public IAsyncRelayCommand DeleteEsdCommand => ImageFormat.DeleteEsdCommand;

    public IAsyncRelayCommand GenerateWinhanceXmlCommand => Step2.GenerateWinhanceXmlCommand;
    public IAsyncRelayCommand DownloadUnattendedWinstallXmlCommand => Step2.DownloadUnattendedWinstallXmlCommand;
    public IAsyncRelayCommand SelectXmlFileCommand => Step2.SelectXmlFileCommand;
    public IAsyncRelayCommand OpenSchneegansXmlGeneratorCommand => Step2.OpenSchneegansXmlGeneratorCommand;

    public IAsyncRelayCommand ExtractAndAddSystemDriversCommand => Step3.ExtractAndAddSystemDriversCommand;
    public IAsyncRelayCommand SelectAndAddCustomDriversCommand => Step3.SelectAndAddCustomDriversCommand;

    public IAsyncRelayCommand DownloadOscdimgCommand => Step4.DownloadOscdimgCommand;
    public IRelayCommand SelectIsoOutputLocationCommand => Step4.SelectIsoOutputLocationCommand;
    public IAsyncRelayCommand CreateIsoCommand => Step4.CreateIsoCommand;

    // ── Constructor ──────────────────────────────────────────────────

    public WimUtilViewModel(
        IWimUtilService wimUtilService,
        ITaskProgressService taskProgressService,
        IDialogService dialogService,
        ILogService logService,
        IAutounattendXmlGeneratorService xmlGeneratorService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IProcessExecutor processExecutor,
        IFileSystemService fileSystemService,
        IFilePickerService filePickerService)
    {
        _wimUtilService = wimUtilService;
        _localizationService = localizationService;
        _dispatcherService = dispatcherService;
        _fileSystemService = fileSystemService;

        // Create sub-ViewModels
        Step1 = new WimStep1ViewModel(
            wimUtilService, taskProgressService, dialogService,
            localizationService, fileSystemService, filePickerService, logService);

        ImageFormat = new WimImageFormatViewModel(
            wimUtilService, taskProgressService, dialogService,
            dispatcherService, localizationService, logService);

        Step2 = new WimStep2XmlViewModel(
            xmlGeneratorService, wimUtilService,
            dialogService, localizationService, fileSystemService, filePickerService, logService);

        Step3 = new WimStep3DriversViewModel(
            wimUtilService, dialogService, localizationService,
            fileSystemService, filePickerService, logService);

        Step4 = new WimStep4IsoViewModel(
            wimUtilService, taskProgressService, processExecutor,
            dialogService, localizationService, fileSystemService, filePickerService, logService);

        // Initialize wizard state
        CurrentStep = 1;
        Step1State = new WizardStepState();
        Step2State = new WizardStepState();
        Step3State = new WizardStepState();
        Step4State = new WizardStepState();

        InitializeStepStates();

        // Subscribe to sub-VM property changes to update wizard states
        Step1.PropertyChanged += OnSubViewModelPropertyChanged;
        ImageFormat.PropertyChanged += OnSubViewModelPropertyChanged;
        Step2.PropertyChanged += OnSubViewModelPropertyChanged;
        Step3.PropertyChanged += OnSubViewModelPropertyChanged;
        Step4.PropertyChanged += OnSubViewModelPropertyChanged;
    }

    /// <summary>
    /// Kept for backward compatibility with WimUtilPage.xaml.cs.
    /// The IFilePickerService now handles window references internally via IMainWindowProvider.
    /// </summary>
    public void SetMainWindow(Microsoft.UI.Xaml.Window window)
    {
        // No-op: file picker now uses IMainWindowProvider internally.
    }

    public async Task OnNavigatedToAsync()
    {
        Step4.IsOscdimgAvailable = await _wimUtilService.IsOscdimgAvailableAsync();
        _dispatcherService.RunOnUIThread(Step4.UpdateDownloadOscdimgCardState);
        UpdateStepStates();
    }

    // ── Wizard navigation ────────────────────────────────────────────

    [RelayCommand]
    private void NavigateToStep(string? stepParameter)
    {
        if (string.IsNullOrEmpty(stepParameter) || !int.TryParse(stepParameter, out int targetStep)) return;

        if (targetStep == CurrentStep)
        {
            CurrentStep = 0;
            UpdateStepStates();
            return;
        }

        if (!IsStepAvailable(targetStep)) return;

        CurrentStep = targetStep;
        UpdateStepStates();
    }

    private bool IsStepAvailable(int step) => step switch
    {
        1 => true,
        2 or 3 or 4 => Step1.IsExtractionComplete && !ImageFormat.IsConverting,
        _ => false
    };

    // ── Step state management ────────────────────────────────────────

    private void InitializeStepStates()
    {
        Step1State = new WizardStepState
        {
            StepNumber = 1,
            Title = _localizationService.GetString("WIMUtil_Step1_Title") ?? "Select ISO",
            Icon = "DiscPlayer",
            StatusText = _localizationService.GetString("WIMUtil_Status_NoIsoSelected"),
            IsExpanded = true,
            IsAvailable = true
        };

        Step2State = new WizardStepState
        {
            StepNumber = 2,
            Title = _localizationService.GetString("WIMUtil_Step2_Title") ?? "Add XML File",
            Icon = "FileCode",
            StatusText = _localizationService.GetString("WIMUtil_Status_CompleteStep1")
        };

        Step3State = new WizardStepState
        {
            StepNumber = 3,
            Title = _localizationService.GetString("WIMUtil_Step3_Title") ?? "Add Drivers",
            Icon = "Chip",
            StatusText = _localizationService.GetString("WIMUtil_Status_CompleteStep1")
        };

        Step4State = new WizardStepState
        {
            StepNumber = 4,
            Title = _localizationService.GetString("WIMUtil_Step4_Title") ?? "Create ISO",
            Icon = "WrenchClock",
            StatusText = _localizationService.GetString("WIMUtil_Status_CompleteStep1")
        };
    }

    private void UpdateStepStates()
    {
        var extractionComplete = Step1.IsExtractionComplete;
        var isConverting = ImageFormat.IsConverting;
        var isExtracting = Step1.IsExtracting;

        Step1State.IsExpanded = CurrentStep == 1;
        Step1State.IsAvailable = true;
        Step1State.IsComplete = extractionComplete && !isConverting;
        Step1State.StatusText = isConverting
            ? _localizationService.GetString("WIMUtil_Status_Converting")
            : extractionComplete
                ? _localizationService.GetString("WIMUtil_Status_IsoExtracted")
                : isExtracting
                    ? _localizationService.GetString("WIMUtil_Status_Extracting")
                    : !string.IsNullOrEmpty(Step1.SelectedIsoPath)
                        ? _localizationService.GetString("WIMUtil_Status_IsoSelected")
                        : _localizationService.GetString("WIMUtil_Status_NoIsoSelected");

        Step2State.IsExpanded = CurrentStep == 2;
        Step2State.IsAvailable = extractionComplete && !isConverting;
        Step2State.IsComplete = Step2.IsXmlAdded;
        Step2State.StatusText = isConverting
            ? _localizationService.GetString("WIMUtil_Status_WaitForConversion")
            : !extractionComplete
                ? _localizationService.GetString("WIMUtil_Status_CompleteStep1")
                : Step2.IsXmlAdded
                    ? _localizationService.GetString("WIMUtil_Status_XmlAdded")
                    : _localizationService.GetString("WIMUtil_Status_NoXmlAdded");

        Step3State.IsExpanded = CurrentStep == 3;
        Step3State.IsAvailable = extractionComplete && !isConverting;
        Step3State.IsComplete = Step3.AreDriversAdded;
        Step3State.StatusText = isConverting
            ? _localizationService.GetString("WIMUtil_Status_WaitForConversion")
            : !extractionComplete
                ? _localizationService.GetString("WIMUtil_Status_CompleteStep1")
                : Step3.AreDriversAdded
                    ? _localizationService.GetString("WIMUtil_Status_DriversAdded")
                    : _localizationService.GetString("WIMUtil_Status_NoDriversAdded");

        Step4State.IsExpanded = CurrentStep == 4;
        Step4State.IsAvailable = extractionComplete && !isConverting;
        Step4State.IsComplete = Step4.IsIsoCreated;
        Step4State.StatusText = isConverting
            ? _localizationService.GetString("WIMUtil_Status_WaitForConversion")
            : !extractionComplete
                ? _localizationService.GetString("WIMUtil_Status_CompleteStep1")
                : Step4.IsIsoCreated
                    ? _localizationService.GetString("WIMUtil_Status_IsoCreated")
                    : !string.IsNullOrEmpty(Step4.OutputIsoPath)
                        ? $"{_localizationService.GetString("WIMUtil_Label_Output")}: {_fileSystemService.GetFileName(Step4.OutputIsoPath)}"
                        : _localizationService.GetString("WIMUtil_Status_ReadyToCreateIso");

        OnPropertyChanged(nameof(Step1State));
        OnPropertyChanged(nameof(Step2State));
        OnPropertyChanged(nameof(Step3State));
        OnPropertyChanged(nameof(Step4State));
    }

    // ── Sub-VM observation ───────────────────────────────────────────

    private void OnSubViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Propagate working directory to sub-VMs that need it
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
                    break;

                case nameof(WimStep1ViewModel.IsExtractionComplete):
                    if (Step1.IsExtractionComplete)
                    {
                        _ = ImageFormat.SafeDetectImageFormatAsync();
                    }
                    break;
            }
        }

        // Forward property changes so XAML bindings on the parent still work
        ForwardPropertyChange(sender, e);

        // Update wizard step states whenever completion flags change
        UpdateStepStates();
    }

    private void ForwardPropertyChange(object? sender, PropertyChangedEventArgs e)
    {
        // Raise PropertyChanged on the parent for forwarded properties
        if (sender == Step1)
        {
            switch (e.PropertyName)
            {
                case nameof(WimStep1ViewModel.SelectedIsoPath): OnPropertyChanged(nameof(SelectedIsoPath)); break;
                case nameof(WimStep1ViewModel.WorkingDirectory): OnPropertyChanged(nameof(WorkingDirectory)); break;
                case nameof(WimStep1ViewModel.CanStartExtraction): OnPropertyChanged(nameof(CanStartExtraction)); break;
                case nameof(WimStep1ViewModel.IsExtractionComplete): OnPropertyChanged(nameof(IsExtractionComplete)); break;
                case nameof(WimStep1ViewModel.IsExtracting): OnPropertyChanged(nameof(IsExtracting)); break;
                case nameof(WimStep1ViewModel.HasExtractedIsoAlready): OnPropertyChanged(nameof(HasExtractedIsoAlready)); break;
            }
        }
        else if (sender == ImageFormat)
        {
            switch (e.PropertyName)
            {
                case nameof(WimImageFormatViewModel.CurrentImageFormat): OnPropertyChanged(nameof(CurrentImageFormat)); break;
                case nameof(WimImageFormatViewModel.ShowConversionCard): OnPropertyChanged(nameof(ShowConversionCard)); break;
                case nameof(WimImageFormatViewModel.IsConverting): OnPropertyChanged(nameof(IsConverting)); break;
                case nameof(WimImageFormatViewModel.ConversionStatus): OnPropertyChanged(nameof(ConversionStatus)); break;
                case nameof(WimImageFormatViewModel.BothFormatsExist): OnPropertyChanged(nameof(BothFormatsExist)); break;
                case nameof(WimImageFormatViewModel.WimFileSize): OnPropertyChanged(nameof(WimFileSize)); break;
                case nameof(WimImageFormatViewModel.EsdFileSize): OnPropertyChanged(nameof(EsdFileSize)); break;
                case nameof(WimImageFormatViewModel.DetectionResult): OnPropertyChanged(nameof(DetectionResult)); break;
            }
        }
        else if (sender == Step2)
        {
            switch (e.PropertyName)
            {
                case nameof(WimStep2XmlViewModel.SelectedXmlPath): OnPropertyChanged(nameof(SelectedXmlPath)); break;
                case nameof(WimStep2XmlViewModel.XmlStatus): OnPropertyChanged(nameof(XmlStatus)); break;
                case nameof(WimStep2XmlViewModel.IsXmlAdded): OnPropertyChanged(nameof(IsXmlAdded)); break;
            }
        }
        else if (sender == Step3)
        {
            switch (e.PropertyName)
            {
                case nameof(WimStep3DriversViewModel.AreDriversAdded): OnPropertyChanged(nameof(AreDriversAdded)); break;
            }
        }
        else if (sender == Step4)
        {
            switch (e.PropertyName)
            {
                case nameof(WimStep4IsoViewModel.IsOscdimgAvailable): OnPropertyChanged(nameof(IsOscdimgAvailable)); break;
                case nameof(WimStep4IsoViewModel.OutputIsoPath): OnPropertyChanged(nameof(OutputIsoPath)); break;
                case nameof(WimStep4IsoViewModel.IsIsoCreated): OnPropertyChanged(nameof(IsIsoCreated)); break;
            }
        }
    }
}
