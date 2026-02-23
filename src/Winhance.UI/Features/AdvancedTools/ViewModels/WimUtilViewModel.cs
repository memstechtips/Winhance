using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.AdvancedTools.Models;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;
using Application = Microsoft.UI.Xaml.Application;

namespace Winhance.UI.Features.AdvancedTools.ViewModels;

/// <summary>
/// ViewModel for the WIM Utility wizard.
/// </summary>
public partial class WimUtilViewModel : ObservableObject
{
    private readonly IWimUtilService _wimUtilService;
    private readonly ITaskProgressService _taskProgressService;
    private readonly IDialogService _dialogService;
    private readonly ILogService _logService;
    private readonly IAutounattendXmlGeneratorService _xmlGeneratorService;
    private readonly ILocalizationService _localizationService;
    private readonly IDispatcherService _dispatcherService;
    private readonly IProcessExecutor _processExecutor;
    private CancellationTokenSource? _cancellationTokenSource;

    private Window? _mainWindow;

    public string Title => _localizationService?.GetString("WIMUtil_Title") ?? "Windows Installation Media Utility";

    // XAML-bound localized labels
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

    [ObservableProperty]
    public partial string SelectedIsoPath { get; set; }

    [ObservableProperty]
    public partial string WorkingDirectory { get; set; }

    [ObservableProperty]
    public partial bool CanStartExtraction { get; set; }

    [ObservableProperty]
    public partial bool IsExtractionComplete { get; set; }

    [ObservableProperty]
    public partial bool IsExtracting { get; set; }

    [ObservableProperty]
    public partial bool HasExtractedIsoAlready { get; set; }

    [ObservableProperty]
    public partial ImageFormatInfo? CurrentImageFormat { get; set; }

    [ObservableProperty]
    public partial bool ShowConversionCard { get; set; }

    [ObservableProperty]
    public partial bool IsConverting { get; set; }

    [ObservableProperty]
    public partial string ConversionStatus { get; set; }

    [ObservableProperty]
    public partial bool BothFormatsExist { get; set; }

    [ObservableProperty]
    public partial string WimFileSize { get; set; }

    [ObservableProperty]
    public partial string EsdFileSize { get; set; }

    [ObservableProperty]
    public partial ImageDetectionResult? DetectionResult { get; set; }

    [ObservableProperty]
    public partial string SelectedXmlPath { get; set; }

    [ObservableProperty]
    public partial string XmlStatus { get; set; }

    [ObservableProperty]
    public partial bool IsXmlAdded { get; set; }

    [ObservableProperty]
    public partial bool AreDriversAdded { get; set; }

    [ObservableProperty]
    public partial string OutputIsoPath { get; set; }

    [ObservableProperty]
    public partial bool IsOscdimgAvailable { get; set; }

    [ObservableProperty]
    public partial bool IsIsoCreated { get; set; }

    public WizardActionCard SelectIsoCard { get; private set; } = new();
    public WizardActionCard SelectDirectoryCard { get; private set; } = new();
    public WizardActionCard ConvertImageCard { get; private set; } = new();
    public WizardActionCard GenerateWinhanceXmlCard { get; private set; } = new();
    public WizardActionCard DownloadXmlCard { get; private set; } = new();
    public WizardActionCard SelectXmlCard { get; private set; } = new();
    public WizardActionCard ExtractSystemDriversCard { get; private set; } = new();
    public WizardActionCard SelectCustomDriversCard { get; private set; } = new();
    public WizardActionCard DownloadOscdimgCard { get; private set; } = new();
    public WizardActionCard SelectOutputCard { get; private set; } = new();

    public WimUtilViewModel(
        IWimUtilService wimUtilService,
        ITaskProgressService taskProgressService,
        IDialogService dialogService,
        ILogService logService,
        IAutounattendXmlGeneratorService xmlGeneratorService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IProcessExecutor processExecutor)
    {
        _wimUtilService = wimUtilService;
        _taskProgressService = taskProgressService;
        _dialogService = dialogService;
        _logService = logService;
        _xmlGeneratorService = xmlGeneratorService;
        _processExecutor = processExecutor;
        _localizationService = localizationService;
        _dispatcherService = dispatcherService;

        // Initialize partial property defaults
        CurrentStep = 1;
        Step1State = new WizardStepState();
        Step2State = new WizardStepState();
        Step3State = new WizardStepState();
        Step4State = new WizardStepState();
        SelectedIsoPath = string.Empty;
        ConversionStatus = string.Empty;
        WimFileSize = string.Empty;
        EsdFileSize = string.Empty;
        SelectedXmlPath = string.Empty;
        OutputIsoPath = string.Empty;

        XmlStatus = _localizationService.GetString("WIMUtil_Status_NoXmlAdded");
        WorkingDirectory = Path.Combine(Path.GetTempPath(), "WinhanceWIM");

        InitializeStepStates();
        CreateActionCards();
    }

    public void SetMainWindow(Window window) => _mainWindow = window;

    public async Task OnNavigatedToAsync()
    {
        IsOscdimgAvailable = await _wimUtilService.IsOscdimgAvailableAsync();
        _dispatcherService.RunOnUIThread(UpdateDownloadOscdimgCardState);
        UpdateStepStates();
    }

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

    private void CreateActionCards()
    {
        SelectIsoCard = new WizardActionCard
        {
            Icon = "\uE958",
            Title = _localizationService.GetString("WIMUtil_Card_SelectISO_Title"),
            Description = _localizationService.GetString("WIMUtil_Label_NoSelection"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_SelectISO_Button"),
            ButtonCommand = SelectIsoFileCommand,
            IsEnabled = true
        };

        SelectDirectoryCard = new WizardActionCard
        {
            IconPath = GetResourceIconPath("ExplorerIconPath"),
            Title = _localizationService.GetString("WIMUtil_Card_SelectDirectory_Title"),
            Description = string.Format(_localizationService.GetString("WIMUtil_Card_SelectDirectory_Description_Default"), Path.Combine(Path.GetTempPath(), "WinhanceWIM")),
            ButtonText = _localizationService.GetString("WIMUtil_Card_SelectDirectory_Button"),
            ButtonCommand = SelectWorkingDirectoryCommand,
            IsEnabled = true
        };

        GenerateWinhanceXmlCard = new WizardActionCard
        {
            Icon = "\uE710",
            Title = _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Title"),
            Description = _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Description"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Button"),
            ButtonCommand = GenerateWinhanceXmlCommand,
            IsEnabled = true
        };

        DownloadXmlCard = new WizardActionCard
        {
            IconPath = GetResourceIconPath("FileDownloadIconPath"),
            Title = _localizationService.GetString("WIMUtil_Card_DownloadXML_Title"),
            Description = _localizationService.GetString("WIMUtil_Card_DownloadXML_Description"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_DownloadXML_Button"),
            ButtonCommand = DownloadUnattendedWinstallXmlCommand,
            IsEnabled = true
        };

        SelectXmlCard = new WizardActionCard
        {
            IconPath = GetResourceIconPath("AutounattendXmlIconPath"),
            Title = _localizationService.GetString("WIMUtil_Card_SelectXML_Title"),
            Description = _localizationService.GetString("WIMUtil_Card_SelectXML_Description"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_SelectXML_Button"),
            ButtonCommand = SelectXmlFileCommand,
            IsEnabled = true
        };

        ExtractSystemDriversCard = new WizardActionCard
        {
            IconPath = GetResourceIconPath("MemoryArrowDownIconPath"),
            Title = _localizationService.GetString("WIMUtil_Card_ExtractDrivers_Title"),
            Description = _localizationService.GetString("WIMUtil_Card_ExtractDrivers_Description"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_ExtractDrivers_Button"),
            ButtonCommand = ExtractAndAddSystemDriversCommand,
            IsEnabled = true
        };

        SelectCustomDriversCard = new WizardActionCard
        {
            IconPath = GetResourceIconPath("ExplorerIconPath"),
            Title = _localizationService.GetString("WIMUtil_Card_CustomDrivers_Title"),
            Description = _localizationService.GetString("WIMUtil_Card_CustomDrivers_Description"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_CustomDrivers_Button"),
            ButtonCommand = SelectAndAddCustomDriversCommand,
            IsEnabled = true
        };

        DownloadOscdimgCard = new WizardActionCard
        {
            IconPath = GetResourceIconPath("ToolBoxIconPath"),
            Title = _localizationService.GetString("WIMUtil_Card_DownloadOscdimg_Title"),
            Description = _localizationService.GetString("WIMUtil_Card_DownloadOscdimg_Description"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_DownloadOscdimg_Button"),
            ButtonCommand = DownloadOscdimgCommand,
            IsEnabled = true
        };

        SelectOutputCard = new WizardActionCard
        {
            Icon = "\uE74E",
            Title = _localizationService.GetString("WIMUtil_Card_SelectOutput_Title"),
            Description = _localizationService.GetString("WIMUtil_Label_NoLocation"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_SelectOutput_Button"),
            ButtonCommand = SelectIsoOutputLocationCommand,
            IsEnabled = true
        };

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
    private void SelectIsoFile()
    {
        if (_mainWindow == null) return;

        var path = Win32FileDialogHelper.ShowOpenFilePicker(_mainWindow, _localizationService.GetString("WIMUtil_FileDialog_SelectIso"), "ISO Files", "*.iso");
        if (!string.IsNullOrEmpty(path))
        {
            SelectedIsoPath = path;
            SelectIsoCard.Description = SelectedIsoPath;
            CanStartExtraction = !string.IsNullOrEmpty(SelectedIsoPath) && !string.IsNullOrEmpty(WorkingDirectory);
        }
    }

    [RelayCommand]
    private async Task SelectWorkingDirectory()
    {
        if (_mainWindow == null) return;

        var description = HasExtractedIsoAlready
            ? _localizationService.GetString("WIMUtil_FolderDialog_SelectExtracted")
            : _localizationService.GetString("WIMUtil_FolderDialog_SelectWorkDir");
        var selectedPath = Win32FileDialogHelper.ShowFolderPicker(_mainWindow, description);

        if (string.IsNullOrEmpty(selectedPath)) return;

        if (HasExtractedIsoAlready)
        {
            var isValid = await ValidateExtractedIsoDirectory(selectedPath);
            if (isValid)
            {
                WorkingDirectory = selectedPath;
                SelectDirectoryCard.Description = $"{_localizationService.GetString("WIMUtil_Label_Using")}: {WorkingDirectory}";
                IsExtractionComplete = true;
                UpdateStepStates();
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetString("WIMUtil_Msg_ValidationComplete"),
                    _localizationService.GetString("WIMUtil_SelectWorkingDirectory"));
            }
            else
            {
                WorkingDirectory = string.Empty;
                SelectDirectoryCard.Description = _localizationService.GetString("WIMUtil_Error_InvalidDirectory");
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_InvalidDirectory"),
                    _localizationService.GetString("WIMUtil_Error_InvalidDirectory"));
            }
        }
        else
        {
            WorkingDirectory = Path.Combine(selectedPath, "WinhanceWIM");
            try
            {
                Directory.CreateDirectory(WorkingDirectory);
                SelectDirectoryCard.Description = $"{_localizationService.GetString("WIMUtil_Label_Using")}: {WorkingDirectory}";
                CanStartExtraction = !string.IsNullOrEmpty(SelectedIsoPath) && !string.IsNullOrEmpty(WorkingDirectory);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to create working directory: {ex.Message}", ex);
                SelectDirectoryCard.Description = _localizationService.GetString("WIMUtil_Error_DirectoryCreateFailed");
                WorkingDirectory = string.Empty;
            }
        }
    }

    private async Task<bool> ValidateExtractedIsoDirectory(string path)
    {
        try
        {
            var pathRoot = Path.GetPathRoot(path);
            if (!string.IsNullOrEmpty(pathRoot) && path.TrimEnd('\\', '/').Equals(pathRoot.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                return false;

            var driveInfo = new DriveInfo(path);
            if (driveInfo.DriveType == DriveType.CDRom) return false;

            var testFile = Path.Combine(path, $".winhance_write_test_{Guid.NewGuid()}.tmp");
            try
            {
                await File.WriteAllTextAsync(testFile, "test");
                File.Delete(testFile);
            }
            catch (Exception ex) { _logService.LogDebug($"Write test failed for directory '{path}': {ex.Message}"); return false; }

            var extractedDirs = Directory.GetDirectories(path);
            var hasSourcesDir = extractedDirs.Any(d => Path.GetFileName(d)?.Equals("sources", StringComparison.OrdinalIgnoreCase) == true);
            var hasBootDir = extractedDirs.Any(d => Path.GetFileName(d)?.Equals("boot", StringComparison.OrdinalIgnoreCase) == true);

            return hasSourcesDir && hasBootDir;
        }
        catch (Exception ex) { _logService.LogDebug($"ISO directory validation failed for '{path}': {ex.Message}"); return false; }
    }

    [RelayCommand]
    private async Task StartIsoExtraction()
    {
        try
        {
            SelectIsoCard.IsEnabled = false;
            SelectIsoCard.Opacity = 0.5;
            SelectDirectoryCard.IsEnabled = false;
            SelectDirectoryCard.Opacity = 0.5;
            IsExtracting = true;
            UpdateStepStates();

            _taskProgressService.StartTask(_localizationService.GetString("WIMUtil_Status_Extracting"), true);
            var progress = _taskProgressService.CreatePowerShellProgress();

            var success = await _wimUtilService.ExtractIsoAsync(SelectedIsoPath, WorkingDirectory, progress, _taskProgressService.CurrentTaskCancellationSource!.Token);

            ResetExtractionState();

            if (success)
            {
                SelectIsoCard.IsComplete = true;
                SelectIsoCard.Description = _localizationService.GetString("WIMUtil_Status_IsoExtractionSuccess");
                SelectIsoCard.DescriptionForeground = new SolidColorBrush(Color.FromArgb(255, 27, 94, 32));
                IsExtractionComplete = true;
                UpdateStepStates();
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetString("WIMUtil_Msg_ExtractionComplete"),
                    _localizationService.GetString("WIMUtil_Status_IsoExtractionSuccess"));
            }
            else
            {
                SelectIsoCard.HasFailed = true;
                SelectIsoCard.Description = _localizationService.GetString("WIMUtil_Status_IsoExtractionFailed");
                SelectIsoCard.DescriptionForeground = new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_ExtractionFailed"),
                    _localizationService.GetString("WIMUtil_Status_IsoExtractionFailed"));
            }
        }
        catch (OperationCanceledException)
        {
            ResetExtractionState();
            SelectIsoCard.Description = _localizationService.GetString("WIMUtil_Status_IsoExtractionCancelled");
        }
        catch (InsufficientDiskSpaceException spaceEx)
        {
            ResetExtractionState();
            SelectIsoCard.HasFailed = true;
            SelectIsoCard.Description = string.Format(_localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace"), spaceEx.DriveName);
            await _dialogService.ShowWarningAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_InsufficientSpace"), spaceEx.DriveName, spaceEx.RequiredGB.ToString("F2"), spaceEx.AvailableGB.ToString("F2"), (spaceEx.RequiredGB - spaceEx.AvailableGB).ToString("F2")),
                string.Format(_localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace"), spaceEx.DriveName));
        }
        catch (Exception ex)
        {
            ResetExtractionState();
            SelectIsoCard.HasFailed = true;
            SelectIsoCard.Description = string.Format(_localizationService.GetString("WIMUtil_Status_ErrorPrefix"), ex.Message);
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_ExtractionError"), ex.Message),
                "Error");
        }
        finally
        {
            _taskProgressService.CompleteTask();
        }
    }

    private void ResetExtractionState()
    {
        SelectIsoCard.IsEnabled = true;
        SelectIsoCard.Opacity = 1.0;
        SelectDirectoryCard.IsEnabled = true;
        SelectDirectoryCard.Opacity = 1.0;
        IsExtracting = false;
        UpdateStepStates();
    }

    [RelayCommand]
    private async Task GenerateWinhanceXml()
    {
        try
        {
            GenerateWinhanceXmlCard.IsComplete = false;
            GenerateWinhanceXmlCard.HasFailed = false;

            var confirmed = await _dialogService.ShowConfirmationAsync(
                _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Description"),
                _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Title"),
                "Yes", "No");
            if (!confirmed) return;

            XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlGenerating");
            var outputPath = Path.Combine(WorkingDirectory, "autounattend.xml");
            var generatedPath = await _xmlGeneratorService.GenerateFromCurrentSelectionsAsync(outputPath);

            SelectedXmlPath = generatedPath;
            IsXmlAdded = true;
            XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlGenSuccess");
            ClearOtherXmlCardCompletions("generate");
            GenerateWinhanceXmlCard.IsComplete = true;
            UpdateStepStates();
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error generating XML: {ex.Message}", ex);
            XmlStatus = string.Format(_localizationService.GetString("WIMUtil_Status_XmlGenFailed"), ex.Message);
            GenerateWinhanceXmlCard.HasFailed = true;
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_XmlGenError"), ex.Message),
                "Error");
        }
    }

    [RelayCommand]
    private async Task DownloadUnattendedWinstallXml()
    {
        try
        {
            DownloadXmlCard.IsComplete = false;
            DownloadXmlCard.HasFailed = false;

            var destinationPath = Path.Combine(WorkingDirectory, "autounattend.xml");
            _cancellationTokenSource = new CancellationTokenSource();
            var progress = new Progress<TaskProgressDetail>(detail => XmlStatus = detail.StatusText ?? _localizationService.GetString("WIMUtil_Status_XmlDownloading"));

            XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlDownloadStart");
            await _wimUtilService.DownloadUnattendedWinstallXmlAsync(destinationPath, progress, _cancellationTokenSource.Token);

            var addSuccess = await _wimUtilService.AddXmlToImageAsync(destinationPath, WorkingDirectory);
            if (addSuccess)
            {
                SelectedXmlPath = destinationPath;
                IsXmlAdded = true;
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlDownloadSuccess");
                ClearOtherXmlCardCompletions("download");
                DownloadXmlCard.IsComplete = true;
                UpdateStepStates();
            }
            else
            {
                DownloadXmlCard.HasFailed = true;
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlAddFailed");
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_XmlAddFailed"),
                    "Error");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error downloading XML: {ex.Message}", ex);
            XmlStatus = string.Format(_localizationService.GetString("WIMUtil_Status_XmlDownloadFailed"), ex.Message);
            DownloadXmlCard.HasFailed = true;
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_XmlDownloadError"), ex.Message),
                "Error");
        }
    }

    [RelayCommand]
    private async Task SelectXmlFile()
    {
        if (_mainWindow == null) return;

        try
        {
            SelectXmlCard.IsComplete = false;
            SelectXmlCard.HasFailed = false;

            var selectedPath = Win32FileDialogHelper.ShowOpenFilePicker(_mainWindow, _localizationService.GetString("WIMUtil_FileDialog_SelectXml"), "XML Files", "*.xml");
            if (string.IsNullOrEmpty(selectedPath)) return;

            XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlValidating");
            var isValidXml = await ValidateXmlFile(selectedPath);
            if (!isValidXml)
            {
                SelectXmlCard.HasFailed = true;
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlInvalid");
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_XmlInvalidError"),
                    "Error");
                return;
            }

            XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlAdding");
            var addSuccess = await _wimUtilService.AddXmlToImageAsync(selectedPath, WorkingDirectory);
            if (addSuccess)
            {
                SelectedXmlPath = selectedPath;
                IsXmlAdded = true;
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlSelectSuccess");
                ClearOtherXmlCardCompletions("select");
                SelectXmlCard.IsComplete = true;
                UpdateStepStates();
            }
            else
            {
                SelectXmlCard.HasFailed = true;
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlValidAddFailed");
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_XmlValidAddFailed"),
                    "Error");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error selecting XML: {ex.Message}", ex);
            XmlStatus = string.Format(_localizationService.GetString("WIMUtil_Status_ErrorPrefix"), ex.Message);
            SelectXmlCard.HasFailed = true;
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_XmlSelectError"), ex.Message),
                "Error");
        }
    }

    private async Task<bool> ValidateXmlFile(string xmlPath)
    {
        try
        {
            await Task.Run(() => XDocument.Load(xmlPath));
            return true;
        }
        catch (Exception ex) { _logService.LogDebug($"XML validation failed for '{xmlPath}': {ex.Message}"); return false; }
    }

    private void ClearOtherXmlCardCompletions(string exceptCard)
    {
        if (exceptCard != "generate") GenerateWinhanceXmlCard.IsComplete = false;
        if (exceptCard != "download") DownloadXmlCard.IsComplete = false;
        if (exceptCard != "select") SelectXmlCard.IsComplete = false;
    }

    [RelayCommand]
    private async Task OpenSchneegansXmlGenerator()
    {
        try { await Windows.System.Launcher.LaunchUriAsync(new Uri("https://schneegans.de/windows/unattend-generator/")); }
        catch (Exception ex) { _logService.LogError($"Error opening Schneegans XML generator: {ex.Message}", ex); }
    }

    [RelayCommand]
    private async Task ExtractAndAddSystemDrivers()
    {
        try
        {
            ExtractSystemDriversCard.IsComplete = false;
            ExtractSystemDriversCard.HasFailed = false;

            var confirmed = await _dialogService.ShowConfirmationAsync(
                _localizationService.GetString("WIMUtil_Msg_ExtractDriversConfirm"),
                _localizationService.GetString("WIMUtil_Card_ExtractDrivers_Title"),
                "Yes", "No");
            if (!confirmed) return;

            ExtractSystemDriversCard.IsProcessing = true;
            ExtractSystemDriversCard.IsEnabled = false;

            _cancellationTokenSource = new CancellationTokenSource();
            var progress = new Progress<TaskProgressDetail>(detail => { });

            var success = await _wimUtilService.AddDriversAsync(WorkingDirectory, null, progress, _cancellationTokenSource.Token);

            ExtractSystemDriversCard.IsProcessing = false;
            ExtractSystemDriversCard.IsEnabled = true;

            if (success)
            {
                AreDriversAdded = true;
                ExtractSystemDriversCard.IsComplete = true;
                UpdateStepStates();
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetString("WIMUtil_Msg_DriversSuccess"),
                    "Success");
            }
            else
            {
                ExtractSystemDriversCard.HasFailed = true;
                await _dialogService.ShowWarningAsync(
                    _localizationService.GetString("WIMUtil_Msg_NoDriversFound"),
                    "Warning");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error extracting system drivers: {ex.Message}", ex);
            ExtractSystemDriversCard.IsProcessing = false;
            ExtractSystemDriversCard.IsEnabled = true;
            ExtractSystemDriversCard.HasFailed = true;
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_DriverExtractionError"), ex.Message),
                "Error");
        }
    }

    [RelayCommand]
    private async Task SelectAndAddCustomDrivers()
    {
        if (_mainWindow == null) return;

        try
        {
            SelectCustomDriversCard.IsComplete = false;
            SelectCustomDriversCard.HasFailed = false;

            var selectedPath = Win32FileDialogHelper.ShowFolderPicker(_mainWindow, _localizationService.GetString("WIMUtil_FolderDialog_SelectDrivers"));
            if (string.IsNullOrEmpty(selectedPath)) return;

            if (!Directory.Exists(selectedPath))
            {
                SelectCustomDriversCard.HasFailed = true;
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_InvalidFolder"),
                    "Error");
                return;
            }

            var hasFiles = Directory.EnumerateFileSystemEntries(selectedPath, "*", SearchOption.AllDirectories).Any();
            if (!hasFiles)
            {
                SelectCustomDriversCard.HasFailed = true;
                await _dialogService.ShowWarningAsync(
                    _localizationService.GetString("WIMUtil_Msg_EmptyFolder"),
                    "Warning");
                return;
            }

            SelectCustomDriversCard.IsProcessing = true;
            SelectCustomDriversCard.IsEnabled = false;
            SelectCustomDriversCard.Description = $"{_localizationService.GetString("WIMUtil_Label_Selected")}: {selectedPath}";

            _cancellationTokenSource = new CancellationTokenSource();
            var progress = new Progress<TaskProgressDetail>(detail => { });

            var success = await _wimUtilService.AddDriversAsync(WorkingDirectory, selectedPath, progress, _cancellationTokenSource.Token);

            SelectCustomDriversCard.IsProcessing = false;
            SelectCustomDriversCard.IsEnabled = true;

            if (success)
            {
                AreDriversAdded = true;
                SelectCustomDriversCard.IsComplete = true;
                UpdateStepStates();
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetString("WIMUtil_Msg_DriverFilesAdded"),
                    "Success");
            }
            else
            {
                SelectCustomDriversCard.HasFailed = true;
                await _dialogService.ShowWarningAsync(
                    string.Format(_localizationService.GetString("WIMUtil_Msg_NoCustomDrivers"), selectedPath),
                    "Warning");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error adding custom drivers: {ex.Message}", ex);
            SelectCustomDriversCard.IsProcessing = false;
            SelectCustomDriversCard.IsEnabled = true;
            SelectCustomDriversCard.HasFailed = true;
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_DriverAdditionError"), ex.Message),
                "Error");
        }
    }

    [RelayCommand]
    private async Task DownloadOscdimg()
    {
        try
        {
            DownloadOscdimgCard.IsComplete = false;
            DownloadOscdimgCard.HasFailed = false;
            DownloadOscdimgCard.IsProcessing = true;
            DownloadOscdimgCard.IsEnabled = false;

            _cancellationTokenSource = new CancellationTokenSource();
            var progress = new Progress<TaskProgressDetail>(detail => { });

            var success = await _wimUtilService.EnsureOscdimgAvailableAsync(progress, _cancellationTokenSource.Token);

            DownloadOscdimgCard.IsProcessing = false;

            if (success)
            {
                IsOscdimgAvailable = true;
                DownloadOscdimgCard.IsComplete = true;
                DownloadOscdimgCard.IsEnabled = false;
                DownloadOscdimgCard.ButtonText = _localizationService.GetString("WIMUtil_Button_OscdimgFound");
                DownloadOscdimgCard.Description = _localizationService.GetString("WIMUtil_Desc_OscdimgInstalled");
                DownloadOscdimgCard.IconPath = GetResourceIconPath("CheckCircleIconPath");
                UpdateStepStates();
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetString("WIMUtil_Msg_AdkInstallComplete"),
                    "Success");
            }
            else
            {
                DownloadOscdimgCard.IsEnabled = true;
                DownloadOscdimgCard.HasFailed = true;
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_AdkInstallFailed"),
                    "Error");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error installing ADK: {ex.Message}", ex);
            DownloadOscdimgCard.IsProcessing = false;
            DownloadOscdimgCard.IsEnabled = true;
            DownloadOscdimgCard.HasFailed = true;
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_AdkInstallError"), ex.Message),
                "Error");
        }
    }

    [RelayCommand]
    private void SelectIsoOutputLocation()
    {
        if (_mainWindow == null) return;

        var path = Win32FileDialogHelper.ShowSaveFilePicker(_mainWindow, _localizationService.GetString("WIMUtil_FileDialog_SelectOutput"), "ISO Files", "*.iso", "Winhance_Windows.iso", "iso");
        if (!string.IsNullOrEmpty(path))
        {
            OutputIsoPath = path;
            SelectOutputCard.Description = $"{_localizationService.GetString("WIMUtil_Label_Output")}: {Path.GetFileName(OutputIsoPath)}";
        }
    }

    [RelayCommand]
    private async Task CreateIso()
    {
        try
        {
            if (!IsOscdimgAvailable)
            {
                await _dialogService.ShowWarningAsync(
                    _localizationService.GetString("WIMUtil_Msg_OscdimgRequired"),
                    "Required");
                return;
            }

            if (string.IsNullOrEmpty(OutputIsoPath))
            {
                await _dialogService.ShowWarningAsync(
                    _localizationService.GetString("WIMUtil_Msg_OutputRequired"),
                    "Required");
                return;
            }

            SelectOutputCard.IsEnabled = false;
            SelectOutputCard.Opacity = 0.5;

            _taskProgressService.StartTask(_localizationService.GetString("WIMUtil_Status_CreatingIso"), true);
            var progress = _taskProgressService.CreatePowerShellProgress();

            var success = await _wimUtilService.CreateIsoAsync(WorkingDirectory, OutputIsoPath, progress, _taskProgressService.CurrentTaskCancellationSource!.Token);

            SelectOutputCard.IsEnabled = true;
            SelectOutputCard.Opacity = 1.0;

            if (success)
            {
                IsIsoCreated = true;
                SelectOutputCard.Description = _localizationService.GetString("WIMUtil_Desc_IsoCreatedSuccess");
                SelectOutputCard.DescriptionForeground = new SolidColorBrush(Color.FromArgb(255, 27, 94, 32));
                UpdateStepStates();

                var openFolder = await _dialogService.ShowConfirmationAsync(
                    string.Format(_localizationService.GetString("WIMUtil_Msg_IsoCreatedSuccess"), OutputIsoPath),
                    _localizationService.GetString("WIMUtil_Desc_IsoCreatedSuccess"),
                    _localizationService.GetString("WIMUtil_Button_OpenFolder"),
                    _localizationService.GetString("Button_Close"));
                if (openFolder)
                {
                    _ = _processExecutor.ShellExecuteAsync("explorer.exe", $"/select,\"{OutputIsoPath}\"");
                }
            }
            else
            {
                SelectOutputCard.Description = _localizationService.GetString("WIMUtil_Desc_IsoCreateFailed");
                SelectOutputCard.DescriptionForeground = new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_IsoCreationFailed"),
                    "Error");
            }
        }
        catch (OperationCanceledException)
        {
            SelectOutputCard.IsEnabled = true;
            SelectOutputCard.Opacity = 1.0;
            try { if (File.Exists(OutputIsoPath)) File.Delete(OutputIsoPath); } catch (Exception ex) { _logService.LogDebug($"Best-effort incomplete ISO cleanup failed: {ex.Message}"); }
            SelectOutputCard.Description = _localizationService.GetString("WIMUtil_Desc_IsoCreateCancelled");
        }
        catch (InsufficientDiskSpaceException spaceEx)
        {
            SelectOutputCard.IsEnabled = true;
            SelectOutputCard.Opacity = 1.0;
            SelectOutputCard.Description = string.Format(_localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace"), spaceEx.DriveName);
            await _dialogService.ShowWarningAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_InsufficientSpace_Create"), spaceEx.DriveName, spaceEx.RequiredGB.ToString("F2"), spaceEx.AvailableGB.ToString("F2"), (spaceEx.RequiredGB - spaceEx.AvailableGB).ToString("F2")),
                string.Format(_localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace"), spaceEx.DriveName));
        }
        catch (Exception ex)
        {
            SelectOutputCard.IsEnabled = true;
            SelectOutputCard.Opacity = 1.0;
            SelectOutputCard.Description = string.Format(_localizationService.GetString("WIMUtil_Status_ErrorPrefix"), ex.Message);
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_IsoCreationError"), ex.Message),
                "Error");
        }
        finally
        {
            _taskProgressService.CompleteTask();
        }
    }

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
        2 or 3 or 4 => IsExtractionComplete && !IsConverting,
        _ => false
    };

    private void UpdateDownloadOscdimgCardState()
    {
        if (IsOscdimgAvailable)
        {
            DownloadOscdimgCard.IsEnabled = false;
            DownloadOscdimgCard.IsComplete = true;
            DownloadOscdimgCard.ButtonText = _localizationService.GetString("WIMUtil_Button_OscdimgFound");
            DownloadOscdimgCard.Description = _localizationService.GetString("WIMUtil_Desc_OscdimgFound");
            DownloadOscdimgCard.IconPath = GetResourceIconPath("CheckCircleIconPath");
        }
        else
        {
            DownloadOscdimgCard.IsEnabled = true;
            DownloadOscdimgCard.IsComplete = false;
            DownloadOscdimgCard.ButtonText = _localizationService.GetString("WIMUtil_Button_Download");
            DownloadOscdimgCard.Description = _localizationService.GetString("WIMUtil_Card_DownloadOscdimg_Description");
            DownloadOscdimgCard.IconPath = GetResourceIconPath("ToolBoxIconPath");
        }
    }

    private void UpdateStepStates()
    {
        Step1State.IsExpanded = CurrentStep == 1;
        Step1State.IsAvailable = true;
        Step1State.IsComplete = IsExtractionComplete && !IsConverting;
        Step1State.StatusText = IsConverting
            ? _localizationService.GetString("WIMUtil_Status_Converting")
            : IsExtractionComplete
                ? _localizationService.GetString("WIMUtil_Status_IsoExtracted")
                : IsExtracting
                    ? _localizationService.GetString("WIMUtil_Status_Extracting")
                    : !string.IsNullOrEmpty(SelectedIsoPath)
                        ? _localizationService.GetString("WIMUtil_Status_IsoSelected")
                        : _localizationService.GetString("WIMUtil_Status_NoIsoSelected");

        Step2State.IsExpanded = CurrentStep == 2;
        Step2State.IsAvailable = IsExtractionComplete && !IsConverting;
        Step2State.IsComplete = IsXmlAdded;
        Step2State.StatusText = IsConverting
            ? _localizationService.GetString("WIMUtil_Status_WaitForConversion")
            : !IsExtractionComplete
                ? _localizationService.GetString("WIMUtil_Status_CompleteStep1")
                : IsXmlAdded
                    ? _localizationService.GetString("WIMUtil_Status_XmlAdded")
                    : _localizationService.GetString("WIMUtil_Status_NoXmlAdded");

        Step3State.IsExpanded = CurrentStep == 3;
        Step3State.IsAvailable = IsExtractionComplete && !IsConverting;
        Step3State.IsComplete = AreDriversAdded;
        Step3State.StatusText = IsConverting
            ? _localizationService.GetString("WIMUtil_Status_WaitForConversion")
            : !IsExtractionComplete
                ? _localizationService.GetString("WIMUtil_Status_CompleteStep1")
                : AreDriversAdded
                    ? _localizationService.GetString("WIMUtil_Status_DriversAdded")
                    : _localizationService.GetString("WIMUtil_Status_NoDriversAdded");

        Step4State.IsExpanded = CurrentStep == 4;
        Step4State.IsAvailable = IsExtractionComplete && !IsConverting;
        Step4State.IsComplete = IsIsoCreated;
        Step4State.StatusText = IsConverting
            ? _localizationService.GetString("WIMUtil_Status_WaitForConversion")
            : !IsExtractionComplete
                ? _localizationService.GetString("WIMUtil_Status_CompleteStep1")
                : IsIsoCreated
                    ? _localizationService.GetString("WIMUtil_Status_IsoCreated")
                    : !string.IsNullOrEmpty(OutputIsoPath)
                        ? $"{_localizationService.GetString("WIMUtil_Label_Output")}: {Path.GetFileName(OutputIsoPath)}"
                        : _localizationService.GetString("WIMUtil_Status_ReadyToCreateIso");

        OnPropertyChanged(nameof(Step1State));
        OnPropertyChanged(nameof(Step2State));
        OnPropertyChanged(nameof(Step3State));
        OnPropertyChanged(nameof(Step4State));
    }

    [RelayCommand]
    private async Task OpenWindows10Download()
    {
        try { await Windows.System.Launcher.LaunchUriAsync(new Uri("https://www.microsoft.com/software-download/windows10")); }
        catch (Exception ex) { _logService.LogDebug($"Failed to launch Windows 10 download URL: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task OpenWindows11Download()
    {
        try { await Windows.System.Launcher.LaunchUriAsync(new Uri("https://www.microsoft.com/software-download/windows11")); }
        catch (Exception ex) { _logService.LogDebug($"Failed to launch Windows 11 download URL: {ex.Message}"); }
    }

    [RelayCommand]
    private async Task ConvertImageFormat()
    {
        if (CurrentImageFormat == null) return;

        try
        {
            var targetFormat = CurrentImageFormat.Format == ImageFormat.Wim ? ImageFormat.Esd : ImageFormat.Wim;
            var targetFormatName = targetFormat == ImageFormat.Wim ? "WIM" : "ESD";
            var currentFormatName = CurrentImageFormat.Format == ImageFormat.Wim ? "WIM" : "ESD";

            var confirmKey = targetFormat == ImageFormat.Esd ? "WIMUtil_Msg_ConvertConfirm_Esd" : "WIMUtil_Msg_ConvertConfirm_Wim";
            var currentSize = CurrentImageFormat.FileSizeBytes / (1024.0 * 1024 * 1024);
            var estimatedTargetSize = CurrentImageFormat.Format == ImageFormat.Wim ? currentSize * 0.65 : currentSize * 1.50;
            var diff = Math.Abs(estimatedTargetSize - currentSize);
            var confirmed = await _dialogService.ShowConfirmationAsync(
                string.Format(_localizationService.GetString(confirmKey), diff.ToString("F2")),
                string.Format(_localizationService.GetString("WIMUtil_Card_ConvertImage_Button_Dynamic"), targetFormatName),
                "Yes", "No");
            if (!confirmed) return;

            IsConverting = true;
            ConvertImageCard.IsProcessing = true;
            ConvertImageCard.IsEnabled = false;
            ConversionStatus = string.Format(_localizationService.GetString("WIMUtil_Status_ConvertingToFormat"), currentFormatName, targetFormatName);
            UpdateStepStates();

            _taskProgressService.StartTask(string.Format(_localizationService.GetString("WIMUtil_Status_ConvertingToFormat"), currentFormatName, targetFormatName), true);
            var progress = _taskProgressService.CreatePowerShellProgress();

            var success = await _wimUtilService.ConvertImageAsync(WorkingDirectory, targetFormat, progress, _taskProgressService.CurrentTaskCancellationSource!.Token);

            if (success)
            {
                ConvertImageCard.IsComplete = true;
                ConversionStatus = string.Format(_localizationService.GetString("WIMUtil_Status_ConversionSuccess"), targetFormatName);
                await DetectImageFormatAsync();
                await _dialogService.ShowInformationAsync(
                    string.Format(_localizationService.GetString("WIMUtil_Msg_ConversionSuccess"), targetFormatName),
                    "Success");
            }
            else
            {
                ConvertImageCard.HasFailed = true;
                ConversionStatus = _localizationService.GetString("WIMUtil_Status_ConversionFailed");
                await _dialogService.ShowErrorAsync(
                    string.Format(_localizationService.GetString("WIMUtil_Msg_ConversionFailed"), targetFormatName),
                    "Error");
            }
        }
        catch (OperationCanceledException)
        {
            ConversionStatus = _localizationService.GetString("WIMUtil_Status_ConversionCancelled");
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error during conversion: {ex.Message}", ex);
            ConvertImageCard.HasFailed = true;
            ConversionStatus = string.Format(_localizationService.GetString("WIMUtil_Status_ErrorPrefix"), ex.Message);
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_ConversionError"), ex.Message),
                "Error");
        }
        finally
        {
            IsConverting = false;
            ConvertImageCard.IsProcessing = false;
            ConvertImageCard.IsEnabled = true;
            UpdateStepStates();
            _taskProgressService.CompleteTask();
        }
    }

    private async Task DetectImageFormatAsync()
    {
        try
        {
            var detection = await _wimUtilService.DetectAllImageFormatsAsync(WorkingDirectory);
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

    private string FormatFileSize(long bytes) => $"{bytes / (1024.0 * 1024 * 1024):F2} GB";

    private static string GetResourceIconPath(string resourceKey)
    {
        if (Application.Current.Resources.TryGetValue(resourceKey, out var value) && value is string path)
            return path;
        return string.Empty;
    }

    private void UpdateConversionCardState()
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

        var perfNote = CurrentImageFormat.Format == ImageFormat.Wim
            ? _localizationService.GetString("WIMUtil_Label_PerfNote_Wim")
            : _localizationService.GetString("WIMUtil_Label_PerfNote_Esd");

        ConvertImageCard.Icon = CurrentImageFormat.Format == ImageFormat.Wim ? "\uE740" : "\uE741";
        ConvertImageCard.Title = string.Format(_localizationService.GetString("WIMUtil_Card_ConvertImage_Title_Dynamic"), currentFormat, targetFormat);
        ConvertImageCard.Description = $"{_localizationService.GetString("WIMUtil_Label_Current")}: install.{currentFormat.ToLower()} ({currentSize:F2} GB)\n{_localizationService.GetString("WIMUtil_Label_AfterConversion")}: ~{estimatedTargetSize:F2} GB ({sizeChange})";
        ConvertImageCard.ButtonText = string.Format(_localizationService.GetString("WIMUtil_Card_ConvertImage_Button_Dynamic"), targetFormat);
        ConvertImageCard.IsEnabled = !IsConverting;
    }

    [RelayCommand]
    private async Task DeleteWim()
    {
        try
        {
            var confirmed = await _dialogService.ShowConfirmationAsync(
                _localizationService.GetString("WIMUtil_Msg_DeleteWimConfirm"),
                _localizationService.GetString("WIMUtil_Button_DeleteWim"),
                _localizationService.GetString("Button_Delete"),
                _localizationService.GetString("Button_Cancel"));
            if (!confirmed) return;

            _cancellationTokenSource = new CancellationTokenSource();
            var progress = new Progress<TaskProgressDetail>(detail => { });

            var success = await _wimUtilService.DeleteImageFileAsync(
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
                    "Error");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error deleting WIM: {ex.Message}", ex);
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_DeleteError"), ex.Message),
                "Error");
        }
    }

    [RelayCommand]
    private async Task DeleteEsd()
    {
        try
        {
            var confirmed = await _dialogService.ShowConfirmationAsync(
                _localizationService.GetString("WIMUtil_Msg_DeleteEsdConfirm"),
                _localizationService.GetString("WIMUtil_Button_DeleteEsd"),
                _localizationService.GetString("Button_Delete"),
                _localizationService.GetString("Button_Cancel"));
            if (!confirmed) return;

            _cancellationTokenSource = new CancellationTokenSource();
            var progress = new Progress<TaskProgressDetail>(detail => { });

            var success = await _wimUtilService.DeleteImageFileAsync(
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
                    "Error");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error deleting ESD: {ex.Message}", ex);
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_DeleteError"), ex.Message),
                "Error");
        }
    }

    partial void OnHasExtractedIsoAlreadyChanged(bool value)
    {
        SelectDirectoryCard.Description = value
            ? _localizationService.GetString("WIMUtil_Label_SelectExtracted")
            : string.Format(_localizationService.GetString("WIMUtil_Card_SelectDirectory_Description_Default"), Path.Combine(Path.GetTempPath(), "WinhanceWIM"));
    }

    partial void OnIsExtractionCompleteChanged(bool value)
    {
        if (value) _ = SafeDetectImageFormatAsync();
    }

    private async Task SafeDetectImageFormatAsync()
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
}
