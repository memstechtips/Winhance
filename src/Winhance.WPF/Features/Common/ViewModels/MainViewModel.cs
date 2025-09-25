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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Controls;
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
        private readonly IConfigurationCoordinatorService _configurationCoordinator;
        private readonly IFlyoutManagementService _flyoutManagement;



        public INavigationService NavigationService => _navigationService;

        [ObservableProperty]
        private object _currentViewModel;

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
        private string _appName = string.Empty;

        [ObservableProperty]
        private string _lastTerminalLine = string.Empty;

        public MoreMenuViewModel MoreMenuViewModel { get; }
        public ICommand SaveUnifiedConfigCommand { get; }
        public ICommand ImportUnifiedConfigCommand { get; }
        public ICommand OpenDonateCommand { get; }
        public ICommand MoreCommand { get; }
        public ICommand CancelCommand => new RelayCommand(() => _taskProgressService.CancelCurrentTask());



        public MainViewModel(
            INavigationService navigationService,
            IEventBus eventBus,
            ITaskProgressService taskProgressService,
            IWindowManagementService windowManagement,
            IConfigurationCoordinatorService configurationCoordinator,
            IFlyoutManagementService flyoutManagement,
            MoreMenuViewModel moreMenuViewModel
        )
        {
            _navigationService = navigationService;
            _eventBus = eventBus;
            _taskProgressService = taskProgressService;
            _windowManagement = windowManagement;
            _configurationCoordinator = configurationCoordinator;
            _flyoutManagement = flyoutManagement;
            MoreMenuViewModel = moreMenuViewModel;

            SaveUnifiedConfigCommand = new AsyncRelayCommand(async () => await _configurationCoordinator.SaveUnifiedConfigAsync());
            ImportUnifiedConfigCommand = new AsyncRelayCommand(async () => await _configurationCoordinator.ImportUnifiedConfigAsync());
            OpenDonateCommand = new RelayCommand(OpenDonate);
            MoreCommand = new RelayCommand(HandleMoreButtonClick);

            _navigationService.Navigated += NavigationService_Navigated;
            _taskProgressService.ProgressUpdated += OnProgressUpdated;
        }

        private void OnProgressUpdated(object sender, TaskProgressDetail detail)
        {
            IsLoading = _taskProgressService.IsTaskRunning;
            AppName = detail.StatusText ?? string.Empty;
            LastTerminalLine = detail.TerminalOutput ?? string.Empty;
        }

        private void NavigationService_Navigated(object sender, NavigationEventArgs e)
        {
            CurrentViewName = e.Route;
            SelectedNavigationItem = e.Route;

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
        }


        [RelayCommand]
        private void ToggleTheme()
        {
            _windowManagement.ToggleTheme();
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

        public void HandleMoreButtonClick()
        {
            SelectedNavigationItem = "More";
            _flyoutManagement.ShowMoreMenuFlyout();
        }


        public void CloseMoreMenuFlyout()
        {
            _flyoutManagement.CloseMoreMenuFlyout();
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

    }
}
