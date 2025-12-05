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
        private readonly ILocalizationService _localizationService;
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
        public override string DisplayName => _localizationService?.GetString("WIMUtil_Title") ?? "Windows Installation Media Utility";
        public string FeatureId => "WimUtil";
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
            IThemeManager themeManager,
            ILocalizationService localizationService)
        {
            _wimUtilService = wimUtilService;
            _taskProgressService = taskProgressService;
            _dialogService = dialogService;
            _logService = logService;
            _serviceProvider = serviceProvider;
            _themeManager = themeManager;

            _localizationService = localizationService;
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

            _localizationService.LanguageChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(Title));
                RefreshLocalization();
            };

            WorkingDirectory = Path.Combine(Path.GetTempPath(), "WinhanceWIM");

            InitializeStepStates();
            CreateActionCards();
        }

        private void CreateActionCards()
        {
            SelectIsoCard = new WizardActionCard
            {
                Icon = "DiscPlayer",
                Title = _localizationService.GetString("WIMUtil_Card_SelectISO_Title"),
                Description = _localizationService.GetString("WIMUtil_Label_NoSelection"),
                ButtonText = _localizationService.GetString("WIMUtil_Card_SelectISO_Button"),
                ButtonCommand = SelectIsoFileCommand,
                IsEnabled = true
            };

            SelectDirectoryCard = new WizardActionCard
            {
                Icon = "FolderOpen",
                Title = _localizationService.GetString("WIMUtil_Card_SelectDirectory_Title"),
                Description = $"{_localizationService.GetString("WIMUtil_Label_DefaultPath")}: {Path.Combine(Path.GetTempPath(), "WinhanceWIM")}",
                ButtonText = _localizationService.GetString("WIMUtil_Card_SelectDirectory_Button"),
                ButtonCommand = SelectWorkingDirectoryCommand,
                IsEnabled = true
            };

            GenerateWinhanceXmlCard = new WizardActionCard
            {
                Icon = "Creation",
                Title = _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Title"),
                Description = _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Description"),
                ButtonText = _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Button"),
                ButtonCommand = GenerateWinhanceXmlCommand,
                IsEnabled = true
            };

            DownloadXmlCard = new WizardActionCard
            {
                Icon = "FileDownload",
                Title = _localizationService.GetString("WIMUtil_Card_DownloadXML_Title"),
                Description = _localizationService.GetString("WIMUtil_Card_DownloadXML_Description"),
                ButtonText = _localizationService.GetString("WIMUtil_Card_DownloadXML_Button"),
                ButtonCommand = DownloadUnattendedWinstallXmlCommand,
                IsEnabled = true
            };

            SelectXmlCard = new WizardActionCard
            {
                Icon = "FileCode",
                Title = _localizationService.GetString("WIMUtil_Card_SelectXML_Title"),
                Description = _localizationService.GetString("WIMUtil_Card_SelectXML_Description"),
                ButtonText = _localizationService.GetString("WIMUtil_Card_SelectXML_Button"),
                ButtonCommand = SelectXmlFileCommand,
                IsEnabled = true
            };

            ExtractSystemDriversCard = new WizardActionCard
            {
                Icon = "MemoryArrowDown",
                Title = _localizationService.GetString("WIMUtil_Card_ExtractDrivers_Title"),
                Description = _localizationService.GetString("WIMUtil_Card_ExtractDrivers_Description"),
                ButtonText = _localizationService.GetString("WIMUtil_Card_ExtractDrivers_Button"),
                ButtonCommand = ExtractAndAddSystemDriversCommand,
                IsEnabled = true
            };

            SelectCustomDriversCard = new WizardActionCard
            {
                Icon = "FolderOpen",
                Title = _localizationService.GetString("WIMUtil_Card_CustomDrivers_Title"),
                Description = _localizationService.GetString("WIMUtil_Card_CustomDrivers_Description"),
                ButtonText = _localizationService.GetString("WIMUtil_Card_CustomDrivers_Button"),
                ButtonCommand = SelectAndAddCustomDriversCommand,
                IsEnabled = true
            };

            DownloadOscdimgCard = new WizardActionCard
            {
                Icon = "DiscAlert",
                Title = _localizationService.GetString("WIMUtil_Card_DownloadOscdimg_Title"),
                Description = _localizationService.GetString("WIMUtil_Card_DownloadOscdimg_Description"),
                ButtonText = _localizationService.GetString("WIMUtil_Card_DownloadOscdimg_Button"),
                ButtonCommand = DownloadOscdimgCommand,
                IsEnabled = true
            };

            SelectOutputCard = new WizardActionCard
            {
                Icon = "ContentSaveOutline",
                Title = _localizationService.GetString("WIMUtil_Card_SelectOutput_Title"),
                Description = _localizationService.GetString("WIMUtil_Label_NoLocation"),
                ButtonText = _localizationService.GetString("WIMUtil_Card_SelectOutput_Button"),
                ButtonCommand = SelectIsoOutputLocationCommand,
                IsEnabled = true
            };

            ConvertImageCard = new WizardActionCard
            {
                Icon = "SwapHorizontal",
                Title = _localizationService.GetString("WIMUtil_Card_ConvertImage_Title"),
                Description = _localizationService.GetString("WIMUtil_Label_Detecting"),
                ButtonText = _localizationService.GetString("WIMUtil_Card_ConvertImage_Button"),
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

        private void RefreshLocalization()
        {
            // 1. Recreate Step objects (for new Titles) and restore state
            InitializeStepStates();
            UpdateStepStates();

            // 2. Update Titles and Buttons for all cards (Static loc)
            SelectIsoCard.Title = _localizationService.GetString("WIMUtil_Card_SelectISO_Title");
            SelectIsoCard.ButtonText = _localizationService.GetString("WIMUtil_Card_SelectISO_Button");

            SelectDirectoryCard.Title = _localizationService.GetString("WIMUtil_Card_SelectDirectory_Title");
            SelectDirectoryCard.ButtonText = _localizationService.GetString("WIMUtil_Card_SelectDirectory_Button");

            GenerateWinhanceXmlCard.Title = _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Title");
            GenerateWinhanceXmlCard.ButtonText = _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Button");

            DownloadXmlCard.Title = _localizationService.GetString("WIMUtil_Card_DownloadXML_Title");
            DownloadXmlCard.ButtonText = _localizationService.GetString("WIMUtil_Card_DownloadXML_Button");

            SelectXmlCard.Title = _localizationService.GetString("WIMUtil_Card_SelectXML_Title");
            SelectXmlCard.ButtonText = _localizationService.GetString("WIMUtil_Card_SelectXML_Button");

            ExtractSystemDriversCard.Title = _localizationService.GetString("WIMUtil_Card_ExtractDrivers_Title");
            ExtractSystemDriversCard.ButtonText = _localizationService.GetString("WIMUtil_Card_ExtractDrivers_Button");

            SelectCustomDriversCard.Title = _localizationService.GetString("WIMUtil_Card_CustomDrivers_Title");
            SelectCustomDriversCard.ButtonText = _localizationService.GetString("WIMUtil_Card_CustomDrivers_Button");

            DownloadOscdimgCard.Title = _localizationService.GetString("WIMUtil_Card_DownloadOscdimg_Title");
            // Button text for Oscdimg is handled in UpdateDownloadOscdimgCardState

            SelectOutputCard.Title = _localizationService.GetString("WIMUtil_Card_SelectOutput_Title");
            SelectOutputCard.ButtonText = _localizationService.GetString("WIMUtil_Card_SelectOutput_Button");

            ConvertImageCard.Title = _localizationService.GetString("WIMUtil_Card_ConvertImage_Title");
            // Button text for ConvertImage is handled in UpdateConversionCardState

            // 3. Update Descriptions (Dynamic loc) based on current state

            // SelectIsoCard
            if (IsExtractionComplete)
            {
                SelectIsoCard.Description = _localizationService.GetString("WIMUtil_Status_IsoExtractionSuccess");
            }
            else if (string.IsNullOrEmpty(SelectedIsoPath))
            {
                SelectIsoCard.Description = _localizationService.GetString("WIMUtil_Label_NoSelection");
            }
            // Else: It shows the file path, which doesn't need localization

            // SelectDirectoryCard
            var defaultPath = Path.Combine(Path.GetTempPath(), "WinhanceWIM");
            if (HasExtractedIsoAlready && string.IsNullOrEmpty(WorkingDirectory))
            {
                SelectDirectoryCard.Description = _localizationService.GetString("WIMUtil_Label_SelectExtracted");
            }
            else if (WorkingDirectory == defaultPath || string.IsNullOrEmpty(WorkingDirectory))
            {
                SelectDirectoryCard.Description = $"{_localizationService.GetString("WIMUtil_Label_DefaultPath")}: {defaultPath}";
            }
            else
            {
                SelectDirectoryCard.Description = $"{_localizationService.GetString("WIMUtil_Label_Using")}: {WorkingDirectory}";
            }

            // GenerateWinhanceXmlCard
            if (GenerateWinhanceXmlCard.IsComplete)
                GenerateWinhanceXmlCard.Description = _localizationService.GetString("WIMUtil_Status_XmlGenSuccess");
            else if (GenerateWinhanceXmlCard.HasFailed)
                GenerateWinhanceXmlCard.Description = XmlStatus.StartsWith("Generation failed") ? _localizationService.GetString("WIMUtil_Status_XmlGenFailed", XmlStatus.Replace("Generation failed: ", "")) : XmlStatus;
            else
                GenerateWinhanceXmlCard.Description = _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Description");

            // DownloadXmlCard
            if (DownloadXmlCard.IsComplete)
                DownloadXmlCard.Description = _localizationService.GetString("WIMUtil_Status_XmlDownloadSuccess");
            else if (DownloadXmlCard.HasFailed)
                DownloadXmlCard.Description = XmlStatus == "Downloaded but failed to add to media" ? _localizationService.GetString("WIMUtil_Status_XmlAddFailed") : XmlStatus;
            else
                DownloadXmlCard.Description = _localizationService.GetString("WIMUtil_Card_DownloadXML_Description");

            // SelectXmlCard
            if (SelectXmlCard.IsComplete)
                SelectXmlCard.Description = _localizationService.GetString("WIMUtil_Status_XmlSelectSuccess");
            else if (SelectXmlCard.HasFailed)
                SelectXmlCard.Description = XmlStatus == "Valid XML but failed to add to media" ? _localizationService.GetString("WIMUtil_Status_XmlValidAddFailed") : 
                                            (XmlStatus == "Invalid XML file" ? _localizationService.GetString("WIMUtil_Status_XmlInvalid") : XmlStatus);
            else
                SelectXmlCard.Description = _localizationService.GetString("WIMUtil_Card_SelectXML_Description");

            // ExtractSystemDriversCard
            if (ExtractSystemDriversCard.IsComplete)
                ExtractSystemDriversCard.Description = _localizationService.GetString("WIMUtil_Status_DriversAdded");
            else if (ExtractSystemDriversCard.HasFailed)
                ExtractSystemDriversCard.Description = _localizationService.GetString("WIMUtil_Status_ErrorPrefix", "Extraction failed"); // simplified
            else
                ExtractSystemDriversCard.Description = _localizationService.GetString("WIMUtil_Card_ExtractDrivers_Description");

            // SelectCustomDriversCard
            if (SelectCustomDriversCard.IsComplete)
                SelectCustomDriversCard.Description = _localizationService.GetString("WIMUtil_Status_DriversAdded"); // Reuse success message
            else if (SelectCustomDriversCard.HasFailed)
                SelectCustomDriversCard.Description = _localizationService.GetString("WIMUtil_Status_NoDriversAdded"); // Simplified failure
            else
                SelectCustomDriversCard.Description = _localizationService.GetString("WIMUtil_Card_CustomDrivers_Description");

            // DownloadOscdimgCard
            UpdateDownloadOscdimgCardState(); // Handles full refresh of this card

            // SelectOutputCard
            if (IsIsoCreated)
                SelectOutputCard.Description = _localizationService.GetString("WIMUtil_Desc_IsoCreatedSuccess");
            else if (!string.IsNullOrEmpty(OutputIsoPath))
                SelectOutputCard.Description = $"{_localizationService.GetString("WIMUtil_Label_Output")}: {Path.GetFileName(OutputIsoPath)}";
            else
                SelectOutputCard.Description = _localizationService.GetString("WIMUtil_Label_NoLocation");

            // ConvertImageCard
            UpdateConversionCardState(); // Handles full refresh of this card

            // Notify UI
            NotifyCardPropertiesChanged();
        }

        private void NotifyCardPropertiesChanged()
        {
            OnPropertyChanged(nameof(SelectIsoCard));
            OnPropertyChanged(nameof(SelectDirectoryCard));
            OnPropertyChanged(nameof(GenerateWinhanceXmlCard));
            OnPropertyChanged(nameof(DownloadXmlCard));
            OnPropertyChanged(nameof(SelectXmlCard));
            OnPropertyChanged(nameof(ExtractSystemDriversCard));
            OnPropertyChanged(nameof(SelectCustomDriversCard));
            OnPropertyChanged(nameof(DownloadOscdimgCard));
            OnPropertyChanged(nameof(SelectOutputCard));
            OnPropertyChanged(nameof(ConvertImageCard));
        }

        private void InitializeStepStates()
        {
            Step1State = new WizardStepState
            {
                StepNumber = 1,
                Title = _localizationService.GetString("WIMUtil_Step1_Title"),
                Icon = "DiscPlayer",
                StatusText = _localizationService.GetString("WIMUtil_Status_NoIsoSelected"),
                IsExpanded = true,
                IsAvailable = true,
                IsComplete = false
            };

            Step2State = new WizardStepState
            {
                StepNumber = 2,
                Title = _localizationService.GetString("WIMUtil_Step2_Title"),
                Icon = "FileCode",
                StatusText = _localizationService.GetString("WIMUtil_Status_CompleteStep1"),
                IsExpanded = false,
                IsAvailable = false,
                IsComplete = false
            };

            Step3State = new WizardStepState
            {
                StepNumber = 3,
                Title = _localizationService.GetString("WIMUtil_Step3_Title"),
                Icon = "Chip",
                StatusText = _localizationService.GetString("WIMUtil_Status_CompleteStep1"),
                IsExpanded = false,
                IsAvailable = false,
                IsComplete = false
            };

            Step4State = new WizardStepState
            {
                StepNumber = 4,
                Title = _localizationService.GetString("WIMUtil_Step4_Title"),
                Icon = "WrenchClock",
                StatusText = _localizationService.GetString("WIMUtil_Status_CompleteStep1"),
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
                Title = _localizationService.GetString("WIMUtil_FileDialog_SelectIso")
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
                SelectDirectoryCard.Description = _localizationService.GetString("WIMUtil_Label_SelectExtracted");
            }
            else
            {
                SelectDirectoryCard.Description = $"{_localizationService.GetString("WIMUtil_Label_DefaultPath")}: {Path.Combine(Path.GetTempPath(), "WinhanceWIM")}";
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
            var description = HasExtractedIsoAlready
                ? _localizationService.GetString("WIMUtil_FolderDialog_SelectExtracted")
                : _localizationService.GetString("WIMUtil_FolderDialog_SelectWorkDir");

            var selectedPath = ShowFolderBrowserDialog(description);

            if (string.IsNullOrEmpty(selectedPath))
                return;

            if (HasExtractedIsoAlready)
            {
                var isValid = await ValidateExtractedIsoDirectory(selectedPath);

                if (isValid)
                {
                    WorkingDirectory = selectedPath;
                    SelectDirectoryCard.Description = $"{_localizationService.GetString("WIMUtil_Label_Using")}: {WorkingDirectory}";
                    IsExtractionComplete = true;
                    UpdateStepStates();

                    _dialogService.ShowLocalizedDialog(
                        "Dialog_ValidationComplete",
                        "WIMUtil_Msg_ValidationComplete",
                        DialogType.Success,
                        "CheckCircle"
                    );
                }
                else
                {
                    WorkingDirectory = string.Empty;
                    SelectDirectoryCard.Description = _localizationService.GetString("WIMUtil_Error_InvalidDirectory");

                    _dialogService.ShowLocalizedDialog(
                        "Dialog_InvalidDirectory",
                        "WIMUtil_Msg_InvalidDirectory",
                        DialogType.Error,
                        "CloseCircle"
                    );
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
                    _logService.LogInformation($"Working directory set to: {WorkingDirectory}");
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
                    SelectIsoCard.Description = _localizationService.GetString("WIMUtil_Status_IsoExtractionSuccess");
                    SelectIsoCard.DescriptionForeground = new SolidColorBrush(Color.FromRgb(27, 94, 32));
                    IsExtracting = false;
                    IsExtractionComplete = true;
                    UpdateStepStates();

                    _dialogService.ShowLocalizedDialog(
                        "Dialog_ExtractionComplete",
                        "WIMUtil_Msg_ExtractionComplete",
                        DialogType.Success,
                        "CheckCircle"
                    );
                }
                else
                {
                    SelectIsoCard.HasFailed = true;
                    SelectIsoCard.IsEnabled = true;
                    SelectIsoCard.Opacity = 1.0;
                    SelectDirectoryCard.IsEnabled = true;
                    SelectDirectoryCard.Opacity = 1.0;
                    SelectIsoCard.Description = _localizationService.GetString("WIMUtil_Status_IsoExtractionFailed");
                    SelectIsoCard.DescriptionForeground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
                    IsExtracting = false;
                    UpdateStepStates();

                    _dialogService.ShowLocalizedDialog(
                        "Dialog_ExtractionFailed",
                        "WIMUtil_Msg_ExtractionFailed",
                        DialogType.Error,
                        "CloseCircle"
                    );
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

                SelectIsoCard.Description = _localizationService.GetString("WIMUtil_Status_IsoExtractionCancelled");
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

                SelectIsoCard.Description = _localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace", spaceEx.DriveName);
                SelectIsoCard.DescriptionForeground = new SolidColorBrush(Color.FromRgb(198, 40, 40));

                _logService.LogError($"Insufficient disk space for ISO extraction: {spaceEx.Message}", spaceEx);

                _dialogService.ShowLocalizedDialog(
                    "Dialog_InsufficientSpace",
                    "WIMUtil_Msg_InsufficientSpace",
                    DialogType.Warning,
                    "Alert",
                    spaceEx.DriveName,
                    spaceEx.RequiredGB.ToString("F2"),
                    spaceEx.AvailableGB.ToString("F2"),
                    (spaceEx.RequiredGB - spaceEx.AvailableGB).ToString("F2")
                );
            }
            catch (Exception ex)
            {
                SelectIsoCard.HasFailed = true;
                SelectIsoCard.IsEnabled = true;
                SelectIsoCard.Opacity = 1.0;
                SelectDirectoryCard.IsEnabled = true;
                SelectDirectoryCard.Opacity = 1.0;
                SelectIsoCard.Description = _localizationService.GetString("WIMUtil_Status_ErrorPrefix", ex.Message);
                SelectIsoCard.DescriptionForeground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
                IsExtracting = false;
                UpdateStepStates();
                _logService.LogError($"Error extracting ISO: {ex.Message}", ex);

                _dialogService.ShowLocalizedDialog(
                    "Dialog_ExtractionError",
                    "WIMUtil_Msg_ExtractionError",
                    DialogType.Error,
                    "CloseCircle",
                    ex.Message
                );
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

                var confirmed = _dialogService.ShowLocalizedConfirmationDialog(
                    "Dialog_GenerateXml",
                    "Msg_GenerateXmlConfirm",
                    DialogType.Information,
                    "Information"
                );

                if (!confirmed)
                    return;

                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlGenerating");

                var xmlGeneratorService = _serviceProvider.GetRequiredService<IAutounattendXmlGeneratorService>();
                var outputPath = Path.Combine(WorkingDirectory, "autounattend.xml");

                var generatedPath = await xmlGeneratorService.GenerateFromCurrentSelectionsAsync(outputPath);

                SelectedXmlPath = generatedPath;
                IsXmlAdded = true;
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlGenSuccess");
                ClearOtherXmlCardCompletions("generate");
                GenerateWinhanceXmlCard.IsComplete = true;
                UpdateStepStates();

                _logService.LogInformation($"Winhance XML generated: {generatedPath}");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error generating Winhance XML: {ex.Message}", ex);
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlGenFailed", ex.Message);
                GenerateWinhanceXmlCard.HasFailed = true;

                _dialogService.ShowLocalizedDialog(
                    "Dialog_XmlGenError",
                    "WIMUtil_Msg_XmlGenError",
                    DialogType.Error,
                    "CloseCircle",
                    ex.Message
                );
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
                    XmlStatus = detail.StatusText ?? _localizationService.GetString("WIMUtil_Status_XmlDownloading");
                });

                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlDownloadStart");
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
                    XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlDownloadSuccess");
                    ClearOtherXmlCardCompletions("download");
                    DownloadXmlCard.IsComplete = true;
                    UpdateStepStates();

                    _logService.LogInformation($"UnattendedWinstall XML downloaded and added: {destinationPath}");
                }
                else
                {
                    DownloadXmlCard.HasFailed = true;
                    XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlAddFailed");

                    _dialogService.ShowLocalizedDialog(
                        "Dialog_XmlAddFailed",
                        "WIMUtil_Msg_XmlAddFailed",
                        DialogType.Error,
                        "CloseCircle"
                    );
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error downloading XML: {ex.Message}", ex);
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlDownloadFailed", ex.Message);
                DownloadXmlCard.HasFailed = true;

                _dialogService.ShowLocalizedDialog(
                    "Dialog_XmlDownloadError",
                    "WIMUtil_Msg_XmlDownloadError",
                    DialogType.Error,
                    "CloseCircle",
                    ex.Message
                );
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
                    Title = _localizationService.GetString("WIMUtil_FileDialog_SelectXml")
                };

                if (dialog.ShowDialog() != true)
                    return;

                var selectedPath = dialog.FileName;

                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlValidating");
                var isValidXml = await ValidateXmlFile(selectedPath);

                if (!isValidXml)
                {
                    SelectXmlCard.HasFailed = true;
                    XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlInvalid");

                    _dialogService.ShowLocalizedDialog(
                        "Dialog_XmlInvalid",
                        "WIMUtil_Msg_XmlInvalidError",
                        DialogType.Error,
                        "CloseCircle"
                    );
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

                    _logService.LogInformation($"Custom XML validated and added: {selectedPath}");
                }
                else
                {
                    SelectXmlCard.HasFailed = true;
                    XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlValidAddFailed");

                    _dialogService.ShowLocalizedDialog(
                        "Dialog_XmlAddFailed",
                        "WIMUtil_Msg_XmlValidAddFailed",
                        DialogType.Error,
                        "CloseCircle"
                    );
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error selecting XML: {ex.Message}", ex);
                XmlStatus = _localizationService.GetString("WIMUtil_Status_ErrorPrefix", ex.Message);
                SelectXmlCard.HasFailed = true;

                _dialogService.ShowLocalizedDialog(
                    "Dialog_XmlSelectError",
                    "WIMUtil_Msg_XmlSelectError",
                    DialogType.Error,
                    "CloseCircle",
                    ex.Message
                );
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

                if (!_dialogService.ShowLocalizedConfirmationDialog(
                    "Dialog_ExtractDrivers",
                    "WIMUtil_Msg_ExtractDriversConfirm",
                    DialogType.Information,
                    "Information"))
                {
                    return;
                }

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

                    _dialogService.ShowLocalizedDialog(
                        "Dialog_DriversSuccess",
                        "WIMUtil_Msg_DriversSuccess",
                        DialogType.Success,
                        "CheckCircle"
                    );
                }
                else
                {
                    ExtractSystemDriversCard.HasFailed = true;

                    _dialogService.ShowLocalizedDialog(
                        "Dialog_NoDrivers",
                        "WIMUtil_Msg_NoDriversFound",
                        DialogType.Warning,
                        "Alert"
                    );
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error extracting system drivers: {ex.Message}", ex);
                ExtractSystemDriversCard.IsProcessing = false;
                ExtractSystemDriversCard.IsEnabled = true;
                ExtractSystemDriversCard.HasFailed = true;

                _dialogService.ShowLocalizedDialog(
                    "Dialog_DriverError",
                    "WIMUtil_Msg_DriverExtractionError",
                    DialogType.Error,
                    "CloseCircle",
                    ex.Message
                );
            }
        }

        [RelayCommand]
        private async Task SelectAndAddCustomDrivers()
        {
            try
            {
                SelectCustomDriversCard.IsComplete = false;
                SelectCustomDriversCard.HasFailed = false;

                var selectedPath = ShowFolderBrowserDialog(_localizationService.GetString("WIMUtil_FolderDialog_SelectDrivers"));

                if (string.IsNullOrEmpty(selectedPath))
                {
                    _logService.LogInformation("User cancelled driver folder selection");
                    return;
                }

                if (!Directory.Exists(selectedPath))
                {
                    SelectCustomDriversCard.HasFailed = true;

                    _dialogService.ShowLocalizedDialog(
                        "Dialog_InvalidFolder",
                        "WIMUtil_Msg_InvalidFolder",
                        DialogType.Error,
                        "CloseCircle"
                    );
                    return;
                }

                var hasFiles = Directory.EnumerateFileSystemEntries(selectedPath, "*", SearchOption.AllDirectories).Any();
                if (!hasFiles)
                {
                    SelectCustomDriversCard.HasFailed = true;

                    _dialogService.ShowLocalizedDialog(
                        "Dialog_EmptyFolder",
                        "WIMUtil_Msg_EmptyFolder",
                        DialogType.Warning,
                        "Alert"
                    );
                    return;
                }

                SelectCustomDriversCard.IsProcessing = true;
                SelectCustomDriversCard.IsEnabled = false;

                _cancellationTokenSource = new CancellationTokenSource();
                var progress = new Progress<TaskProgressDetail>(detail => { });

                SelectCustomDriversCard.Description = $"{_localizationService.GetString("WIMUtil_Label_Selected")}: {selectedPath}";

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

                    _dialogService.ShowLocalizedDialog(
                        "Dialog_DriversAdded",
                        "WIMUtil_Msg_DriverFilesAdded",
                        DialogType.Success,
                        "CheckCircle"
                    );
                }
                else
                {
                    SelectCustomDriversCard.HasFailed = true;

                    _dialogService.ShowLocalizedDialog(
                        "Dialog_NoDrivers",
                        "WIMUtil_Msg_NoCustomDrivers",
                        DialogType.Warning,
                        "Alert",
                        selectedPath
                    );
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error adding custom drivers: {ex.Message}", ex);
                SelectCustomDriversCard.IsProcessing = false;
                SelectCustomDriversCard.IsEnabled = true;
                SelectCustomDriversCard.HasFailed = true;

                _dialogService.ShowLocalizedDialog(
                    "Dialog_DriverAddError",
                    "WIMUtil_Msg_DriverAdditionError",
                    DialogType.Error,
                    "CloseCircle",
                    ex.Message
                );
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
                    DownloadOscdimgCard.ButtonText = _localizationService.GetString("WIMUtil_Button_OscdimgFound");
                    DownloadOscdimgCard.Description = _localizationService.GetString("WIMUtil_Desc_OscdimgInstalled");
                    DownloadOscdimgCard.Icon = "CheckCircle";

                    UpdateStepStates();

                    _dialogService.ShowLocalizedDialog(
                        "Dialog_AdkComplete",
                        "WIMUtil_Msg_AdkInstallComplete",
                        DialogType.Success,
                        "CheckCircle"
                    );
                }
                else
                {
                    DownloadOscdimgCard.IsEnabled = true;
                    DownloadOscdimgCard.HasFailed = true;

                    _dialogService.ShowLocalizedDialog(
                        "Dialog_AdkFailed",
                        "WIMUtil_Msg_AdkInstallFailed",
                        DialogType.Error,
                        "CloseCircle"
                    );
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error installing ADK: {ex.Message}", ex);

                DownloadOscdimgCard.IsProcessing = false;
                DownloadOscdimgCard.IsEnabled = true;
                DownloadOscdimgCard.HasFailed = true;

                _dialogService.ShowLocalizedDialog(
                    "Dialog_AdkError",
                    "WIMUtil_Msg_AdkInstallError",
                    DialogType.Error,
                    "CloseCircle",
                    ex.Message
                );
            }
        }

        [RelayCommand]
        private void SelectIsoOutputLocation()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "ISO Files (*.iso)|*.iso",
                Title = _localizationService.GetString("WIMUtil_FileDialog_SelectOutput"),
                FileName = "Winhance_Windows.iso"
            };

            if (dialog.ShowDialog() == true)
            {
                OutputIsoPath = dialog.FileName;
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
                    _dialogService.ShowLocalizedDialog(
                        "Dialog_OscdimgRequired",
                        "WIMUtil_Msg_OscdimgRequired",
                        DialogType.Warning,
                        "Alert"
                    );
                    return;
                }

                if (string.IsNullOrEmpty(OutputIsoPath))
                {
                    _dialogService.ShowLocalizedDialog(
                        "Dialog_OutputRequired",
                        "WIMUtil_Msg_OutputRequired",
                        DialogType.Warning,
                        "Alert"
                    );
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
                    SelectOutputCard.Description = _localizationService.GetString("WIMUtil_Desc_IsoCreatedSuccess");
                    SelectOutputCard.DescriptionForeground = new SolidColorBrush(Color.FromRgb(27, 94, 32));
                    UpdateStepStates();

                    if (_dialogService.ShowLocalizedConfirmationDialog(
                        "Dialog_IsoCreated",
                        "WIMUtil_Msg_IsoCreatedSuccess",
                        DialogType.Success,
                        "CheckCircle",
                        OutputIsoPath))
                    {
                        Process.Start("explorer.exe", $"/select,\"{OutputIsoPath}\"");
                    }
                }
                else
                {
                    SelectOutputCard.IsEnabled = true;
                    SelectOutputCard.Opacity = 1.0;
                    SelectOutputCard.Description = _localizationService.GetString("WIMUtil_Desc_IsoCreateFailed");
                    SelectOutputCard.DescriptionForeground = new SolidColorBrush(Color.FromRgb(198, 40, 40));

                    _dialogService.ShowLocalizedDialog(
                        "Dialog_IsoCreationFailed",
                        "WIMUtil_Msg_IsoCreationFailed",
                        DialogType.Error,
                        "CloseCircle"
                    );
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

                SelectOutputCard.Description = _localizationService.GetString("WIMUtil_Desc_IsoCreateCancelled");
                SelectOutputCard.DescriptionForeground = new SolidColorBrush(Color.FromRgb(255, 152, 0));

            }
            catch (InsufficientDiskSpaceException spaceEx)
            {
                SelectOutputCard.IsEnabled = true;
                SelectOutputCard.Opacity = 1.0;

                SelectOutputCard.Description = _localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace", spaceEx.DriveName);
                SelectOutputCard.DescriptionForeground = new SolidColorBrush(Color.FromRgb(198, 40, 40));

                _logService.LogError($"Insufficient disk space for ISO creation: {spaceEx.Message}", spaceEx);

                _dialogService.ShowLocalizedDialog(
                    "Dialog_InsufficientSpace",
                    "WIMUtil_Msg_InsufficientSpace_Create",
                    DialogType.Warning,
                    "Alert",
                    spaceEx.DriveName,
                    spaceEx.RequiredGB.ToString("F2"),
                    spaceEx.AvailableGB.ToString("F2"),
                    (spaceEx.RequiredGB - spaceEx.AvailableGB).ToString("F2")
                );
            }
            catch (Exception ex)
            {
                SelectOutputCard.IsEnabled = true;
                SelectOutputCard.Opacity = 1.0;
                _logService.LogError($"Error creating ISO: {ex.Message}", ex);
                SelectOutputCard.Description = _localizationService.GetString("WIMUtil_Status_ErrorPrefix", ex.Message);
                SelectOutputCard.DescriptionForeground = new SolidColorBrush(Color.FromRgb(198, 40, 40));

                _dialogService.ShowLocalizedDialog(
                    "Dialog_IsoCreationError",
                    "WIMUtil_Msg_IsoCreationError",
                    DialogType.Error,
                    "CloseCircle",
                    ex.Message
                );
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
                _dialogService.ShowLocalizedDialog(
                    "Dialog_StepNotAvailable",
                    "WIMUtil_Msg_StepNotAvailable",
                    DialogType.Information,
                    "Information"
                );
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
                DownloadOscdimgCard.ButtonText = _localizationService.GetString("WIMUtil_Button_OscdimgFound");
                DownloadOscdimgCard.Description = _localizationService.GetString("WIMUtil_Desc_OscdimgFound");
                DownloadOscdimgCard.Icon = "CheckCircle";
            }
            else
            {
                DownloadOscdimgCard.IsEnabled = true;
                DownloadOscdimgCard.IsComplete = false;
                DownloadOscdimgCard.ButtonText = _localizationService.GetString("WIMUtil_Button_Download");
                DownloadOscdimgCard.Description = _localizationService.GetString("WIMUtil_Card_DownloadOscdimg_Description");
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
            if (IsConverting) return _localizationService.GetString("WIMUtil_Status_Converting");
            if (IsExtractionComplete) return _localizationService.GetString("WIMUtil_Status_IsoExtracted");
            if (IsExtracting) return _localizationService.GetString("WIMUtil_Status_Extracting");
            if (!string.IsNullOrEmpty(SelectedIsoPath)) return _localizationService.GetString("WIMUtil_Status_IsoSelected");
            return _localizationService.GetString("WIMUtil_Status_NoIsoSelected");
        }

        private string GetStep2StatusText()
        {
            if (IsConverting) return _localizationService.GetString("WIMUtil_Status_WaitForConversion");
            if (!IsExtractionComplete) return _localizationService.GetString("WIMUtil_Status_CompleteStep1");
            if (IsXmlAdded) return _localizationService.GetString("WIMUtil_Status_XmlAdded");
            if (!string.IsNullOrEmpty(SelectedXmlPath)) return $"{_localizationService.GetString("WIMUtil_Label_Selected")}: {Path.GetFileName(SelectedXmlPath)}";
            return _localizationService.GetString("WIMUtil_Status_NoXmlAdded");
        }

        private string GetStep3StatusText()
        {
            if (IsConverting) return _localizationService.GetString("WIMUtil_Status_WaitForConversion");
            if (!IsExtractionComplete) return _localizationService.GetString("WIMUtil_Status_CompleteStep1");
            if (AreDriversAdded) return _localizationService.GetString("WIMUtil_Status_DriversAdded");
            return _localizationService.GetString("WIMUtil_Status_NoDriversAdded");
        }

        private string GetStep4StatusText()
        {
            if (IsConverting) return _localizationService.GetString("WIMUtil_Status_WaitForConversion");
            if (!IsExtractionComplete) return _localizationService.GetString("WIMUtil_Status_CompleteStep1");
            if (IsIsoCreated) return _localizationService.GetString("WIMUtil_Status_IsoCreated");
            if (!string.IsNullOrEmpty(OutputIsoPath)) return $"{_localizationService.GetString("WIMUtil_Label_Output")}: {Path.GetFileName(OutputIsoPath)}";
            return _localizationService.GetString("WIMUtil_Status_ReadyToCreateIso");
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
                ConvertImageCard.Description = _localizationService.GetString("WIMUtil_Label_NoImageDetected");
                return;
            }

            var currentFormat = CurrentImageFormat.Format == ImageFormat.Wim ? "WIM" : "ESD";
            var targetFormat = CurrentImageFormat.Format == ImageFormat.Wim ? "ESD" : "WIM";
            var currentSize = CurrentImageFormat.FileSizeBytes / (1024.0 * 1024 * 1024);

            var estimatedTargetSize = CurrentImageFormat.Format == ImageFormat.Wim
                ? currentSize * 0.65
                : currentSize * 1.50;

            var diff = Math.Abs(estimatedTargetSize - currentSize);
            var sizeChange = CurrentImageFormat.Format == ImageFormat.Wim
                ? $"{_localizationService.GetString("WIMUtil_Label_Save")} ~{diff:F2} GB"
                : _localizationService.GetString("WIMUtil_Label_RequiresMore", diff.ToString("F2"));

            ConvertImageCard.Icon = CurrentImageFormat.Format == ImageFormat.Wim
                ? "ArrowCollapseAll"
                : "ArrowExpandAll";

            ConvertImageCard.Title = _localizationService.GetString("WIMUtil_Card_ConvertImage_Title_Dynamic", currentFormat, targetFormat);

            var performanceNote = CurrentImageFormat.Format == ImageFormat.Wim
                ? _localizationService.GetString("WIMUtil_Label_PerfNote_Wim")
                : _localizationService.GetString("WIMUtil_Label_PerfNote_Esd");

            ConvertImageCard.Description =
                $"{_localizationService.GetString("WIMUtil_Label_Current")}: install.{currentFormat.ToLower()} ({currentSize:F2} GB)\n" +
                $"{_localizationService.GetString("WIMUtil_Label_AfterConversion")}: ~{estimatedTargetSize:F2} GB ({sizeChange})\n" +
                $"{performanceNote}";

            ConvertImageCard.ButtonText = _localizationService.GetString("WIMUtil_Card_ConvertImage_Button_Dynamic", targetFormat);
            ConvertImageCard.IsEnabled = !IsConverting;

            _logService.LogInformation(
                string.Format(_localizationService.GetString("WIMUtil_Label_FormatDetected"), currentFormat, CurrentImageFormat.ImageCount, currentSize.ToString("F2"))
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

                var dialogTitleKey = targetFormat == ImageFormat.Esd ? "Dialog_ConvertToEsd" : "Dialog_ConvertToWim";
                var messageKey = targetFormat == ImageFormat.Esd ? "WIMUtil_Msg_ConvertConfirm_Esd" : "WIMUtil_Msg_ConvertConfirm_Wim";
                var sizeDiff = targetFormat == ImageFormat.Esd
                    ? (CurrentImageFormat.FileSizeBytes * 0.35 / (1024.0 * 1024 * 1024))
                    : (CurrentImageFormat.FileSizeBytes * 0.50 / (1024.0 * 1024 * 1024));

                if (!_dialogService.ShowLocalizedConfirmationDialog(
                    dialogTitleKey,
                    messageKey,
                    DialogType.Information,
                    "Information",
                    sizeDiff.ToString("F2")))
                {
                    return;
                }

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
                    ConversionStatus = _localizationService.GetString("WIMUtil_Status_ConversionSuccess", targetFormatName);

                    await DetectImageFormatAsync();

                    _dialogService.ShowLocalizedDialog(
                        "Dialog_ConversionSuccess",
                        "WIMUtil_Msg_ConversionSuccess",
                        DialogType.Success,
                        "CheckCircle",
                        targetFormatName
                    );
                }
                else
                {
                    ConvertImageCard.HasFailed = true;
                    ConversionStatus = _localizationService.GetString("WIMUtil_Status_ConversionFailed");

                    _dialogService.ShowLocalizedDialog(
                        "Dialog_ConversionFailed",
                        "WIMUtil_Msg_ConversionFailed",
                        DialogType.Error,
                        "CloseCircle",
                        targetFormatName
                    );
                }
            }
            catch (OperationCanceledException)
            {
                ConversionStatus = _localizationService.GetString("WIMUtil_Status_ConversionCancelled");
                ConvertImageCard.IsComplete = false;

            }
            catch (InsufficientDiskSpaceException spaceEx)
            {
                ConvertImageCard.HasFailed = true;
                ConversionStatus = _localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace", spaceEx.DriveName);

                _logService.LogError($"Insufficient disk space for image conversion: {spaceEx.Message}", spaceEx);

                _dialogService.ShowLocalizedDialog(
                    "Dialog_InsufficientSpace",
                    "WIMUtil_Msg_InsufficientSpace_Convert",
                    DialogType.Warning,
                    "Alert",
                    spaceEx.DriveName,
                    spaceEx.RequiredGB.ToString("F2"),
                    spaceEx.AvailableGB.ToString("F2"),
                    (spaceEx.RequiredGB - spaceEx.AvailableGB).ToString("F2")
                );
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error during conversion: {ex.Message}", ex);
                ConvertImageCard.HasFailed = true;
                ConversionStatus = _localizationService.GetString("WIMUtil_Status_ErrorPrefix", ex.Message);

                _dialogService.ShowLocalizedDialog(
                    "Dialog_ConversionError",
                    "WIMUtil_Msg_ConversionError",
                    DialogType.Error,
                    "CloseCircle",
                    ex.Message
                );
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
