using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.AdvancedTools.Models;
using Winhance.WPF.Features.Common.Resources.Theme;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Views;

namespace Winhance.WPF.Features.AdvancedTools.ViewModels
{
    public partial class WimUtilViewModel : BaseFeatureViewModel, IFeatureViewModel
    {
        private readonly IWimUtilService _wimUtilService;
        private readonly ITaskProgressService _taskProgressService;
        private readonly IDialogService _dialogService;
        private readonly ILogService _logService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IThemeManager _themeManager;
        private CancellationTokenSource? _cancellationTokenSource;

        public bool IsDarkTheme => _themeManager.IsDarkTheme;

        // P/Invoke for folder browser dialog
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHBrowseForFolder(ref BROWSEINFO bi);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern bool SHGetPathFromIDList(IntPtr pidl, IntPtr pszPath);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct BROWSEINFO
        {
            public IntPtr hwndOwner;
            public IntPtr pidlRoot;
            public IntPtr pszDisplayName;
            public string lpszTitle;
            public uint ulFlags;
            public IntPtr lpfn;
            public IntPtr lParam;
            public int iImage;
        }

        private const uint BIF_RETURNONLYFSDIRS = 0x00000001;
        private const uint BIF_NEWDIALOGSTYLE = 0x00000040;

        private string? ShowFolderBrowserDialog(string description)
        {
            var bi = new BROWSEINFO
            {
                hwndOwner = Application.Current?.MainWindow != null
                    ? new WindowInteropHelper(Application.Current.MainWindow).Handle
                    : IntPtr.Zero,
                lpszTitle = description,
                ulFlags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE
            };

            IntPtr pidl = SHBrowseForFolder(ref bi);
            if (pidl != IntPtr.Zero)
            {
                IntPtr path = Marshal.AllocHGlobal(260 * Marshal.SystemDefaultCharSize);
                try
                {
                    if (SHGetPathFromIDList(pidl, path))
                    {
                        return Marshal.PtrToStringAuto(path);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(path);
                    Marshal.FreeCoTaskMem(pidl);
                }
            }
            return null;
        }

        public override string ModuleId => "WimUtil";
        public override string DisplayName => "Windows Installation Media Utility";
        public string FeatureId => "WimUtil";
        public string Title => "Windows Installation Media Utility";

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

        // Step 1: Select ISO
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

        public WizardActionCard SelectIsoCard { get; private set; } = new();
        public WizardActionCard SelectDirectoryCard { get; private set; } = new();

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

        public WizardActionCard ConvertImageCard { get; private set; } = new();

        // Step 2: Add XML
        [ObservableProperty]
        private string _selectedXmlPath = string.Empty;

        [ObservableProperty]
        private string _xmlStatus = "No XML File Added";

        [ObservableProperty]
        private bool _isXmlAdded;

        public WizardActionCard GenerateWinhanceXmlCard { get; private set; } = new();
        public WizardActionCard DownloadXmlCard { get; private set; } = new();
        public WizardActionCard SelectXmlCard { get; private set; } = new();

        // Step 3: Add Drivers
        [ObservableProperty]
        private bool _areDriversAdded;

        public WizardActionCard ExtractSystemDriversCard { get; private set; } = new();
        public WizardActionCard SelectCustomDriversCard { get; private set; } = new();

        // Step 4: Create ISO
        [ObservableProperty]
        private string _outputIsoPath = string.Empty;

        [ObservableProperty]
        private bool _isOscdimgAvailable;

        [ObservableProperty]
        private bool _isIsoCreated;

        public WizardActionCard DownloadOscdimgCard { get; private set; } = new();
        public WizardActionCard SelectOutputCard { get; private set; } = new();

        public WimUtilViewModel(
            IWimUtilService wimUtilService,
            ITaskProgressService taskProgressService,
            IDialogService dialogService,
            ILogService logService,
            IServiceProvider serviceProvider,
            IThemeManager themeManager)
        {
            _wimUtilService = wimUtilService;
            _taskProgressService = taskProgressService;
            _dialogService = dialogService;
            _logService = logService;
            _serviceProvider = serviceProvider;
            _themeManager = themeManager;

            if (_themeManager is INotifyPropertyChanged notifyPropertyChanged)
            {
                notifyPropertyChanged.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(IThemeManager.IsDarkTheme))
                    {
                        OnPropertyChanged(nameof(IsDarkTheme));
                    }
                };
            }

            WorkingDirectory = Path.Combine(Path.GetTempPath(), "WinhanceWIM");

            InitializeStepStates();
            InitializeActionCards();
        }

        private void InitializeActionCards()
        {
            SelectIsoCard = new WizardActionCard
            {
                Icon = "DiscPlayer",
                Title = "Select Windows ISO",
                Description = "No file selected",
                ButtonText = "Select ISO",
                ButtonCommand = SelectIsoFileCommand,
                IsEnabled = true
            };

            SelectDirectoryCard = new WizardActionCard
            {
                Icon = "FolderOpen",
                Title = "Select working directory",
                Description = $"Default path: {Path.Combine(Path.GetTempPath(), "WinhanceWIM")}",
                ButtonText = "Select Directory",
                ButtonCommand = SelectWorkingDirectoryCommand,
                IsEnabled = true
            };

            GenerateWinhanceXmlCard = new WizardActionCard
            {
                Icon = "Creation",
                Title = "Generate and add Winhance XML",
                Description = "Generate an autounattend.xml file based on your current selections in Winhance",
                ButtonText = "Generate",
                ButtonCommand = GenerateWinhanceXmlCommand,
                IsEnabled = true
            };

            DownloadXmlCard = new WizardActionCard
            {
                Icon = "FileDownload",
                Title = "Download & add UnattendedWinstall XML",
                Description = "Get the latest UnattendedWinstall preconfigured autounattend.xml file (requires internet)",
                ButtonText = "Download",
                ButtonCommand = DownloadUnattendedWinstallXmlCommand,
                IsEnabled = true
            };

            SelectXmlCard = new WizardActionCard
            {
                Icon = "FileCode",
                Title = "Select XML File",
                Description = "Add an autounattend.xml file generated by yourself or services like Winhance, Schneegans or other similar services",
                ButtonText = "Select XML",
                ButtonCommand = SelectXmlFileCommand,
                IsEnabled = true
            };

            ExtractSystemDriversCard = new WizardActionCard
            {
                Icon = "MemoryArrowDown",
                Title = "Extract and add drivers from current OS (Recommended)",
                Description = "Extract the drivers from the current operating system and add it to the installation media",
                ButtonText = "Extract & Add",
                ButtonCommand = ExtractAndAddSystemDriversCommand,
                IsEnabled = true
            };

            SelectCustomDriversCard = new WizardActionCard
            {
                Icon = "FolderOpen",
                Title = "Add other drivers",
                Description = "Select a folder that contains drivers that you want to add to the installation media",
                ButtonText = "Select & Add",
                ButtonCommand = SelectAndAddCustomDriversCommand,
                IsEnabled = true
            };

            DownloadOscdimgCard = new WizardActionCard
            {
                Icon = "DiscAlert",
                Title = "Download & install oscdimg.exe (Required)",
                Description = "The official Windows ADK will be downloaded and Deployment Tools installed. Download time depends on your internet connection speed",
                ButtonText = "Download & Install",
                ButtonCommand = DownloadOscdimgCommand,
                IsEnabled = true
            };

            SelectOutputCard = new WizardActionCard
            {
                Icon = "ContentSaveOutline",
                Title = "Select output location",
                Description = "No location selected",
                ButtonText = "Select Location",
                ButtonCommand = SelectIsoOutputLocationCommand,
                IsEnabled = true
            };

            ConvertImageCard = new WizardActionCard
            {
                Icon = "SwapHorizontal",
                Title = "Convert Image Format",
                Description = "Detecting image format...",
                ButtonText = "Convert",
                ButtonCommand = ConvertImageFormatCommand,
                IsEnabled = false
            };
        }

        public override void OnNavigatedTo(object? parameter = null)
        {
            base.OnNavigatedTo(parameter);

            Task.Run(async () =>
            {
                IsOscdimgAvailable = await _wimUtilService.IsOscdimgAvailableAsync();

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateDownloadOscdimgCardState();
                });

                UpdateStepStates();
            });

            UpdateStepStates();
        }

        private void InitializeStepStates()
        {
            Step1State = new WizardStepState
            {
                StepNumber = 1,
                Title = "Select Windows ISO",
                Icon = "DiscPlayer",
                StatusText = "No ISO Selected",
                IsExpanded = true,
                IsAvailable = true,
                IsComplete = false
            };

            Step2State = new WizardStepState
            {
                StepNumber = 2,
                Title = "Add XML File",
                Icon = "FileCode",
                StatusText = "Complete Step 1 first",
                IsExpanded = false,
                IsAvailable = false,
                IsComplete = false
            };

            Step3State = new WizardStepState
            {
                StepNumber = 3,
                Title = "Extract and Add Drivers",
                Icon = "Chip",
                StatusText = "Complete Step 1 first",
                IsExpanded = false,
                IsAvailable = false,
                IsComplete = false
            };

            Step4State = new WizardStepState
            {
                StepNumber = 4,
                Title = "Create New ISO",
                Icon = "WrenchClock",
                StatusText = "Complete Step 1 first",
                IsExpanded = false,
                IsAvailable = false,
                IsComplete = false
            };
        }

        [RelayCommand]
        private void SelectIsoFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "ISO Files (*.iso)|*.iso|All Files (*.*)|*.*",
                Title = "Select Windows ISO File"
            };

            if (dialog.ShowDialog() == true)
            {
                SelectedIsoPath = dialog.FileName;
                SelectIsoCard.Description = SelectedIsoPath;
                CanStartExtraction = !string.IsNullOrEmpty(SelectedIsoPath) && !string.IsNullOrEmpty(WorkingDirectory);
            }
        }

        partial void OnHasExtractedIsoAlreadyChanged(bool value)
        {
            if (value)
            {
                SelectDirectoryCard.Description = "Select a folder containing already extracted Windows ISO files";
            }
            else
            {
                SelectDirectoryCard.Description = $"Default path: {Path.Combine(Path.GetTempPath(), "WinhanceWIM")}";
            }
        }

        partial void OnCurrentImageFormatChanged(ImageFormatInfo? value)
        {
            UpdateConversionCardState();
        }

        partial void OnIsExtractionCompleteChanged(bool value)
        {
            if (value)
            {
                Task.Run(async () =>
                {
                    await DetectImageFormatAsync();
                });
            }
        }

        [RelayCommand]
        private async Task SelectWorkingDirectory()
        {
            var selectedPath = ShowFolderBrowserDialog(
                HasExtractedIsoAlready
                    ? "Select folder with extracted ISO contents"
                    : "Select a working directory to extract the ISO"
            );

            if (string.IsNullOrEmpty(selectedPath))
                return;

            if (HasExtractedIsoAlready)
            {
                var isValid = await ValidateExtractedIsoDirectory(selectedPath);

                if (isValid)
                {
                    WorkingDirectory = selectedPath;
                    SelectDirectoryCard.Description = $"Using: {WorkingDirectory}";
                    IsExtractionComplete = true;
                    UpdateStepStates();

                    var dialog = CustomDialog.CreateInformationDialog(
                        "Validation Complete",
                        "The selected directory contains valid Windows ISO files. You can proceed to the next step.",
                        "",
                        DialogType.Success,
                        "CheckCircle"
                    );
                    dialog.ShowDialog();
                }
                else
                {
                    WorkingDirectory = string.Empty;
                    SelectDirectoryCard.Description = "Invalid directory. Please select a folder with extracted ISO contents.";

                    var dialog = CustomDialog.CreateInformationDialog(
                        "Invalid Directory",
                        "The selected directory is not valid:\n\n" +
                        "• It might be a mounted ISO file (must be extracted to a real folder)\n" +
                        "• It might be read-only or on a CD/DVD drive\n" +
                        "• It might not contain the required 'sources' and 'boot' folders\n\n" +
                        "Please extract the ISO contents to a writable folder on your hard drive first.",
                        "",
                        DialogType.Error,
                        "CloseCircle"
                    );
                    dialog.ShowDialog();
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
                    _logService.LogInformation($"Working directory set to: {WorkingDirectory}");
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Failed to create working directory: {ex.Message}", ex);
                    SelectDirectoryCard.Description = "Failed to create directory. Please try another location.";
                    WorkingDirectory = string.Empty;
                }
            }
        }

        private async Task<bool> ValidateExtractedIsoDirectory(string path)
        {
            try
            {
                var pathRoot = Path.GetPathRoot(path);
                if (!string.IsNullOrEmpty(pathRoot) &&
                    path.TrimEnd('\\', '/').Equals(pathRoot.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                {
                    _logService.LogError($"Path appears to be a mounted drive: {path}");
                    return false;
                }

                var driveInfo = new DriveInfo(path);
                if (driveInfo.DriveType == DriveType.CDRom)
                {
                    _logService.LogError($"Path is a CD/DVD drive or mounted ISO: {path}");
                    return false;
                }

                var testFile = Path.Combine(path, $".winhance_write_test_{Guid.NewGuid()}.tmp");
                try
                {
                    await File.WriteAllTextAsync(testFile, "test");
                    File.Delete(testFile);
                }
                catch (UnauthorizedAccessException)
                {
                    _logService.LogError($"Path is read-only (likely mounted ISO): {path}");
                    return false;
                }
                catch (IOException ex) when (ex.Message.Contains("read-only"))
                {
                    _logService.LogError($"Path is read-only (likely mounted ISO): {path}");
                    return false;
                }

                var extractedDirs = Directory.GetDirectories(path);
                var dirNames = extractedDirs.Select(d => Path.GetFileName(d)).ToList();

                _logService.LogInformation($"Validating directory. Found {extractedDirs.Length} folders: {string.Join(", ", dirNames)}");

                var hasSourcesDir = extractedDirs.Any(d =>
                    Path.GetFileName(d).Equals("sources", StringComparison.OrdinalIgnoreCase));
                var hasBootDir = extractedDirs.Any(d =>
                    Path.GetFileName(d).Equals("boot", StringComparison.OrdinalIgnoreCase));

                if (!hasSourcesDir || !hasBootDir)
                {
                    _logService.LogError($"Directory validation failed. Expected 'sources' and 'boot' folders. Found: {string.Join(", ", dirNames)}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error validating directory: {ex.Message}", ex);
                return false;
            }
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

                var success = await _wimUtilService.ExtractIsoAsync(
                    SelectedIsoPath,
                    WorkingDirectory,
                    progress,
                    _taskProgressService.CurrentTaskCancellationSource.Token
                );

                if (success)
                {
                    SelectIsoCard.IsComplete = true;
                    SelectIsoCard.IsEnabled = true;
                    SelectIsoCard.Opacity = 1.0;
                    SelectDirectoryCard.IsEnabled = true;
                    SelectDirectoryCard.Opacity = 1.0;
                    SelectIsoCard.Description = "ISO extracted successfully!";
                    SelectIsoCard.DescriptionForeground = new SolidColorBrush(Color.FromRgb(27, 94, 32));
                    IsExtracting = false;
                    IsExtractionComplete = true;
                    UpdateStepStates();

                    var dialog = CustomDialog.CreateInformationDialog(
                        "Extraction Complete",
                        "ISO has been extracted successfully. You can now proceed to the next step.",
                        "",
                        DialogType.Success,
                        "CheckCircle"
                    );
                    dialog.ShowDialog();
                }
                else
                {
                    SelectIsoCard.HasFailed = true;
                    SelectIsoCard.IsEnabled = true;
                    SelectIsoCard.Opacity = 1.0;
                    SelectDirectoryCard.IsEnabled = true;
                    SelectDirectoryCard.Opacity = 1.0;
                    SelectIsoCard.Description = "ISO extraction failed";
                    SelectIsoCard.DescriptionForeground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
                    IsExtracting = false;
                    UpdateStepStates();

                    var dialog = CustomDialog.CreateInformationDialog(
                        "Extraction Failed",
                        "Failed to extract ISO. Please check that the ISO file is valid and you have sufficient disk space.",
                        "",
                        DialogType.Error,
                        "CloseCircle"
                    );
                    dialog.ShowDialog();
                }
            }
            catch (OperationCanceledException)
            {
                SelectIsoCard.IsEnabled = true;
                SelectIsoCard.Opacity = 1.0;
                SelectDirectoryCard.IsEnabled = true;
                SelectDirectoryCard.Opacity = 1.0;
                IsExtracting = false;
                UpdateStepStates();

                SelectIsoCard.Description = "ISO extraction cancelled";
                SelectIsoCard.DescriptionForeground = new SolidColorBrush(Color.FromRgb(255, 152, 0));

            }
            catch (InsufficientDiskSpaceException spaceEx)
            {
                SelectIsoCard.HasFailed = true;
                SelectIsoCard.IsEnabled = true;
                SelectIsoCard.Opacity = 1.0;
                SelectDirectoryCard.IsEnabled = true;
                SelectDirectoryCard.Opacity = 1.0;
                IsExtracting = false;
                UpdateStepStates();

                SelectIsoCard.Description = $"Insufficient disk space on {spaceEx.DriveName}";
                SelectIsoCard.DescriptionForeground = new SolidColorBrush(Color.FromRgb(198, 40, 40));

                _logService.LogError($"Insufficient disk space for ISO extraction: {spaceEx.Message}", spaceEx);

                var dialog = CustomDialog.CreateInformationDialog(
                    "Insufficient Disk Space",
                    $"There is not enough space on drive {spaceEx.DriveName} to extract the ISO.\n\n" +
                    $"Required: {spaceEx.RequiredGB:F2} GB\n" +
                    $"Available: {spaceEx.AvailableGB:F2} GB\n" +
                    $"Needed: {(spaceEx.RequiredGB - spaceEx.AvailableGB):F2} GB more\n\n" +
                    $"Please free up space or select another location with more free space.",
                    "",
                    DialogType.Warning,
                    "Alert"
                );
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                SelectIsoCard.HasFailed = true;
                SelectIsoCard.IsEnabled = true;
                SelectIsoCard.Opacity = 1.0;
                SelectDirectoryCard.IsEnabled = true;
                SelectDirectoryCard.Opacity = 1.0;
                SelectIsoCard.Description = $"Error: {ex.Message}";
                SelectIsoCard.DescriptionForeground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
                IsExtracting = false;
                UpdateStepStates();
                _logService.LogError($"Error extracting ISO: {ex.Message}", ex);

                var dialog = CustomDialog.CreateInformationDialog(
                    "Extraction Error",
                    $"An error occurred: {ex.Message}",
                    "",
                    DialogType.Error,
                    "CloseCircle"
                );
                dialog.ShowDialog();
            }
            finally
            {
                _taskProgressService.CompleteTask();
            }
        }

        private void ClearOtherXmlCardCompletions(string exceptCard)
        {
            if (exceptCard != "generate")
                GenerateWinhanceXmlCard.IsComplete = false;

            if (exceptCard != "download")
                DownloadXmlCard.IsComplete = false;

            if (exceptCard != "select")
                SelectXmlCard.IsComplete = false;
        }

        [RelayCommand]
        private async Task GenerateWinhanceXml()
        {
            try
            {
                GenerateWinhanceXmlCard.IsComplete = false;
                GenerateWinhanceXmlCard.HasFailed = false;

                var confirmDialog = CustomDialog.CreateConfirmationDialog(
                    "Generate Autounattend XML",
                    "You can generate an autounattend.xml file to add to a Windows ISO to customize Windows during installation based on your selections in Winhance.\n\n" +
                    "How this works:\n" +
                    "• Apps selected on the Windows Apps screen will be uninstalled automatically\n" +
                    "• Settings in the Optimize and Customize screens will be added according to their current state in Winhance\n\n" +
                    "If you're sure your selections are correct you can continue.\nIf not, hit no, review your app and setting selections, and come back here.\n\n" +
                    "Do you want to generate the XML file and add it to the media?",
                    "",
                    DialogType.Information,
                    "Information"
                );

                if (confirmDialog.ShowDialog() != true)
                    return;

                XmlStatus = "Generating Winhance XML...";

                var xmlGeneratorService = _serviceProvider.GetRequiredService<IAutounattendXmlGeneratorService>();
                var outputPath = Path.Combine(WorkingDirectory, "autounattend.xml");

                var generatedPath = await xmlGeneratorService.GenerateFromCurrentSelectionsAsync(outputPath);

                SelectedXmlPath = generatedPath;
                IsXmlAdded = true;
                XmlStatus = "Winhance XML generated and added successfully!";
                ClearOtherXmlCardCompletions("generate");
                GenerateWinhanceXmlCard.IsComplete = true;
                UpdateStepStates();

                _logService.LogInformation($"Winhance XML generated: {generatedPath}");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error generating Winhance XML: {ex.Message}", ex);
                XmlStatus = $"Generation failed: {ex.Message}";
                GenerateWinhanceXmlCard.HasFailed = true;

                var errorDialog = CustomDialog.CreateInformationDialog(
                    "Generation Error",
                    $"Failed to generate XML: {ex.Message}",
                    "",
                    DialogType.Error,
                    "CloseCircle"
                );
                errorDialog.ShowDialog();
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
                var progress = new Progress<TaskProgressDetail>(detail =>
                {
                    XmlStatus = detail.StatusText ?? "Downloading XML...";
                });

                XmlStatus = "Downloading latest UnattendedWinstall XML...";
                await _wimUtilService.DownloadUnattendedWinstallXmlAsync(
                    destinationPath,
                    progress,
                    _cancellationTokenSource.Token
                );

                var addSuccess = await _wimUtilService.AddXmlToImageAsync(destinationPath, WorkingDirectory);

                if (addSuccess)
                {
                    SelectedXmlPath = destinationPath;
                    IsXmlAdded = true;
                    XmlStatus = "XML downloaded and added successfully!";
                    ClearOtherXmlCardCompletions("download");
                    DownloadXmlCard.IsComplete = true;
                    UpdateStepStates();

                    _logService.LogInformation($"UnattendedWinstall XML downloaded and added: {destinationPath}");
                }
                else
                {
                    DownloadXmlCard.HasFailed = true;
                    XmlStatus = "Downloaded but failed to add to media";

                    var dialog = CustomDialog.CreateInformationDialog(
                        "Addition Failed",
                        "XML was downloaded but could not be added to the installation media.",
                        "",
                        DialogType.Error,
                        "CloseCircle"
                    );
                    dialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error downloading XML: {ex.Message}", ex);
                XmlStatus = $"Download failed: {ex.Message}";
                DownloadXmlCard.HasFailed = true;

                var dialog = CustomDialog.CreateInformationDialog(
                    "Download Error",
                    $"Failed to download XML: {ex.Message}",
                    "",
                    DialogType.Error,
                    "CloseCircle"
                );
                dialog.ShowDialog();
            }
        }

        [RelayCommand]
        private async Task SelectXmlFile()
        {
            try
            {
                SelectXmlCard.IsComplete = false;
                SelectXmlCard.HasFailed = false;

                var dialog = new OpenFileDialog
                {
                    Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                    Title = "Select autounattend.xml File"
                };

                if (dialog.ShowDialog() != true)
                    return;

                var selectedPath = dialog.FileName;

                XmlStatus = "Validating XML file...";
                var isValidXml = await ValidateXmlFile(selectedPath);

                if (!isValidXml)
                {
                    SelectXmlCard.HasFailed = true;
                    XmlStatus = "Invalid XML file";

                    var errorDialog = CustomDialog.CreateInformationDialog(
                        "Invalid XML",
                        "The selected file is not a valid XML file. Please check the file and try again.",
                        "",
                        DialogType.Error,
                        "CloseCircle"
                    );
                    errorDialog.ShowDialog();
                    return;
                }

                XmlStatus = "Adding XML to media...";
                var addSuccess = await _wimUtilService.AddXmlToImageAsync(selectedPath, WorkingDirectory);

                if (addSuccess)
                {
                    SelectedXmlPath = selectedPath;
                    IsXmlAdded = true;
                    XmlStatus = "XML validated and added successfully!";
                    ClearOtherXmlCardCompletions("select");
                    SelectXmlCard.IsComplete = true;
                    UpdateStepStates();

                    _logService.LogInformation($"Custom XML validated and added: {selectedPath}");
                }
                else
                {
                    SelectXmlCard.HasFailed = true;
                    XmlStatus = "Valid XML but failed to add to media";

                    var errorDialog = CustomDialog.CreateInformationDialog(
                        "Addition Failed",
                        "XML is valid but could not be added to the installation media.",
                        "",
                        DialogType.Error,
                        "CloseCircle"
                    );
                    errorDialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error selecting XML: {ex.Message}", ex);
                XmlStatus = $"Error: {ex.Message}";
                SelectXmlCard.HasFailed = true;

                var errorDialog = CustomDialog.CreateInformationDialog(
                    "Selection Error",
                    $"An error occurred: {ex.Message}",
                    "",
                    DialogType.Error,
                    "CloseCircle"
                );
                errorDialog.ShowDialog();
            }
        }

        private async Task<bool> ValidateXmlFile(string xmlPath)
        {
            try
            {
                _logService.LogInformation($"Validating XML file: {xmlPath}");

                await Task.Run(() =>
                {
                    var doc = XDocument.Load(xmlPath);
                });

                _logService.LogInformation("XML validation successful");
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"XML validation failed: {ex.Message}", ex);
                return false;
            }
        }

        [RelayCommand]
        private void OpenSchneegansXmlGenerator()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://schneegans.de/windows/unattend-generator/",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error opening Schneegans XML generator: {ex.Message}", ex);
            }
        }

        [RelayCommand]
        private async Task ExtractAndAddSystemDrivers()
        {
            try
            {
                ExtractSystemDriversCard.IsComplete = false;
                ExtractSystemDriversCard.HasFailed = false;

                var confirmDialog = CustomDialog.CreateConfirmationDialog(
                    "Extract System Drivers",
                    "This will extract all third-party drivers from your current Windows installation and add them to the installation media.\n\n" +
                    "This operation may take several minutes depending on the number of drivers installed.\n\n" +
                    "Do you want to continue?",
                    "",
                    DialogType.Information,
                    "Information"
                );

                if (confirmDialog.ShowDialog() != true)
                    return;

                ExtractSystemDriversCard.IsProcessing = true;
                ExtractSystemDriversCard.IsEnabled = false;

                _cancellationTokenSource = new CancellationTokenSource();
                var progress = new Progress<TaskProgressDetail>(detail => { });

                var success = await _wimUtilService.AddDriversAsync(
                    WorkingDirectory,
                    null,
                    progress,
                    _cancellationTokenSource.Token
                );

                ExtractSystemDriversCard.IsProcessing = false;
                ExtractSystemDriversCard.IsEnabled = true;

                if (success)
                {
                    AreDriversAdded = true;
                    ExtractSystemDriversCard.IsComplete = true;
                    UpdateStepStates();

                    _logService.LogInformation("System drivers extracted and added successfully");

                    var successDialog = CustomDialog.CreateInformationDialog(
                        "Drivers Extracted Successfully",
                        "System drivers have been extracted and added to the installation media. They will be automatically installed during Windows setup.",
                        "",
                        DialogType.Success,
                        "CheckCircle"
                    );
                    successDialog.ShowDialog();
                }
                else
                {
                    ExtractSystemDriversCard.HasFailed = true;

                    var errorDialog = CustomDialog.CreateInformationDialog(
                        "No Drivers Found",
                        "No third-party drivers were found on your current system, or the extraction failed.\n\n" +
                        "This can happen if:\n" +
                        "• Your system only uses built-in Windows drivers\n" +
                        "• The drivers are not exportable\n" +
                        "• Insufficient permissions to export drivers",
                        "",
                        DialogType.Warning,
                        "Alert"
                    );
                    errorDialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error extracting system drivers: {ex.Message}", ex);
                ExtractSystemDriversCard.IsProcessing = false;
                ExtractSystemDriversCard.IsEnabled = true;
                ExtractSystemDriversCard.HasFailed = true;

                var errorDialog = CustomDialog.CreateInformationDialog(
                    "Driver Extraction Error",
                    $"An error occurred while extracting drivers: {ex.Message}",
                    "",
                    DialogType.Error,
                    "CloseCircle"
                );
                errorDialog.ShowDialog();
            }
        }

        [RelayCommand]
        private async Task SelectAndAddCustomDrivers()
        {
            try
            {
                SelectCustomDriversCard.IsComplete = false;
                SelectCustomDriversCard.HasFailed = false;

                var selectedPath = ShowFolderBrowserDialog("Select folder containing drivers");

                if (string.IsNullOrEmpty(selectedPath))
                {
                    _logService.LogInformation("User cancelled driver folder selection");
                    return;
                }

                if (!Directory.Exists(selectedPath))
                {
                    SelectCustomDriversCard.HasFailed = true;

                    var errorDialog = CustomDialog.CreateInformationDialog(
                        "Invalid Folder",
                        "The selected folder does not exist. Please try again.",
                        "",
                        DialogType.Error,
                        "CloseCircle"
                    );
                    errorDialog.ShowDialog();
                    return;
                }

                var hasFiles = Directory.EnumerateFileSystemEntries(selectedPath, "*", SearchOption.AllDirectories).Any();
                if (!hasFiles)
                {
                    SelectCustomDriversCard.HasFailed = true;

                    var errorDialog = CustomDialog.CreateInformationDialog(
                        "Empty Folder",
                        "The selected folder appears to be empty. Please select a folder containing driver files.",
                        "",
                        DialogType.Warning,
                        "Alert"
                    );
                    errorDialog.ShowDialog();
                    return;
                }

                SelectCustomDriversCard.IsProcessing = true;
                SelectCustomDriversCard.IsEnabled = false;

                _cancellationTokenSource = new CancellationTokenSource();
                var progress = new Progress<TaskProgressDetail>(detail => { });

                SelectCustomDriversCard.Description = $"Selected: {selectedPath}";

                var success = await _wimUtilService.AddDriversAsync(
                    WorkingDirectory,
                    selectedPath,
                    progress,
                    _cancellationTokenSource.Token
                );

                SelectCustomDriversCard.IsProcessing = false;
                SelectCustomDriversCard.IsEnabled = true;

                if (success)
                {
                    AreDriversAdded = true;
                    SelectCustomDriversCard.IsComplete = true;
                    UpdateStepStates();

                    _logService.LogInformation($"Custom drivers added from: {selectedPath}");

                    var successDialog = CustomDialog.CreateInformationDialog(
                        "Drivers Added Successfully",
                        "Driver files have been added to the installation media and will be automatically installed during Windows setup.",
                        "",
                        DialogType.Success,
                        "CheckCircle"
                    );
                    successDialog.ShowDialog();
                }
                else
                {
                    SelectCustomDriversCard.HasFailed = true;

                    var errorDialog = CustomDialog.CreateInformationDialog(
                        "No Drivers Found",
                        $"No driver files (.inf) were found in the selected directory:\n\n{selectedPath}\n\n" +
                        "Please ensure:\n" +
                        "• The directory contains driver files with .inf extensions\n" +
                        "• You have selected the correct folder\n" +
                        "• The drivers are compatible with your Windows version",
                        "",
                        DialogType.Warning,
                        "Alert"
                    );
                    errorDialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error adding custom drivers: {ex.Message}", ex);
                SelectCustomDriversCard.IsProcessing = false;
                SelectCustomDriversCard.IsEnabled = true;
                SelectCustomDriversCard.HasFailed = true;

                var errorDialog = CustomDialog.CreateInformationDialog(
                    "Driver Addition Error",
                    $"An error occurred while adding drivers: {ex.Message}",
                    "",
                    DialogType.Error,
                    "CloseCircle"
                );
                errorDialog.ShowDialog();
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
                var progress = new Progress<TaskProgressDetail>(detail =>
                {
                    // Progress is reported via TaskProgressService
                });

                var success = await _wimUtilService.EnsureOscdimgAvailableAsync(
                    progress,
                    _cancellationTokenSource.Token
                );

                DownloadOscdimgCard.IsProcessing = false;

                if (success)
                {
                    IsOscdimgAvailable = true;
                    DownloadOscdimgCard.IsComplete = true;
                    DownloadOscdimgCard.IsEnabled = false;
                    DownloadOscdimgCard.ButtonText = "oscdimg.exe found";
                    DownloadOscdimgCard.Description = "Successfully installed from Windows ADK";
                    DownloadOscdimgCard.Icon = "CheckCircle";

                    UpdateStepStates();

                    var dialog = CustomDialog.CreateInformationDialog(
                        "Installation Complete",
                        "Windows ADK Deployment Tools have been installed successfully. oscdimg.exe is now available.",
                        "",
                        DialogType.Success,
                        "CheckCircle"
                    );
                    dialog.ShowDialog();
                }
                else
                {
                    DownloadOscdimgCard.IsEnabled = true;
                    DownloadOscdimgCard.HasFailed = true;

                    var dialog = CustomDialog.CreateInformationDialog(
                        "Installation Failed",
                        "Failed to install Windows ADK Deployment Tools. Please check your internet connection and try again.",
                        "",
                        DialogType.Error,
                        "CloseCircle"
                    );
                    dialog.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error installing ADK: {ex.Message}", ex);

                DownloadOscdimgCard.IsProcessing = false;
                DownloadOscdimgCard.IsEnabled = true;
                DownloadOscdimgCard.HasFailed = true;

                var dialog = CustomDialog.CreateInformationDialog(
                    "Installation Error",
                    $"An error occurred: {ex.Message}",
                    "",
                    DialogType.Error,
                    "CloseCircle"
                );
                dialog.ShowDialog();
            }
        }

        [RelayCommand]
        private void SelectIsoOutputLocation()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "ISO Files (*.iso)|*.iso",
                Title = "Select Output Location for New ISO",
                FileName = "Winhance_Windows.iso"
            };

            if (dialog.ShowDialog() == true)
            {
                OutputIsoPath = dialog.FileName;
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
                    var dialog = CustomDialog.CreateInformationDialog(
                        "oscdimg.exe Required",
                        "Please download oscdimg.exe first by clicking the 'Download oscdimg' button.",
                        "",
                        DialogType.Warning,
                        "Alert"
                    );
                    dialog.ShowDialog();
                    return;
                }

                if (string.IsNullOrEmpty(OutputIsoPath))
                {
                    var dialog = CustomDialog.CreateInformationDialog(
                        "Output Location Required",
                        "Please select an output location for the ISO file.",
                        "",
                        DialogType.Warning,
                        "Alert"
                    );
                    dialog.ShowDialog();
                    return;
                }

                SelectOutputCard.IsEnabled = false;
                SelectOutputCard.Opacity = 0.5;

                _taskProgressService.StartTask("Creating ISO", true);
                var progress = _taskProgressService.CreatePowerShellProgress();

                var success = await _wimUtilService.CreateIsoAsync(
                    WorkingDirectory,
                    OutputIsoPath,
                    progress,
                    _taskProgressService.CurrentTaskCancellationSource.Token
                );

                if (success)
                {
                    SelectOutputCard.IsEnabled = true;
                    SelectOutputCard.Opacity = 1.0;
                    IsIsoCreated = true;
                    SelectOutputCard.Description = "ISO created successfully!";
                    SelectOutputCard.DescriptionForeground = new SolidColorBrush(Color.FromRgb(27, 94, 32));
                    UpdateStepStates();

                    var dialog = CustomDialog.CreateConfirmationDialog(
                        "ISO Created Successfully",
                        $"Your Windows installation ISO has been created successfully!\n\n" +
                        $"Location: {OutputIsoPath}\n\n" +
                        "Would you like to open the folder containing the ISO?",
                        "",
                        DialogType.Success,
                        "CheckCircle"
                    );

                    if (dialog.ShowDialog() == true)
                    {
                        Process.Start("explorer.exe", $"/select,\"{OutputIsoPath}\"");
                    }
                }
                else
                {
                    SelectOutputCard.IsEnabled = true;
                    SelectOutputCard.Opacity = 1.0;
                    SelectOutputCard.Description = "Failed to create ISO";
                    SelectOutputCard.DescriptionForeground = new SolidColorBrush(Color.FromRgb(198, 40, 40));

                    var dialog = CustomDialog.CreateInformationDialog(
                        "ISO Creation Failed",
                        "Failed to create ISO file. Please check the logs for details.",
                        "",
                        DialogType.Error,
                        "CloseCircle"
                    );
                    dialog.ShowDialog();
                }
            }
            catch (OperationCanceledException)
            {
                SelectOutputCard.IsEnabled = true;
                SelectOutputCard.Opacity = 1.0;

                try
                {
                    if (File.Exists(OutputIsoPath))
                    {
                        File.Delete(OutputIsoPath);
                        _logService.LogInformation($"Cleaned up incomplete ISO: {OutputIsoPath}");
                    }
                }
                catch (Exception cleanupEx)
                {
                    _logService.LogWarning($"Could not cleanup ISO file: {cleanupEx.Message}");
                }

                SelectOutputCard.Description = "ISO creation cancelled";
                SelectOutputCard.DescriptionForeground = new SolidColorBrush(Color.FromRgb(255, 152, 0));

            }
            catch (InsufficientDiskSpaceException spaceEx)
            {
                SelectOutputCard.IsEnabled = true;
                SelectOutputCard.Opacity = 1.0;

                SelectOutputCard.Description = $"Insufficient disk space on {spaceEx.DriveName}";
                SelectOutputCard.DescriptionForeground = new SolidColorBrush(Color.FromRgb(198, 40, 40));

                _logService.LogError($"Insufficient disk space for ISO creation: {spaceEx.Message}", spaceEx);

                var dialog = CustomDialog.CreateInformationDialog(
                    "Insufficient Disk Space",
                    $"There is not enough space on drive {spaceEx.DriveName} to create the ISO.\n\n" +
                    $"Required: {spaceEx.RequiredGB:F2} GB\n" +
                    $"Available: {spaceEx.AvailableGB:F2} GB\n" +
                    $"Needed: {(spaceEx.RequiredGB - spaceEx.AvailableGB):F2} GB more\n\n" +
                    $"Please free up space or select another location with more free space.",
                    "",
                    DialogType.Warning,
                    "Alert"
                );
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                SelectOutputCard.IsEnabled = true;
                SelectOutputCard.Opacity = 1.0;
                _logService.LogError($"Error creating ISO: {ex.Message}", ex);
                SelectOutputCard.Description = $"Error: {ex.Message}";
                SelectOutputCard.DescriptionForeground = new SolidColorBrush(Color.FromRgb(198, 40, 40));

                var dialog = CustomDialog.CreateInformationDialog(
                    "ISO Creation Error",
                    $"An error occurred: {ex.Message}",
                    "",
                    DialogType.Error,
                    "CloseCircle"
                );
                dialog.ShowDialog();
            }
            finally
            {
                _taskProgressService.CompleteTask();
            }
        }

        [RelayCommand]
        private async Task NavigateToStep(object stepParameter)
        {
            if (stepParameter is not string stepString || !int.TryParse(stepString, out int targetStep))
                return;

            if (targetStep == CurrentStep)
            {
                CurrentStep = 0;
                UpdateStepStates();
                return;
            }

            if (!IsStepAvailable(targetStep))
            {
                var dialog = CustomDialog.CreateInformationDialog(
                    "Step Not Available",
                    "Please complete Step 1 (Select Windows ISO) before accessing this step.",
                    "",
                    DialogType.Information,
                    "Information"
                );
                dialog.ShowDialog();
                return;
            }

            CurrentStep = targetStep;
            UpdateStepStates();
        }

        private bool IsStepAvailable(int step)
        {
            return step switch
            {
                1 => true,
                2 => IsExtractionComplete && !IsConverting,
                3 => IsExtractionComplete && !IsConverting,
                4 => IsExtractionComplete && !IsConverting,
                _ => false
            };
        }

        private void UpdateDownloadOscdimgCardState()
        {
            if (IsOscdimgAvailable)
            {
                DownloadOscdimgCard.IsEnabled = false;
                DownloadOscdimgCard.IsComplete = true;
                DownloadOscdimgCard.ButtonText = "oscdimg.exe found";
                DownloadOscdimgCard.Description = "Required to create bootable ISO files, but was already found on your system, no download needed";
                DownloadOscdimgCard.Icon = "CheckCircle";
            }
            else
            {
                DownloadOscdimgCard.IsEnabled = true;
                DownloadOscdimgCard.IsComplete = false;
                DownloadOscdimgCard.ButtonText = "Download";
                DownloadOscdimgCard.Description = "The official Windows ADK will be downloaded and Deployment Tools installed. Download time depends on your internet connection speed";
                DownloadOscdimgCard.Icon = "Download";
            }
        }

        private void UpdateStepStates()
        {
            Step1State.IsExpanded = CurrentStep == 1;
            Step1State.IsAvailable = true;
            Step1State.IsComplete = IsExtractionComplete && !IsConverting;
            Step1State.StatusText = GetStep1StatusText();

            Step2State.IsExpanded = CurrentStep == 2;
            Step2State.IsAvailable = IsExtractionComplete && !IsConverting;
            Step2State.IsComplete = IsXmlAdded;
            Step2State.StatusText = GetStep2StatusText();

            Step3State.IsExpanded = CurrentStep == 3;
            Step3State.IsAvailable = IsExtractionComplete && !IsConverting;
            Step3State.IsComplete = AreDriversAdded;
            Step3State.StatusText = GetStep3StatusText();

            Step4State.IsExpanded = CurrentStep == 4;
            Step4State.IsAvailable = IsExtractionComplete && !IsConverting;
            Step4State.IsComplete = IsIsoCreated;
            Step4State.StatusText = GetStep4StatusText();

            OnPropertyChanged(nameof(Step1State));
            OnPropertyChanged(nameof(Step2State));
            OnPropertyChanged(nameof(Step3State));
            OnPropertyChanged(nameof(Step4State));
        }

        private string GetStep1StatusText()
        {
            if (IsConverting) return "Converting image format...";
            if (IsExtractionComplete) return "✓ ISO Extracted";
            if (IsExtracting) return "Extracting ISO contents...";
            if (!string.IsNullOrEmpty(SelectedIsoPath)) return "ISO selected, ready to start extraction";
            return "No ISO Selected";
        }

        private string GetStep2StatusText()
        {
            if (IsConverting) return "🔒 Wait for conversion to complete";
            if (!IsExtractionComplete) return "🔒 Complete Step 1";
            if (IsXmlAdded) return "✓ XML Added";
            if (!string.IsNullOrEmpty(SelectedXmlPath)) return $"Selected: {Path.GetFileName(SelectedXmlPath)}";
            return "No XML Added (Optional)";
        }

        private string GetStep3StatusText()
        {
            if (IsConverting) return "🔒 Wait for conversion to complete";
            if (!IsExtractionComplete) return "🔒 Complete Step 1";
            if (AreDriversAdded) return "✓ Drivers Added";
            return "No Drivers Added (Optional)";
        }

        private string GetStep4StatusText()
        {
            if (IsConverting) return "🔒 Wait for conversion to complete";
            if (!IsExtractionComplete) return "🔒 Complete Step 1";
            if (IsIsoCreated) return "✓ ISO Created";
            if (!string.IsNullOrEmpty(OutputIsoPath)) return $"Output: {Path.GetFileName(OutputIsoPath)}";
            return "Ready to Create ISO";
        }

        [RelayCommand]
        private async Task OpenWindows10Download()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.microsoft.com/software-download/windows10",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error opening Windows 10 download page: {ex.Message}", ex);
            }
        }

        [RelayCommand]
        private async Task OpenWindows11Download()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://www.microsoft.com/software-download/windows11",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error opening Windows 11 download page: {ex.Message}", ex);
            }
        }

        private async Task DetectImageFormatAsync()
        {
            try
            {
                _logService.LogInformation("Detecting image format...");

                var formatInfo = await _wimUtilService.DetectImageFormatAsync(WorkingDirectory);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    CurrentImageFormat = formatInfo;
                    ShowConversionCard = formatInfo != null;
                    UpdateConversionCardState();
                });
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error detecting image format: {ex.Message}", ex);
                ShowConversionCard = false;
            }
        }

        private void UpdateConversionCardState()
        {
            if (CurrentImageFormat == null)
            {
                ConvertImageCard.IsEnabled = false;
                ConvertImageCard.Description = "No image file detected";
                return;
            }

            var currentFormat = CurrentImageFormat.Format == ImageFormat.Wim ? "WIM" : "ESD";
            var targetFormat = CurrentImageFormat.Format == ImageFormat.Wim ? "ESD" : "WIM";
            var currentSize = CurrentImageFormat.FileSizeBytes / (1024.0 * 1024 * 1024);

            var estimatedTargetSize = CurrentImageFormat.Format == ImageFormat.Wim
                ? currentSize * 0.65
                : currentSize * 1.50;

            var sizeChange = CurrentImageFormat.Format == ImageFormat.Wim
                ? $"Save ~{(currentSize - estimatedTargetSize):F2} GB"
                : $"Requires ~{(estimatedTargetSize - currentSize):F2} GB more space";

            ConvertImageCard.Icon = CurrentImageFormat.Format == ImageFormat.Wim
                ? "ArrowCollapseAll"
                : "ArrowExpandAll";

            ConvertImageCard.Title = $"Convert {currentFormat} to {targetFormat} Format (Optional)";

            var performanceNote = CurrentImageFormat.Format == ImageFormat.Wim
                ? "Creates smaller ISO but slower Windows installation"
                : "Creates larger ISO but faster Windows installation";

            ConvertImageCard.Description =
                $"Current: install.{currentFormat.ToLower()} ({currentSize:F2} GB)\n" +
                $"After conversion: ~{estimatedTargetSize:F2} GB ({sizeChange})\n" +
                $"{performanceNote}";

            ConvertImageCard.ButtonText = $"Convert to {targetFormat}";
            ConvertImageCard.IsEnabled = !IsConverting;

            _logService.LogInformation(
                $"Format detected: {currentFormat}, " +
                $"{CurrentImageFormat.ImageCount} editions, " +
                $"{currentSize:F2} GB"
            );
        }

        [RelayCommand]
        private async Task ConvertImageFormat()
        {
            if (CurrentImageFormat == null) return;

            try
            {
                var targetFormat = CurrentImageFormat.Format == ImageFormat.Wim
                    ? ImageFormat.Esd
                    : ImageFormat.Wim;

                var targetFormatName = targetFormat == ImageFormat.Wim ? "WIM" : "ESD";
                var currentFormatName = CurrentImageFormat.Format == ImageFormat.Wim ? "WIM" : "ESD";

                var confirmMessage = targetFormat == ImageFormat.Esd
                    ? $"Convert to ESD format?\n\n" +
                      $"✓ Smaller ISO size (save ~{(CurrentImageFormat.FileSizeBytes * 0.35 / (1024.0 * 1024 * 1024)):F2} GB)\n" +
                      $"✗ Slower Windows installation\n\n" +
                      $"This process will take 10-20 minutes.\n\n" +
                      $"Continue?"
                    : $"Convert to WIM format?\n\n" +
                      $"✓ Faster Windows installation\n" +
                      $"✗ Larger ISO size (needs ~{(CurrentImageFormat.FileSizeBytes * 0.50 / (1024.0 * 1024 * 1024)):F2} GB more space)\n\n" +
                      $"This process will take 10-20 minutes.\n\n" +
                      $"Continue?";

                var confirmDialog = CustomDialog.CreateConfirmationDialog(
                    $"Convert {currentFormatName} to {targetFormatName}",
                    confirmMessage,
                    "",
                    DialogType.Information,
                    "Information"
                );

                if (confirmDialog.ShowDialog() != true)
                    return;

                IsConverting = true;
                ConvertImageCard.IsProcessing = true;
                ConvertImageCard.IsEnabled = false;
                ConversionStatus = $"Converting {currentFormatName} to {targetFormatName}...";
                UpdateStepStates();

                _taskProgressService.StartTask($"Converting to {targetFormatName}", true);
                var progress = _taskProgressService.CreatePowerShellProgress();

                var success = await _wimUtilService.ConvertImageAsync(
                    WorkingDirectory,
                    targetFormat,
                    progress,
                    _taskProgressService.CurrentTaskCancellationSource.Token
                );

                if (success)
                {
                    ConvertImageCard.IsComplete = true;
                    ConversionStatus = $"Successfully converted to {targetFormatName}!";

                    await DetectImageFormatAsync();

                    var successDialog = CustomDialog.CreateInformationDialog(
                        "Conversion Successful",
                        $"Image successfully converted to {targetFormatName} format!\n\n" +
                        $"You can now proceed to the next steps.",
                        "",
                        DialogType.Success,
                        "CheckCircle"
                    );
                    successDialog.ShowDialog();
                }
                else
                {
                    ConvertImageCard.HasFailed = true;
                    ConversionStatus = "Conversion failed";

                    var errorDialog = CustomDialog.CreateInformationDialog(
                        "Conversion Failed",
                        $"Failed to convert image to {targetFormatName} format. Please check the logs for details.",
                        "",
                        DialogType.Error,
                        "CloseCircle"
                    );
                    errorDialog.ShowDialog();
                }
            }
            catch (OperationCanceledException)
            {
                ConversionStatus = "Conversion cancelled";
                ConvertImageCard.IsComplete = false;

            }
            catch (InsufficientDiskSpaceException spaceEx)
            {
                ConvertImageCard.HasFailed = true;
                ConversionStatus = $"Insufficient disk space on {spaceEx.DriveName}";

                _logService.LogError($"Insufficient disk space for image conversion: {spaceEx.Message}", spaceEx);

                var dialog = CustomDialog.CreateInformationDialog(
                    "Insufficient Disk Space",
                    $"There is not enough space on drive {spaceEx.DriveName} to convert the image.\n\n" +
                    $"Required: {spaceEx.RequiredGB:F2} GB\n" +
                    $"Available: {spaceEx.AvailableGB:F2} GB\n" +
                    $"Needed: {(spaceEx.RequiredGB - spaceEx.AvailableGB):F2} GB more\n\n" +
                    $"Please free up space or select a different location.",
                    "",
                    DialogType.Warning,
                    "Alert"
                );
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error during conversion: {ex.Message}", ex);
                ConvertImageCard.HasFailed = true;
                ConversionStatus = $"Error: {ex.Message}";

                var errorDialog = CustomDialog.CreateInformationDialog(
                    "Conversion Error",
                    $"An error occurred during conversion: {ex.Message}",
                    "",
                    DialogType.Error,
                    "CloseCircle"
                );
                errorDialog.ShowDialog();
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

    }
}
