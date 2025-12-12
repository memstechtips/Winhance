using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Controls;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Common.Resources.Theme;
using Winhance.WPF.Features.Common.Utilities;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Views;

namespace Winhance.WPF.Features.Common.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly INavigationService _navigationService;
        private readonly IEventBus _eventBus;
        private readonly ITaskProgressService _taskProgressService;
        private readonly IWindowManagementService _windowManagement;
        private readonly IConfigurationService _configurationService;
        private readonly IFlyoutManagementService _flyoutManagement;
        private readonly IUserPreferencesService _preferencesService;
        private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
        private readonly IDialogService _dialogService;
        private readonly IFilterUpdateService _filterUpdateService;
        private readonly ILocalizationService _localizationService;
        private readonly HashSet<BaseCategoryViewModel> _loadedCategoryViewModels = new();

        public INavigationService NavigationService => _navigationService;

        [ObservableProperty]
        private object _currentViewModel;

        public object CurrentViewInstance
        {
            get
            {
                if (_navigationService is Infrastructure.Features.Common.Services.FrameNavigationService frameNav)
                {
                    return frameNav.CurrentViewInstance;
                }
                return null;
            }
        }

        private string _currentViewName = string.Empty;
        public string CurrentViewName
        {
            get => _currentViewName;
            set => SetProperty(ref _currentViewName, value);
        }

        [ObservableProperty]
        private string _selectedNavigationItem = string.Empty;

        [ObservableProperty]
        private string _maximizeButtonContent = "\uE739";

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _loadingRoute = string.Empty;

        [ObservableProperty]
        private string _appName = string.Empty;

        [ObservableProperty]
        private string _lastTerminalLine = string.Empty;

        [ObservableProperty]
        private bool _isDialogOverlayVisible;

        [ObservableProperty]
        private bool _isWindowsVersionFilterEnabled = true;

        public string WindowsVersionFilterTooltip =>
            IsWindowsVersionFilterEnabled
                ? _localizationService.GetString("Tooltip_FilterEnabled") + "\n" +
                  _localizationService.GetString("Tooltip_FilterEnabled_Description")
                : _localizationService.GetString("Tooltip_FilterDisabled") + "\n" +
                  _localizationService.GetString("Tooltip_FilterDisabled_Description");

        partial void OnIsWindowsVersionFilterEnabledChanged(bool value)
        {
            OnPropertyChanged(nameof(WindowsVersionFilterTooltip));
        }

        public MoreMenuViewModel MoreMenuViewModel { get; }
        public Winhance.WPF.Features.AdvancedTools.ViewModels.AdvancedToolsMenuViewModel AdvancedToolsMenuViewModel { get; }
        public ICommand SaveUnifiedConfigCommand { get; }
        public ICommand ImportUnifiedConfigCommand { get; }
        public ICommand OpenDonateCommand { get; }
        public ICommand OpenBugReportCommand { get; }
        public ICommand MoreCommand { get; }
        public ICommand AdvancedToolsCommand { get; }
        public ICommand ToggleWindowsVersionFilterCommand { get; }
        public ICommand CancelCommand => new RelayCommand(() => _taskProgressService.CancelCurrentTask());



        public MainViewModel(
            INavigationService navigationService,
            IEventBus eventBus,
            ITaskProgressService taskProgressService,
            IWindowManagementService windowManagement,
            IConfigurationService configurationService,
            IFlyoutManagementService flyoutManagement,
            IUserPreferencesService preferencesService,
            ICompatibleSettingsRegistry compatibleSettingsRegistry,
            IDialogService dialogService,
            IFilterUpdateService filterUpdateService,
            ILocalizationService localizationService,
            MoreMenuViewModel moreMenuViewModel,
            Winhance.WPF.Features.AdvancedTools.ViewModels.AdvancedToolsMenuViewModel advancedToolsMenuViewModel
        )
        {
            _navigationService = navigationService;
            _eventBus = eventBus;
            _taskProgressService = taskProgressService;
            _windowManagement = windowManagement;
            _configurationService = configurationService;
            _flyoutManagement = flyoutManagement;
            _preferencesService = preferencesService;
            _compatibleSettingsRegistry = compatibleSettingsRegistry;
            _dialogService = dialogService;
            _filterUpdateService = filterUpdateService;
            _localizationService = localizationService;
            MoreMenuViewModel = moreMenuViewModel;
            AdvancedToolsMenuViewModel = advancedToolsMenuViewModel;

            SaveUnifiedConfigCommand = new AsyncRelayCommand(async () => await _configurationService.ExportConfigurationAsync());
            ImportUnifiedConfigCommand = new AsyncRelayCommand(async () => await _configurationService.ImportConfigurationAsync());
            OpenDonateCommand = new RelayCommand(OpenDonate);
            OpenBugReportCommand = new RelayCommand(OpenBugReport);
            MoreCommand = new RelayCommand(HandleMoreButtonClick);
            AdvancedToolsCommand = new RelayCommand(HandleAdvancedToolsButtonClick);
            ToggleWindowsVersionFilterCommand = new AsyncRelayCommand(ToggleWindowsVersionFilterAsync);

            _navigationService.Navigated += NavigationService_Navigated;
            _navigationService.Navigating += NavigationService_Navigating;
            _taskProgressService.ProgressUpdated += OnProgressUpdated;
        }

        private void OnProgressUpdated(object sender, TaskProgressDetail detail)
        {
            IsLoading = _taskProgressService.IsTaskRunning;

            if (string.IsNullOrEmpty(detail.TerminalOutput) && !string.IsNullOrEmpty(detail.StatusText))
            {
                AppName = detail.StatusText;
            }

            LastTerminalLine = detail.TerminalOutput ?? string.Empty;
        }

        private void NavigationService_Navigating(object sender, NavigationEventArgs e)
        {
            LoadingRoute = e.Route;
        }

        private void NavigationService_Navigated(object sender, NavigationEventArgs e)
        {
            LoadingRoute = string.Empty;
            CurrentViewName = e.Route;
            SelectedNavigationItem = e.Route;
            OnPropertyChanged(nameof(CurrentViewInstance));

            if (e.Parameter != null && e.Parameter is IFeatureViewModel)
            {
                CurrentViewModel = e.Parameter;
            }
            else if (e.ViewModelType != null)
            {
                try
                {
                    if (e.Parameter != null)
                    {
                        CurrentViewModel = e.Parameter;
                    }
                }
                catch (Exception ex)
                {
                    _eventBus.Publish(
                        new LogEvent
                        {
                            Message = $"Error getting current view model: {ex.Message}",
                            Level = LogLevel.Error,
                            Exception = ex,
                        }
                    );
                }
            }

            if (CurrentViewModel is BaseCategoryViewModel categoryViewModel)
            {
                _loadedCategoryViewModels.Add(categoryViewModel);
            }
        }

        [RelayCommand]
        private void MinimizeWindow()
        {
            _windowManagement.MinimizeWindow();
        }



        [RelayCommand]
        private void MaximizeRestoreWindow()
        {
            _windowManagement.MaximizeRestoreWindow();
        }

        [RelayCommand]
        private async Task CloseWindowAsync()
        {
            await _windowManagement.CloseWindowAsync();
        }






        private void OpenDonate()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "https://ko-fi.com/memstechtips",
                    UseShellExecute = true,
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                _eventBus.Publish(new LogEvent
                {
                    Message = $"Error opening donation page: {ex.Message}",
                    Level = LogLevel.Error,
                    Exception = ex,
                });
            }
        }

        private void OpenBugReport()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "https://github.com/memstechtips/Winhance/issues",
                    UseShellExecute = true,
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                _eventBus.Publish(new LogEvent
                {
                    Message = $"Error opening bug report page: {ex.Message}",
                    Level = LogLevel.Error,
                    Exception = ex,
                });
            }
        }

        public void InitializeApplication()
        {
            try
            {
                _navigationService.NavigateTo("SoftwareApps");
            }
            catch (Exception ex)
            {
                try
                {
                    _navigationService.NavigateTo("About");
                }
                catch (Exception fallbackEx)
                {
                    _eventBus.Publish(new LogEvent
                    {
                        Message = $"Failed to navigate to default views: {ex.Message}, Fallback: {fallbackEx.Message}",
                        Level = LogLevel.Error,
                        Exception = ex,
                    });
                }
            }
        }

        public async Task InitializeApplicationAsync()
        {
            try
            {
                await LoadFilterPreferenceAsync();
                await _navigationService.NavigateToAsync("SoftwareApps");
            }
            catch (Exception ex)
            {
                try
                {
                    await _navigationService.NavigateToAsync("About");
                }
                catch (Exception fallbackEx)
                {
                    _eventBus.Publish(new LogEvent
                    {
                        Message = $"Failed to navigate to default views: {ex.Message}, Fallback: {fallbackEx.Message}",
                        Level = LogLevel.Error,
                        Exception = ex,
                    });
                }
            }
        }

        public void HandleMoreButtonClick()
        {
            SelectedNavigationItem = "More";
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _flyoutManagement.ShowMoreMenuFlyout();
            }, DispatcherPriority.Render);
        }


        public void CloseMoreMenuFlyout()
        {
            _flyoutManagement.CloseMoreMenuFlyout();
            SelectedNavigationItem = CurrentViewName;
        }

        public void HandleAdvancedToolsButtonClick()
        {
            SelectedNavigationItem = "AdvancedTools";
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _flyoutManagement.ShowAdvancedToolsFlyout();
            }, DispatcherPriority.Render);
        }

        public void CloseAdvancedToolsFlyout()
        {
            _flyoutManagement.CloseAdvancedToolsFlyout();
            SelectedNavigationItem = CurrentViewName;
        }

        public void HandleWindowStateChanged(System.Windows.WindowState windowState)
        {
            MaximizeButtonContent = windowState == System.Windows.WindowState.Maximized
                ? "\uE923"
                : "\uE739";

            var domainWindowState = windowState switch
            {
                System.Windows.WindowState.Minimized => Core.Features.Common.Enums.WindowState.Minimized,
                System.Windows.WindowState.Maximized => Core.Features.Common.Enums.WindowState.Maximized,
                System.Windows.WindowState.Normal => Core.Features.Common.Enums.WindowState.Normal,
                _ => Core.Features.Common.Enums.WindowState.Normal
            };

            _windowManagement.HandleWindowStateChanged(domainWindowState);
        }

        public string GetThemeIconPath() => _windowManagement.GetThemeIconPath();

        public string GetDefaultIconPath() => _windowManagement.GetDefaultIconPath();

        public void RequestThemeIconUpdate() => _windowManagement.RequestThemeIconUpdate();

        private async Task LoadFilterPreferenceAsync()
        {
            IsWindowsVersionFilterEnabled = await _preferencesService.GetPreferenceAsync(
                Core.Features.Common.Constants.UserPreferenceKeys.EnableWindowsVersionFilter,
                defaultValue: true);

            _compatibleSettingsRegistry.SetFilterEnabled(IsWindowsVersionFilterEnabled);
        }

        private async Task ToggleWindowsVersionFilterAsync()
        {
            var dontShowAgain = await _preferencesService.GetPreferenceAsync(
                Core.Features.Common.Constants.UserPreferenceKeys.DontShowFilterExplanation,
                defaultValue: false);

            if (!dontShowAgain)
            {
                var (confirmed, dontShow) = await _dialogService.ShowConfirmationWithCheckboxAsync(
                    _localizationService.GetString("Filter_Dialog_Message"),
                    checkboxText: _localizationService.GetString("Filter_Dialog_Checkbox"),
                    title: _localizationService.GetString("Filter_Dialog_Title"),
                    continueButtonText: _localizationService.GetString("Filter_Dialog_Button_Toggle"),
                    cancelButtonText: _localizationService.GetString("Button_Cancel"),
                    titleBarIcon: "Filter");

                if (dontShow)
                {
                    await _preferencesService.SetPreferenceAsync(
                        Core.Features.Common.Constants.UserPreferenceKeys.DontShowFilterExplanation,
                        true);
                }

                if (!confirmed)
                {
                    return;
                }
            }

            IsWindowsVersionFilterEnabled = !IsWindowsVersionFilterEnabled;

            await _preferencesService.SetPreferenceAsync(
                Core.Features.Common.Constants.UserPreferenceKeys.EnableWindowsVersionFilter,
                IsWindowsVersionFilterEnabled);

            _compatibleSettingsRegistry.SetFilterEnabled(IsWindowsVersionFilterEnabled);

            foreach (var categoryViewModel in _loadedCategoryViewModels)
            {
                foreach (var view in categoryViewModel.FeatureViews)
                {
                    if (view.DataContext is ISettingsFeatureViewModel settingsVm)
                    {
                        await _filterUpdateService.UpdateFeatureSettingsAsync(settingsVm);
                    }
                }
            }

            if (CurrentViewModel is ISettingsFeatureViewModel settingsViewModel)
            {
                await _filterUpdateService.UpdateFeatureSettingsAsync(settingsViewModel);
            }
        }

    }
}
