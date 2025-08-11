using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.WPF.Features.Common.Models;
namespace Winhance.WPF.Features.Optimize.ViewModels
{
    /// <summary>
    /// ViewModel for Power optimizations using clean architecture principles.
    /// Uses composition pattern with ISettingsUICoordinator.
    /// </summary>
    public partial class PowerOptimizationsViewModel : ObservableObject, IFeatureViewModel
    {
        private readonly IPowerService _powerService;
        private readonly IDialogService _dialogService;
        private readonly ISettingsUICoordinator _uiCoordinator;
        private readonly ITaskProgressService _progressService;
        private readonly ILogService _logService;

        /// <summary>
        /// Gets or sets a value indicating whether the system has a battery.
        /// </summary>
        [ObservableProperty]
        private bool _hasBattery;

        /// <summary>
        /// Gets or sets a value indicating whether the system has a lid.
        /// </summary>
        [ObservableProperty]
        private bool _hasLid;

        /// <summary>
        /// Gets or sets the power plan value.
        /// </summary>
        [ObservableProperty]
        private int _powerPlanValue;

        /// <summary>
        /// Gets or sets a value indicating whether a power plan is being applied.
        /// </summary>
        [ObservableProperty]
        private bool _isApplyingPowerPlan;

        /// <summary>
        /// Gets or sets the advanced power setting groups.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasAdvancedPowerSettings))]
        private ObservableCollection<AdvancedPowerSettingGroup> _advancedPowerSettingGroups = new();

        /// <summary>
        /// Gets a value indicating whether there are any advanced power settings available.
        /// </summary>
        public bool HasAdvancedPowerSettings => AdvancedPowerSettingGroups.Count > 0;

        /// <summary>
        /// Gets or sets a value indicating whether advanced power settings are being loaded.
        /// </summary>
        [ObservableProperty]
        private bool _isLoadingAdvancedSettings;

        /// <summary>
        /// Gets or sets the selected power plan.
        /// </summary>
        [ObservableProperty]
        private PowerPlan _selectedPowerPlan;

        /// <summary>
        /// Gets or sets the available power plans.
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<PowerPlan> _powerPlans = new();

        /// <summary>
        /// Gets the power plan labels for backward compatibility.
        /// </summary>
        public List<string> PowerPlanLabels { get; private set; } = new List<string>
        {
            "High Performance",
            "Balanced",
            "Power Saver",
            "Ultimate Performance",
        };

        // Delegate properties to UI Coordinator
        public ObservableCollection<SettingUIItem> Settings => _uiCoordinator.Settings;
        public ObservableCollection<SettingGroup> SettingGroups => _uiCoordinator.SettingGroups;
        public bool IsLoading
        {
            get => _uiCoordinator.IsLoading;
            set => _uiCoordinator.IsLoading = value;
        }
        public string CategoryName
        {
            get => _uiCoordinator.CategoryName;
            set => _uiCoordinator.CategoryName = value;
        }
        public string SearchText
        {
            get => _uiCoordinator.SearchText;
            set => _uiCoordinator.SearchText = value;
        }
        public bool HasVisibleSettings => _uiCoordinator.HasVisibleSettings;

        // IFeatureViewModel implementation
        public string ModuleId => "Power";
        public string DisplayName => "Power";
        public int SettingsCount => Settings?.Count ?? 0;
        public string Category => "Optimize";
        public string Description => "Optimize Windows power settings";
        public int SortOrder => 4;
        public ICommand LoadSettingsCommand { get; }
        
        [ObservableProperty]
        private bool _isExpanded = true;
        
        public ICommand ToggleExpandCommand { get; }
        


        /// <summary>
        /// Initializes a new instance of the <see cref="PowerOptimizationsViewModel"/> class.
        /// </summary>
        /// <param name="powerService">The power domain service.</param>
        /// <param name="uiCoordinator">The settings UI coordinator.</param>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="logService">The log service.</param>
        /// <param name="dialogService">The dialog service.</param>
        public PowerOptimizationsViewModel(
            IPowerService powerService,
            ISettingsUICoordinator uiCoordinator,
            ITaskProgressService progressService,
            ILogService logService,
            IDialogService dialogService)
        {
            _powerService = powerService ?? throw new ArgumentNullException(nameof(powerService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _uiCoordinator = uiCoordinator ?? throw new ArgumentNullException(nameof(uiCoordinator));
            _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));

            _uiCoordinator.CategoryName = "Power Settings";
            
            // Subscribe to coordinator's PropertyChanged events to relay them to the UI
            _uiCoordinator.PropertyChanged += (sender, e) => OnPropertyChanged(e.PropertyName);
            
            // Initialize commands
            LoadSettingsCommand = new AsyncRelayCommand(LoadSettingsAsync);
            ToggleExpandCommand = new RelayCommand(ToggleExpand);
        }

        /// <summary>
        /// Loads settings and initializes the UI state.
        /// </summary>
        public async Task LoadSettingsAsync()
        {
            try
            {
                _progressService.StartTask("Loading power optimization settings...");
                
                // Load available power plans for the power plan selector
                var powerPlans = await _powerService.GetAvailablePowerPlansAsync();
                PowerPlans.Clear();
                foreach (var plan in powerPlans.Cast<PowerPlan>())
                {
                    PowerPlans.Add(plan);
                }
                
                // Get active power plan
                var activePlan = await _powerService.GetActivePowerPlanAsync() as PowerPlan;
                if (activePlan != null)
                {
                    SelectedPowerPlan = activePlan;
                }
                
                // Check system capabilities
                var capabilities = await _powerService.CheckPowerSystemCapabilitiesAsync();
                HasBattery = capabilities.GetValueOrDefault("HasBattery", false);
                HasLid = capabilities.GetValueOrDefault("HasLid", false);
                
                // Load settings using UI coordinator - Application Service handles business logic
                await _uiCoordinator.LoadSettingsAsync(() => _powerService.GetSettingsAsync());
                
                _progressService.CompleteTask();
            }
            catch (Exception ex)
            {
                _progressService.CompleteTask();
                _logService.Log(LogLevel.Error, $"Error loading power optimization settings: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Checks the system capabilities for power management.
        /// </summary>
        private async Task CheckSystemCapabilitiesAsync()
        {
            try
            {
                // For now, assume desktop systems don't have battery/lid
                // This can be enhanced later with proper system detection
                HasBattery = false;
                HasLid = false;
                
                _logService.Log(LogLevel.Info, $"System capabilities: Battery={HasBattery}, Lid={HasLid}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking system capabilities: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the application settings that this ViewModel manages.
        /// </summary>
        /// <returns>Collection of application settings for power optimizations.</returns>
        private async Task<IEnumerable<ApplicationSetting>> GetApplicationSettingsAsync()
        {
            return await _powerService.GetSettingsAsync();
        }

        /// <summary>
        /// Refreshes the settings for this feature asynchronously.
        /// </summary>
        public async Task RefreshSettingsAsync()
        {
            await LoadSettingsAsync();
        }

        /// <summary>
        /// Clears all settings and resets the feature state.
        /// </summary>
        public void ClearSettings()
        {
            _uiCoordinator.ClearSettings();
        }
        
        private void ToggleExpand()
        {
            IsExpanded = !IsExpanded;
        }
    }
}
