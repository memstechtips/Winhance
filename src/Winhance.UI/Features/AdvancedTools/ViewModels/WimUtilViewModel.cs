using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.AdvancedTools.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.Features.AdvancedTools.ViewModels;

public partial class WimUtilViewModel : ObservableObject, IDisposable
{
    private readonly ILocalizationService _localizationService;
    private readonly IFileSystemService _fileSystemService;
    private bool _disposed;

    // Steps 2-4 auto-expand once per completed extraction; the flag stops a
    // later refresh from re-opening steps the user has manually collapsed.
    private bool _autoExpandApplied;

    public WimStep1ViewModel Step1 { get; }
    public WimImageFormatViewModel ImageFormat { get; }
    public WimStep2XmlViewModel Step2 { get; }
    public WimStep3DriversViewModel Step3 { get; }
    public WimStep4IsoViewModel Step4 { get; }

    [ObservableProperty]
    public partial WizardStepState Step1State { get; set; }

    [ObservableProperty]
    public partial WizardStepState Step2State { get; set; }

    [ObservableProperty]
    public partial WizardStepState Step3State { get; set; }

    [ObservableProperty]
    public partial WizardStepState Step4State { get; set; }

    public string Title => _localizationService.GetStringOrDefault("WIMUtil_Title", "Windows Installation Media Utility");
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
        Step4.IsUsbDestination ? "WIMUtil_ButtonWriteUsb" : "WIMUtil_ButtonCreateISO");

    public string DestinationCardTitle => _localizationService.GetString("WIMUtil_Card_Destination_Title");

    public string DestinationCardDescription => _localizationService.GetString("WIMUtil_Card_Destination_Description");

    public string DestinationIsoText => _localizationService.GetString("WIMUtil_Destination_Iso");

    public string DestinationUsbText => _localizationService.GetString("WIMUtil_Destination_Usb");

    public string NoUsbSelectedText => _localizationService.GetString("WIMUtil_Label_NoUsbSelected");

    public string UsbEraseWarningText => _localizationService.GetString("WIMUtil_Msg_UsbEraseWarning");

    // Forwarders so existing XAML can keep binding to ViewModel.PropertyName instead of the sub-VMs.
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

    public string OutputIsoPath => Step4.OutputIsoPath;
    public bool IsIsoCreated => Step4.IsIsoCreated;
    public bool IsIsoDestination => Step4.IsIsoDestination;
    public bool IsUsbDestination => Step4.IsUsbDestination;
    public WizardActionCard SelectOutputCard => Step4.SelectOutputCard;
    public WizardActionCard SelectUsbCard => Step4.SelectUsbCard;
    public ObservableCollection<RemovableDrive> UsbTargets => Step4.UsbTargets;

    public RemovableDrive? SelectedUsbTarget
    {
        get => Step4.SelectedUsbTarget;
        set => Step4.SelectedUsbTarget = value;
    }

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

    public IRelayCommand SelectIsoOutputLocationCommand => Step4.SelectIsoOutputLocationCommand;
    public IAsyncRelayCommand CreateMediaCommand => Step4.CreateMediaCommand;
    public IRelayCommand SelectIsoDestinationCommand => Step4.SelectIsoDestinationCommand;
    public IAsyncRelayCommand SelectUsbDestinationCommand => Step4.SelectUsbDestinationCommand;

    public WimUtilViewModel(
        IIsoService isoService,
        IUsbMediaWriter usbMediaWriter,
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
        IResourceService resourceService)
    {
        _localizationService = localizationService;
        _fileSystemService = fileSystemService;

        Step1 = new WimStep1ViewModel(
            isoService, taskProgressService, dialogService,
            localizationService, fileSystemService, filePickerService, logService,
            resourceService);

        ImageFormat = new WimImageFormatViewModel(
            wimImageService, taskProgressService, dialogService,
            dispatcherService, localizationService, logService);

        Step2 = new WimStep2XmlViewModel(
            saves, wimCustomizationService, selections,
            dialogService, localizationService, fileSystemService, filePickerService, logService,
            resourceService);

        Step3 = new WimStep3DriversViewModel(
            wimCustomizationService, taskProgressService, dialogService,
            localizationService, fileSystemService, filePickerService, logService,
            resourceService);

        Step4 = new WimStep4IsoViewModel(
            wimCustomizationService, isoService, usbMediaWriter, taskProgressService, processExecutor,
            dialogService, localizationService, fileSystemService, filePickerService, logService);

        Step1State = new WizardStepState();
        Step2State = new WizardStepState();
        Step3State = new WizardStepState();
        Step4State = new WizardStepState();

        InitializeStepStates();

        _localizationService.LanguageChanged += OnLanguageChanged;

        Step1.PropertyChanged += OnSubViewModelPropertyChanged;
        ImageFormat.PropertyChanged += OnSubViewModelPropertyChanged;
        Step2.PropertyChanged += OnSubViewModelPropertyChanged;
        Step3.PropertyChanged += OnSubViewModelPropertyChanged;
        Step4.PropertyChanged += OnSubViewModelPropertyChanged;

        // Propagate Step1's initial WorkingDirectory to sub-VMs.
        // The constructor assignment above fires before subscriptions,
        // so without this, sub-VMs never receive the default value.
        var initialWorkingDir = Step1.WorkingDirectory;
        ImageFormat.WorkingDirectory = initialWorkingDir;
        Step2.WorkingDirectory = initialWorkingDir;
        Step3.WorkingDirectory = initialWorkingDir;
        Step4.WorkingDirectory = initialWorkingDir;
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
        _ => null
    };

    private bool IsStepAvailable(int step) => step switch
    {
        1 => true,
        2 or 3 or 4 => Step1.IsExtractionComplete && !ImageFormat.IsConverting,
        _ => false
    };

    private void InitializeStepStates()
    {
        Step1State = new WizardStepState
        {
            StepNumber = 1,
            Title = _localizationService.GetStringOrDefault("WIMUtil_Step1_Title", "Select ISO"),
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
            Title = _localizationService.GetStringOrDefault("WIMUtil_Step3_Title", "Add Drivers"),
            Icon = "Chip",
            StatusText = _localizationService.GetString("WIMUtil_Status_CompleteStep1")
        };

        Step4State = new WizardStepState
        {
            StepNumber = 4,
            Title = _localizationService.GetStringOrDefault("WIMUtil_Step4_Title", "Create ISO"),
            Icon = "WrenchClock",
            StatusText = _localizationService.GetString("WIMUtil_Status_CompleteStep1")
        };
    }

    private void UpdateStepStates()
    {
        var extractionComplete = Step1.IsExtractionComplete;
        var isConverting = ImageFormat.IsConverting;
        var isExtracting = Step1.IsExtracting;

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

        Step2State.IsAvailable = extractionComplete && !isConverting;
        Step2State.IsComplete = Step2.IsXmlAdded;
        Step2State.StatusText = isConverting
            ? _localizationService.GetString("WIMUtil_Status_WaitForConversion")
            : !extractionComplete
                ? _localizationService.GetString("WIMUtil_Status_CompleteStep1")
                : Step2.IsXmlAdded
                    ? _localizationService.GetString("WIMUtil_Status_XmlAdded")
                    : _localizationService.GetString("WIMUtil_Status_NoXmlAdded");

        Step3State.IsAvailable = extractionComplete && !isConverting;
        Step3State.IsComplete = Step3.AreDriversAdded;
        Step3State.StatusText = isConverting
            ? _localizationService.GetString("WIMUtil_Status_WaitForConversion")
            : !extractionComplete
                ? _localizationService.GetString("WIMUtil_Status_CompleteStep1")
                : Step3.AreDriversAdded
                    ? _localizationService.GetString("WIMUtil_Status_DriversAdded")
                    : _localizationService.GetString("WIMUtil_Status_NoDriversAdded");

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
                    break;

                case nameof(WimStep1ViewModel.IsExtractionComplete):
                    if (Step1.IsExtractionComplete)
                    {
                        _ = ImageFormat.SafeDetectImageFormatAsync();
                    }
                    RefreshStepExpansion();
                    break;
            }
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
                case nameof(WimStep4IsoViewModel.OutputIsoPath): OnPropertyChanged(nameof(OutputIsoPath)); break;
                case nameof(WimStep4IsoViewModel.IsIsoCreated): OnPropertyChanged(nameof(IsIsoCreated)); break;
                case nameof(WimStep4IsoViewModel.SelectedUsbTarget): OnPropertyChanged(nameof(SelectedUsbTarget)); break;
                case nameof(WimStep4IsoViewModel.IsUsbDestination):
                    OnPropertyChanged(nameof(IsIsoDestination));
                    OnPropertyChanged(nameof(IsUsbDestination));
                    OnPropertyChanged(nameof(ButtonCreateMediaText));
                    break;
            }
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Step1State.Title = _localizationService.GetStringOrDefault("WIMUtil_Step1_Title", "Select ISO");
        Step2State.Title = _localizationService.GetStringOrDefault("WIMUtil_Step2_Title", "Add XML File");
        Step3State.Title = _localizationService.GetStringOrDefault("WIMUtil_Step3_Title", "Add Drivers");
        Step4State.Title = _localizationService.GetStringOrDefault("WIMUtil_Step4_Title", "Create ISO");
        UpdateStepStates();

        OnPropertyChanged(nameof(Title));
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

        (Step2 as IDisposable)?.Dispose();
        (Step3 as IDisposable)?.Dispose();
        (Step4 as IDisposable)?.Dispose();
        (ImageFormat as IDisposable)?.Dispose();
        GC.SuppressFinalize(this);
    }
}
