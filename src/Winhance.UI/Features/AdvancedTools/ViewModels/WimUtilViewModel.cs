using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceProvider _serviceProvider;
    private readonly ILocalizationService _localizationService;
    private readonly IDispatcherService _dispatcherService;
    private CancellationTokenSource? _cancellationTokenSource;

    private Window? _mainWindow;

    public string Title => _localizationService?.GetString("WIMUtil_Title") ?? "Windows Installation Media Utility";

    [ObservableProperty]
    private int _currentStep = 1;

    [ObservableProperty]
    private WizardStepState _step1State = new();

    [ObservableProperty]
    private WizardStepState _step2State = new();

    [ObservableProperty]
    private WizardStepState _step3State = new();

    [ObservableProperty]
    private WizardStepState _step4State = new();

    [ObservableProperty]
    private string _selectedIsoPath = string.Empty;

    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    [ObservableProperty]
    private bool _canStartExtraction;

    [ObservableProperty]
    private bool _isExtractionComplete;

    [ObservableProperty]
    private bool _isExtracting;

    [ObservableProperty]
    private bool _hasExtractedIsoAlready;

    [ObservableProperty]
    private ImageFormatInfo? _currentImageFormat;

    [ObservableProperty]
    private bool _showConversionCard;

    [ObservableProperty]
    private bool _isConverting;

    [ObservableProperty]
    private string _conversionStatus = string.Empty;

    [ObservableProperty]
    private bool _bothFormatsExist;

    [ObservableProperty]
    private string _wimFileSize = string.Empty;

    [ObservableProperty]
    private string _esdFileSize = string.Empty;

    [ObservableProperty]
    private ImageDetectionResult? _detectionResult;

    [ObservableProperty]
    private string _selectedXmlPath = string.Empty;

    [ObservableProperty]
    private string _xmlStatus = "No XML File Added";

    [ObservableProperty]
    private bool _isXmlAdded;

    [ObservableProperty]
    private bool _areDriversAdded;

    [ObservableProperty]
    private string _outputIsoPath = string.Empty;

    [ObservableProperty]
    private bool _isOscdimgAvailable;

    [ObservableProperty]
    private bool _isIsoCreated;

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
        IServiceProvider serviceProvider,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService)
    {
        _wimUtilService = wimUtilService;
        _taskProgressService = taskProgressService;
        _dialogService = dialogService;
        _logService = logService;
        _serviceProvider = serviceProvider;
        _localizationService = localizationService;
        _dispatcherService = dispatcherService;

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
            StatusText = "No ISO selected",
            IsExpanded = true,
            IsAvailable = true
        };

        Step2State = new WizardStepState
        {
            StepNumber = 2,
            Title = _localizationService.GetString("WIMUtil_Step2_Title") ?? "Add XML File",
            Icon = "FileCode",
            StatusText = "Complete Step 1 first"
        };

        Step3State = new WizardStepState
        {
            StepNumber = 3,
            Title = _localizationService.GetString("WIMUtil_Step3_Title") ?? "Add Drivers",
            Icon = "Chip",
            StatusText = "Complete Step 1 first"
        };

        Step4State = new WizardStepState
        {
            StepNumber = 4,
            Title = _localizationService.GetString("WIMUtil_Step4_Title") ?? "Create ISO",
            Icon = "WrenchClock",
            StatusText = "Complete Step 1 first"
        };
    }

    private void CreateActionCards()
    {
        SelectIsoCard = new WizardActionCard
        {
            Icon = "\uE958",
            Title = "Select ISO File",
            Description = "No file selected",
            ButtonText = "Browse",
            ButtonCommand = SelectIsoFileCommand,
            IsEnabled = true
        };

        SelectDirectoryCard = new WizardActionCard
        {
            Icon = "\uE8B7",
            Title = "Working Directory",
            Description = $"Default: {Path.Combine(Path.GetTempPath(), "WinhanceWIM")}",
            ButtonText = "Change",
            ButtonCommand = SelectWorkingDirectoryCommand,
            IsEnabled = true
        };

        GenerateWinhanceXmlCard = new WizardActionCard
        {
            Icon = "\uE710",
            Title = "Generate Winhance XML",
            Description = "Generate XML from current selections",
            ButtonText = "Generate",
            ButtonCommand = GenerateWinhanceXmlCommand,
            IsEnabled = true
        };

        DownloadXmlCard = new WizardActionCard
        {
            Icon = "\uE896",
            Title = "Download XML",
            Description = "Download pre-made unattend XML",
            ButtonText = "Download",
            ButtonCommand = DownloadUnattendedWinstallXmlCommand,
            IsEnabled = true
        };

        SelectXmlCard = new WizardActionCard
        {
            Icon = "\uE8E5",
            Title = "Select XML File",
            Description = "Choose your own XML file",
            ButtonText = "Browse",
            ButtonCommand = SelectXmlFileCommand,
            IsEnabled = true
        };

        ExtractSystemDriversCard = new WizardActionCard
        {
            Icon = "\uE964",
            Title = "Extract System Drivers",
            Description = "Export drivers from current system",
            ButtonText = "Extract",
            ButtonCommand = ExtractAndAddSystemDriversCommand,
            IsEnabled = true
        };

        SelectCustomDriversCard = new WizardActionCard
        {
            Icon = "\uE8B7",
            Title = "Add Custom Drivers",
            Description = "Add drivers from a folder",
            ButtonText = "Browse",
            ButtonCommand = SelectAndAddCustomDriversCommand,
            IsEnabled = true
        };

        DownloadOscdimgCard = new WizardActionCard
        {
            Icon = "\uE896",
            Title = "Download Oscdimg",
            Description = "Required tool for ISO creation",
            ButtonText = "Download",
            ButtonCommand = DownloadOscdimgCommand,
            IsEnabled = true
        };

        SelectOutputCard = new WizardActionCard
        {
            Icon = "\uE74E",
            Title = "Output Location",
            Description = "No location selected",
            ButtonText = "Browse",
            ButtonCommand = SelectIsoOutputLocationCommand,
            IsEnabled = true
        };

        ConvertImageCard = new WizardActionCard
        {
            Icon = "\uE8AB",
            Title = "Convert Image",
            Description = "Detecting image format...",
            ButtonText = "Convert",
            ButtonCommand = ConvertImageFormatCommand,
            IsEnabled = false
        };
    }

    [RelayCommand]
    private void SelectIsoFile()
    {
        if (_mainWindow == null) return;

        var path = Win32FileDialogHelper.ShowOpenFilePicker(_mainWindow, "Select ISO File", "ISO Files", "*.iso");
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

        var description = HasExtractedIsoAlready ? "Select extracted ISO folder" : "Select working directory";
        var selectedPath = Win32FileDialogHelper.ShowFolderPicker(_mainWindow, description);

        if (string.IsNullOrEmpty(selectedPath)) return;

        if (HasExtractedIsoAlready)
        {
            var isValid = await ValidateExtractedIsoDirectory(selectedPath);
            if (isValid)
            {
                WorkingDirectory = selectedPath;
                SelectDirectoryCard.Description = $"Using: {WorkingDirectory}";
                IsExtractionComplete = true;
                UpdateStepStates();
                await _dialogService.ShowInformationAsync("The directory has been validated successfully.", "Validation Complete");
            }
            else
            {
                WorkingDirectory = string.Empty;
                SelectDirectoryCard.Description = "Invalid directory";
                await _dialogService.ShowErrorAsync("The selected directory does not appear to contain extracted Windows ISO files.", "Invalid Directory");
            }
        }
        else
        {
            WorkingDirectory = Path.Combine(selectedPath, "WinhanceWIM");
            try
            {
                Directory.CreateDirectory(WorkingDirectory);
                SelectDirectoryCard.Description = $"Using: {WorkingDirectory}";
                CanStartExtraction = !string.IsNullOrEmpty(SelectedIsoPath) && !string.IsNullOrEmpty(WorkingDirectory);
            }
            catch (Exception ex)
            {
                _logService.LogError($"Failed to create working directory: {ex.Message}", ex);
                SelectDirectoryCard.Description = "Failed to create directory";
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
            catch { return false; }

            var extractedDirs = Directory.GetDirectories(path);
            var hasSourcesDir = extractedDirs.Any(d => Path.GetFileName(d)?.Equals("sources", StringComparison.OrdinalIgnoreCase) == true);
            var hasBootDir = extractedDirs.Any(d => Path.GetFileName(d)?.Equals("boot", StringComparison.OrdinalIgnoreCase) == true);

            return hasSourcesDir && hasBootDir;
        }
        catch { return false; }
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

            _taskProgressService.StartTask("Extracting ISO", true);
            var progress = _taskProgressService.CreatePowerShellProgress();

            var success = await _wimUtilService.ExtractIsoAsync(SelectedIsoPath, WorkingDirectory, progress, _taskProgressService.CurrentTaskCancellationSource.Token);

            ResetExtractionState();

            if (success)
            {
                SelectIsoCard.IsComplete = true;
                SelectIsoCard.Description = "ISO extracted successfully";
                SelectIsoCard.DescriptionForeground = new SolidColorBrush(Color.FromArgb(255, 27, 94, 32));
                IsExtractionComplete = true;
                UpdateStepStates();
                await _dialogService.ShowInformationAsync("The ISO has been extracted successfully.", "Extraction Complete");
            }
            else
            {
                SelectIsoCard.HasFailed = true;
                SelectIsoCard.Description = "Extraction failed";
                SelectIsoCard.DescriptionForeground = new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
                await _dialogService.ShowErrorAsync("Failed to extract the ISO file.", "Extraction Failed");
            }
        }
        catch (OperationCanceledException)
        {
            ResetExtractionState();
            SelectIsoCard.Description = "Extraction cancelled";
        }
        catch (InsufficientDiskSpaceException spaceEx)
        {
            ResetExtractionState();
            SelectIsoCard.HasFailed = true;
            SelectIsoCard.Description = $"Insufficient disk space on {spaceEx.DriveName}";
            await _dialogService.ShowWarningAsync($"Not enough space on {spaceEx.DriveName}. Required: {spaceEx.RequiredGB:F2} GB, Available: {spaceEx.AvailableGB:F2} GB", "Insufficient Disk Space");
        }
        catch (Exception ex)
        {
            ResetExtractionState();
            SelectIsoCard.HasFailed = true;
            SelectIsoCard.Description = $"Error: {ex.Message}";
            await _dialogService.ShowErrorAsync(ex.Message, "Error");
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

            var confirmed = await _dialogService.ShowConfirmationAsync("This will generate an autounattend.xml file based on your current Winhance selections. Continue?", "Generate XML", "Yes", "No");
            if (!confirmed) return;

            XmlStatus = "Generating XML...";
            var xmlGeneratorService = _serviceProvider.GetRequiredService<IAutounattendXmlGeneratorService>();
            var outputPath = Path.Combine(WorkingDirectory, "autounattend.xml");
            var generatedPath = await xmlGeneratorService.GenerateFromCurrentSelectionsAsync(outputPath);

            SelectedXmlPath = generatedPath;
            IsXmlAdded = true;
            XmlStatus = "XML generated successfully";
            ClearOtherXmlCardCompletions("generate");
            GenerateWinhanceXmlCard.IsComplete = true;
            UpdateStepStates();
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error generating XML: {ex.Message}", ex);
            XmlStatus = $"Generation failed: {ex.Message}";
            GenerateWinhanceXmlCard.HasFailed = true;
            await _dialogService.ShowErrorAsync($"Failed to generate XML: {ex.Message}", "Error");
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
            var progress = new Progress<TaskProgressDetail>(detail => XmlStatus = detail.StatusText ?? "Downloading...");

            XmlStatus = "Starting download...";
            await _wimUtilService.DownloadUnattendedWinstallXmlAsync(destinationPath, progress, _cancellationTokenSource.Token);

            var addSuccess = await _wimUtilService.AddXmlToImageAsync(destinationPath, WorkingDirectory);
            if (addSuccess)
            {
                SelectedXmlPath = destinationPath;
                IsXmlAdded = true;
                XmlStatus = "XML downloaded and added";
                ClearOtherXmlCardCompletions("download");
                DownloadXmlCard.IsComplete = true;
                UpdateStepStates();
            }
            else
            {
                DownloadXmlCard.HasFailed = true;
                XmlStatus = "Downloaded but failed to add to media";
                await _dialogService.ShowErrorAsync("Failed to add XML to installation media.", "Error");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error downloading XML: {ex.Message}", ex);
            XmlStatus = $"Download failed: {ex.Message}";
            DownloadXmlCard.HasFailed = true;
            await _dialogService.ShowErrorAsync($"Failed to download XML: {ex.Message}", "Error");
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

            var selectedPath = Win32FileDialogHelper.ShowOpenFilePicker(_mainWindow, "Select XML File", "XML Files", "*.xml");
            if (string.IsNullOrEmpty(selectedPath)) return;

            XmlStatus = "Validating XML...";
            var isValidXml = await ValidateXmlFile(selectedPath);
            if (!isValidXml)
            {
                SelectXmlCard.HasFailed = true;
                XmlStatus = "Invalid XML file";
                await _dialogService.ShowErrorAsync("The selected file is not a valid XML file.", "Error");
                return;
            }

            XmlStatus = "Adding XML to media...";
            var addSuccess = await _wimUtilService.AddXmlToImageAsync(selectedPath, WorkingDirectory);
            if (addSuccess)
            {
                SelectedXmlPath = selectedPath;
                IsXmlAdded = true;
                XmlStatus = "XML added successfully";
                ClearOtherXmlCardCompletions("select");
                SelectXmlCard.IsComplete = true;
                UpdateStepStates();
            }
            else
            {
                SelectXmlCard.HasFailed = true;
                XmlStatus = "Valid XML but failed to add to media";
                await _dialogService.ShowErrorAsync("Failed to add XML to installation media.", "Error");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error selecting XML: {ex.Message}", ex);
            XmlStatus = $"Error: {ex.Message}";
            SelectXmlCard.HasFailed = true;
            await _dialogService.ShowErrorAsync(ex.Message, "Error");
        }
    }

    private async Task<bool> ValidateXmlFile(string xmlPath)
    {
        try
        {
            await Task.Run(() => XDocument.Load(xmlPath));
            return true;
        }
        catch { return false; }
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

            var confirmed = await _dialogService.ShowConfirmationAsync("This will export all third-party drivers from your current system. This may take several minutes. Continue?", "Extract Drivers", "Yes", "No");
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
                await _dialogService.ShowInformationAsync("System drivers have been exported and added.", "Success");
            }
            else
            {
                ExtractSystemDriversCard.HasFailed = true;
                await _dialogService.ShowWarningAsync("No third-party drivers were found to export.", "Warning");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error extracting system drivers: {ex.Message}", ex);
            ExtractSystemDriversCard.IsProcessing = false;
            ExtractSystemDriversCard.IsEnabled = true;
            ExtractSystemDriversCard.HasFailed = true;
            await _dialogService.ShowErrorAsync($"Failed to extract drivers: {ex.Message}", "Error");
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

            var selectedPath = Win32FileDialogHelper.ShowFolderPicker(_mainWindow, "Select driver folder");
            if (string.IsNullOrEmpty(selectedPath)) return;

            if (!Directory.Exists(selectedPath))
            {
                SelectCustomDriversCard.HasFailed = true;
                await _dialogService.ShowErrorAsync("The selected folder does not exist.", "Error");
                return;
            }

            var hasFiles = Directory.EnumerateFileSystemEntries(selectedPath, "*", SearchOption.AllDirectories).Any();
            if (!hasFiles)
            {
                SelectCustomDriversCard.HasFailed = true;
                await _dialogService.ShowWarningAsync("The selected folder is empty.", "Warning");
                return;
            }

            SelectCustomDriversCard.IsProcessing = true;
            SelectCustomDriversCard.IsEnabled = false;
            SelectCustomDriversCard.Description = $"Selected: {selectedPath}";

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
                await _dialogService.ShowInformationAsync("Custom drivers have been added.", "Success");
            }
            else
            {
                SelectCustomDriversCard.HasFailed = true;
                await _dialogService.ShowWarningAsync("No valid driver files were found in the selected folder.", "Warning");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error adding custom drivers: {ex.Message}", ex);
            SelectCustomDriversCard.IsProcessing = false;
            SelectCustomDriversCard.IsEnabled = true;
            SelectCustomDriversCard.HasFailed = true;
            await _dialogService.ShowErrorAsync($"Failed to add drivers: {ex.Message}", "Error");
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
                DownloadOscdimgCard.ButtonText = "Installed";
                DownloadOscdimgCard.Description = "Oscdimg is installed and ready";
                DownloadOscdimgCard.Icon = "\uE73E";
                UpdateStepStates();
                await _dialogService.ShowInformationAsync("ADK tools have been installed successfully.", "Success");
            }
            else
            {
                DownloadOscdimgCard.IsEnabled = true;
                DownloadOscdimgCard.HasFailed = true;
                await _dialogService.ShowErrorAsync("Failed to install ADK tools.", "Error");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error installing ADK: {ex.Message}", ex);
            DownloadOscdimgCard.IsProcessing = false;
            DownloadOscdimgCard.IsEnabled = true;
            DownloadOscdimgCard.HasFailed = true;
            await _dialogService.ShowErrorAsync($"Failed to install ADK: {ex.Message}", "Error");
        }
    }

    [RelayCommand]
    private void SelectIsoOutputLocation()
    {
        if (_mainWindow == null) return;

        var path = Win32FileDialogHelper.ShowSaveFilePicker(_mainWindow, "Save ISO As", "ISO Files", "*.iso", "Winhance_Windows.iso", "iso");
        if (!string.IsNullOrEmpty(path))
        {
            OutputIsoPath = path;
            SelectOutputCard.Description = $"Output: {Path.GetFileName(OutputIsoPath)}";
        }
    }

    [RelayCommand]
    private async Task CreateIso()
    {
        try
        {
            if (!IsOscdimgAvailable)
            {
                await _dialogService.ShowWarningAsync("Please download and install oscdimg first.", "Required");
                return;
            }

            if (string.IsNullOrEmpty(OutputIsoPath))
            {
                await _dialogService.ShowWarningAsync("Please select an output location for the ISO.", "Required");
                return;
            }

            SelectOutputCard.IsEnabled = false;
            SelectOutputCard.Opacity = 0.5;

            _taskProgressService.StartTask("Creating ISO", true);
            var progress = _taskProgressService.CreatePowerShellProgress();

            var success = await _wimUtilService.CreateIsoAsync(WorkingDirectory, OutputIsoPath, progress, _taskProgressService.CurrentTaskCancellationSource.Token);

            SelectOutputCard.IsEnabled = true;
            SelectOutputCard.Opacity = 1.0;

            if (success)
            {
                IsIsoCreated = true;
                SelectOutputCard.Description = "ISO created successfully!";
                SelectOutputCard.DescriptionForeground = new SolidColorBrush(Color.FromArgb(255, 27, 94, 32));
                UpdateStepStates();

                var openFolder = await _dialogService.ShowConfirmationAsync($"ISO has been created at:\n{OutputIsoPath}\n\nWould you like to open the folder?", "ISO Created", "Open Folder", "Close");
                if (openFolder)
                {
                    Process.Start("explorer.exe", $"/select,\"{OutputIsoPath}\"");
                }
            }
            else
            {
                SelectOutputCard.Description = "ISO creation failed";
                SelectOutputCard.DescriptionForeground = new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
                await _dialogService.ShowErrorAsync("Failed to create the ISO file.", "Error");
            }
        }
        catch (OperationCanceledException)
        {
            SelectOutputCard.IsEnabled = true;
            SelectOutputCard.Opacity = 1.0;
            try { if (File.Exists(OutputIsoPath)) File.Delete(OutputIsoPath); } catch { }
            SelectOutputCard.Description = "ISO creation cancelled";
        }
        catch (InsufficientDiskSpaceException spaceEx)
        {
            SelectOutputCard.IsEnabled = true;
            SelectOutputCard.Opacity = 1.0;
            SelectOutputCard.Description = $"Insufficient space on {spaceEx.DriveName}";
            await _dialogService.ShowWarningAsync($"Not enough space on {spaceEx.DriveName}. Required: {spaceEx.RequiredGB:F2} GB, Available: {spaceEx.AvailableGB:F2} GB", "Insufficient Disk Space");
        }
        catch (Exception ex)
        {
            SelectOutputCard.IsEnabled = true;
            SelectOutputCard.Opacity = 1.0;
            SelectOutputCard.Description = $"Error: {ex.Message}";
            await _dialogService.ShowErrorAsync($"Failed to create ISO: {ex.Message}", "Error");
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
            DownloadOscdimgCard.ButtonText = "Installed";
            DownloadOscdimgCard.Description = "Oscdimg is available";
            DownloadOscdimgCard.Icon = "\uE73E";
        }
        else
        {
            DownloadOscdimgCard.IsEnabled = true;
            DownloadOscdimgCard.IsComplete = false;
            DownloadOscdimgCard.ButtonText = "Download";
            DownloadOscdimgCard.Description = "Required tool for ISO creation";
            DownloadOscdimgCard.Icon = "\uE896";
        }
    }

    private void UpdateStepStates()
    {
        Step1State.IsExpanded = CurrentStep == 1;
        Step1State.IsAvailable = true;
        Step1State.IsComplete = IsExtractionComplete && !IsConverting;
        Step1State.StatusText = IsConverting ? "Converting..." : IsExtractionComplete ? "ISO extracted" : IsExtracting ? "Extracting..." : !string.IsNullOrEmpty(SelectedIsoPath) ? "ISO selected" : "No ISO selected";

        Step2State.IsExpanded = CurrentStep == 2;
        Step2State.IsAvailable = IsExtractionComplete && !IsConverting;
        Step2State.IsComplete = IsXmlAdded;
        Step2State.StatusText = IsConverting ? "Wait for conversion" : !IsExtractionComplete ? "Complete Step 1 first" : IsXmlAdded ? "XML added" : "No XML added (optional)";

        Step3State.IsExpanded = CurrentStep == 3;
        Step3State.IsAvailable = IsExtractionComplete && !IsConverting;
        Step3State.IsComplete = AreDriversAdded;
        Step3State.StatusText = IsConverting ? "Wait for conversion" : !IsExtractionComplete ? "Complete Step 1 first" : AreDriversAdded ? "Drivers added" : "No drivers added (optional)";

        Step4State.IsExpanded = CurrentStep == 4;
        Step4State.IsAvailable = IsExtractionComplete && !IsConverting;
        Step4State.IsComplete = IsIsoCreated;
        Step4State.StatusText = IsConverting ? "Wait for conversion" : !IsExtractionComplete ? "Complete Step 1 first" : IsIsoCreated ? "ISO created!" : !string.IsNullOrEmpty(OutputIsoPath) ? $"Output: {Path.GetFileName(OutputIsoPath)}" : "Ready to create ISO";

        OnPropertyChanged(nameof(Step1State));
        OnPropertyChanged(nameof(Step2State));
        OnPropertyChanged(nameof(Step3State));
        OnPropertyChanged(nameof(Step4State));
    }

    [RelayCommand]
    private async Task OpenWindows10Download()
    {
        try { await Windows.System.Launcher.LaunchUriAsync(new Uri("https://www.microsoft.com/software-download/windows10")); }
        catch { }
    }

    [RelayCommand]
    private async Task OpenWindows11Download()
    {
        try { await Windows.System.Launcher.LaunchUriAsync(new Uri("https://www.microsoft.com/software-download/windows11")); }
        catch { }
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

            var confirmed = await _dialogService.ShowConfirmationAsync($"This will convert the image from {currentFormatName} to {targetFormatName}. This process may take a while. Continue?", $"Convert to {targetFormatName}", "Yes", "No");
            if (!confirmed) return;

            IsConverting = true;
            ConvertImageCard.IsProcessing = true;
            ConvertImageCard.IsEnabled = false;
            ConversionStatus = $"Converting {currentFormatName} to {targetFormatName}...";
            UpdateStepStates();

            _taskProgressService.StartTask($"Converting to {targetFormatName}", true);
            var progress = _taskProgressService.CreatePowerShellProgress();

            var success = await _wimUtilService.ConvertImageAsync(WorkingDirectory, targetFormat, progress, _taskProgressService.CurrentTaskCancellationSource.Token);

            if (success)
            {
                ConvertImageCard.IsComplete = true;
                ConversionStatus = $"Converted to {targetFormatName} successfully";
                await DetectImageFormatAsync();
                await _dialogService.ShowInformationAsync($"Image converted to {targetFormatName} successfully.", "Success");
            }
            else
            {
                ConvertImageCard.HasFailed = true;
                ConversionStatus = "Conversion failed";
                await _dialogService.ShowErrorAsync($"Failed to convert image to {targetFormatName}.", "Error");
            }
        }
        catch (OperationCanceledException)
        {
            ConversionStatus = "Conversion cancelled";
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error during conversion: {ex.Message}", ex);
            ConvertImageCard.HasFailed = true;
            ConversionStatus = $"Error: {ex.Message}";
            await _dialogService.ShowErrorAsync($"Conversion failed: {ex.Message}", "Error");
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

    private void UpdateConversionCardState()
    {
        if (BothFormatsExist || CurrentImageFormat == null)
        {
            ConvertImageCard.IsEnabled = CurrentImageFormat != null && !BothFormatsExist;
            ConvertImageCard.Description = CurrentImageFormat == null ? "No image format detected" : "Both formats exist";
            return;
        }

        var currentFormat = CurrentImageFormat.Format == ImageFormat.Wim ? "WIM" : "ESD";
        var targetFormat = CurrentImageFormat.Format == ImageFormat.Wim ? "ESD" : "WIM";
        var currentSize = CurrentImageFormat.FileSizeBytes / (1024.0 * 1024 * 1024);
        var estimatedTargetSize = CurrentImageFormat.Format == ImageFormat.Wim ? currentSize * 0.65 : currentSize * 1.50;
        var diff = Math.Abs(estimatedTargetSize - currentSize);
        var sizeChange = CurrentImageFormat.Format == ImageFormat.Wim ? $"Save ~{diff:F2} GB" : $"Requires ~{diff:F2} GB more";

        ConvertImageCard.Icon = CurrentImageFormat.Format == ImageFormat.Wim ? "\uE740" : "\uE741";
        ConvertImageCard.Title = $"Convert {currentFormat} to {targetFormat}";
        ConvertImageCard.Description = $"Current: install.{currentFormat.ToLower()} ({currentSize:F2} GB)\nAfter: ~{estimatedTargetSize:F2} GB ({sizeChange})";
        ConvertImageCard.ButtonText = $"Convert to {targetFormat}";
        ConvertImageCard.IsEnabled = !IsConverting;
    }

    partial void OnHasExtractedIsoAlreadyChanged(bool value)
    {
        SelectDirectoryCard.Description = value ? "Select folder with extracted ISO" : $"Default: {Path.Combine(Path.GetTempPath(), "WinhanceWIM")}";
    }

    partial void OnIsExtractionCompleteChanged(bool value)
    {
        if (value) Task.Run(DetectImageFormatAsync);
    }
}
