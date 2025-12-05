using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.WPF.Features.Common.Services;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Views;
using Winhance.WPF.Features.SoftwareApps.Views;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    public partial class SoftwareAppsViewModel : BaseContainerViewModel
    {
        protected override string DefaultStatusText => "Manage Windows Packages and Install External Software";

        public override string ModuleId => "SoftwareApps";
        public override string DisplayName => "Software & Apps";

        [ObservableProperty] private bool _isTableViewMode;

        public Visibility GridViewVisibility => IsTableViewMode ? Visibility.Collapsed : Visibility.Visible;
        public Visibility TableViewVisibility => IsTableViewMode ? Visibility.Visible : Visibility.Collapsed;

        public WindowsAppsViewModel WindowsAppsViewModel { get; private set; }
        public ExternalAppsViewModel ExternalAppsViewModel { get; private set; }

        [ObservableProperty]
        private bool _isWindowsAppsTabSelected = true;

        [ObservableProperty]
        private bool _isExternalAppsTabSelected = false;

        [ObservableProperty]
        private Visibility _windowsAppsContentVisibility = Visibility.Visible;

        [ObservableProperty]
        private Visibility _externalAppsContentVisibility = Visibility.Collapsed;

        [ObservableProperty]
        private bool _canInstallItems = false;

        [ObservableProperty]
        private bool _canRemoveItems = false;

        private readonly ILocalizationService _localizationService;

        public string RemoveButtonText => IsWindowsAppsTabSelected
            ? _localizationService.GetString("SoftwareApps_Button_RemoveSelected")
            : _localizationService.GetString("SoftwareApps_Button_UninstallSelected");

        [ObservableProperty]
        private object _currentHelpContent = null;

        [ObservableProperty]
        private bool _isHelpVisible = false;

        [ObservableProperty]
        private bool _isHelpFlyoutVisible = false;

        [ObservableProperty]
        private double _helpFlyoutLeft = 0;

        [ObservableProperty]
        private double _helpFlyoutTop = 0;

        [ObservableProperty]
        private bool _isHelpButtonActive = false;

        [ObservableProperty]
        private bool _shouldFocusHelpOverlay = false;

        [ObservableProperty]
        private FrameworkElement _helpButtonElement = null;

        public bool IsRefreshingContent
        {
            get
            {
                if (IsWindowsAppsTabSelected)
                    return WindowsAppsViewModel?.IsLoading ?? false;
                else
                    return ExternalAppsViewModel?.IsLoading ?? false;
            }
        }

        public SoftwareAppsViewModel(
            IServiceProvider serviceProvider,
            ISearchTextCoordinationService searchTextCoordinationService,
            ILocalizationService localizationService)
            : base(serviceProvider, searchTextCoordinationService)
        {
            _localizationService = localizationService;
            WindowsAppsViewModel = serviceProvider.GetRequiredService<WindowsAppsViewModel>();
            ExternalAppsViewModel = serviceProvider.GetRequiredService<ExternalAppsViewModel>();

            IsTableViewMode = IsWindowsAppsTabSelected
                ? WindowsAppsViewModel.IsTableViewMode
                : ExternalAppsViewModel.IsTableViewMode;

            this.PropertyChanged += SoftwareAppsViewModel_PropertyChanged;
            WindowsAppsViewModel.PropertyChanged += ChildViewModel_PropertyChanged;
            ExternalAppsViewModel.PropertyChanged += ChildViewModel_PropertyChanged;
            _localizationService.LanguageChanged += OnLanguageChanged;

            WindowsAppsViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(WindowsAppsViewModel.IsLoading))
                {
                    OnPropertyChanged(nameof(IsRefreshingContent));
                }
                else if (e.PropertyName == nameof(WindowsAppsViewModel.IsTaskRunning))
                {
                    UpdateButtonStates();
                }
            };

            ExternalAppsViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ExternalAppsViewModel.IsLoading))
                {
                    OnPropertyChanged(nameof(IsRefreshingContent));
                }
                else if (e.PropertyName == nameof(ExternalAppsViewModel.IsTaskRunning))
                {
                    UpdateButtonStates();
                }
            };

            UpdateButtonStates();
            Initialize();
        }

        partial void OnIsTableViewModeChanged(bool value)
        {
            OnPropertyChanged(nameof(GridViewVisibility));
            OnPropertyChanged(nameof(TableViewVisibility));

            if (IsWindowsAppsTabSelected)
            {
                if (WindowsAppsViewModel.IsTableViewMode != value)
                {
                    WindowsAppsViewModel.IsTableViewMode = value;
                }
            }
            else if (IsExternalAppsTabSelected)
            {
                if (ExternalAppsViewModel.IsTableViewMode != value)
                {
                    ExternalAppsViewModel.IsTableViewMode = value;
                }
            }
        }

        [RelayCommand]
        public async Task InitializeAsync()
        {
            var logService = serviceProvider.GetRequiredService<ILogService>();
            try
            {
                logService.LogInformation("[SoftwareAppsViewModel] InitializeAsync started");
                
                if (!WindowsAppsViewModel.IsInitialized)
                {
                    logService.LogInformation("[SoftwareAppsViewModel] Loading WindowsAppsViewModel");
                    await WindowsAppsViewModel.LoadAppsAndCheckInstallationStatusAsync().ConfigureAwait(false);
                    logService.LogInformation("[SoftwareAppsViewModel] WindowsAppsViewModel loaded");
                }
                else
                {
                    logService.LogInformation("[SoftwareAppsViewModel] WindowsAppsViewModel already initialized");
                }

                WindowsAppsViewModel.SelectedItemsChanged -= ChildViewModel_SelectedItemsChanged;
                WindowsAppsViewModel.SelectedItemsChanged += ChildViewModel_SelectedItemsChanged;

                if (!ExternalAppsViewModel.IsInitialized)
                {
                    logService.LogInformation("[SoftwareAppsViewModel] Loading ExternalAppsViewModel");
                    await ExternalAppsViewModel.LoadAppsAndCheckInstallationStatusAsync().ConfigureAwait(false);
                    logService.LogInformation("[SoftwareAppsViewModel] ExternalAppsViewModel loaded");
                }
                else
                {
                    logService.LogInformation("[SoftwareAppsViewModel] ExternalAppsViewModel already initialized");
                }

                ExternalAppsViewModel.SelectedItemsChanged -= ChildViewModel_SelectedItemsChanged;
                ExternalAppsViewModel.SelectedItemsChanged += ChildViewModel_SelectedItemsChanged;

                StatusText = DefaultStatusText;
                logService.LogInformation("[SoftwareAppsViewModel] InitializeAsync completed");
            }
            catch (Exception ex)
            {
                logService.LogError($"[SoftwareAppsViewModel] Error in InitializeAsync: {ex.Message}", ex);
                StatusText = $"Error initializing: {ex.Message}";
                throw;
            }
        }


        [RelayCommand]
        private void ToggleViewMode(object parameter)
        {
            bool newViewMode;
            if (parameter != null)
            {
                if (parameter is bool tableViewMode)
                {
                    newViewMode = tableViewMode;
                }
                else if (parameter is string stringParam && bool.TryParse(stringParam, out bool result))
                {
                    newViewMode = result;
                }
                else
                {
                    newViewMode = IsTableViewMode;
                }
            }
            else
            {
                newViewMode = !IsTableViewMode;
            }

            IsTableViewMode = newViewMode;

            if (IsWindowsAppsTabSelected)
            {
                WindowsAppsViewModel.IsTableViewMode = newViewMode;
                OnPropertyChanged(nameof(WindowsAppsViewModel));
                OnPropertyChanged(nameof(IsTableViewMode));
            }
            else
            {
                ExternalAppsViewModel.IsTableViewMode = newViewMode;
                OnPropertyChanged(nameof(ExternalAppsViewModel));
                OnPropertyChanged(nameof(IsTableViewMode));
            }

            UpdateButtonStates();
        }

        [RelayCommand]
        public void SelectTab(object parameter)
        {
            bool isWindowsAppsTab = true;

            if (parameter is string strParam)
            {
                isWindowsAppsTab = bool.Parse(strParam);
            }
            else if (parameter is bool boolParam)
            {
                isWindowsAppsTab = boolParam;
            }

            IsWindowsAppsTabSelected = isWindowsAppsTab;
            IsExternalAppsTabSelected = !isWindowsAppsTab;

            WindowsAppsContentVisibility = isWindowsAppsTab ? Visibility.Visible : Visibility.Collapsed;
            ExternalAppsContentVisibility = isWindowsAppsTab ? Visibility.Collapsed : Visibility.Visible;

            OnPropertyChanged(nameof(RemoveButtonText));

            RouteSearchTextToActiveViewModel();

            IsTableViewMode = isWindowsAppsTab
                ? WindowsAppsViewModel.IsTableViewMode
                : ExternalAppsViewModel.IsTableViewMode;

            UpdateButtonStates();
        }

        protected override void OnSearchTextChanged(object sender, SearchTextChangedEventArgs e)
        {
            base.OnSearchTextChanged(sender, e);
            RouteSearchTextToActiveViewModel();
        }

        private void SoftwareAppsViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SearchText))
            {
                RouteSearchTextToActiveViewModel();
            }
            else if (e.PropertyName == nameof(IsWindowsAppsTabSelected) || e.PropertyName == nameof(IsExternalAppsTabSelected))
            {
                UpdateButtonStates();
            }
        }

        private void RouteSearchTextToActiveViewModel()
        {
            WindowsAppsViewModel.SearchText = string.Empty;
            ExternalAppsViewModel.SearchText = string.Empty;

            if (IsWindowsAppsTabSelected)
            {
                WindowsAppsViewModel.SearchText = SearchText;
            }
            else
            {
                ExternalAppsViewModel.SearchText = SearchText;
            }
        }

        private void ChildViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName?.Contains("Selected") == true
                || e.PropertyName == nameof(WindowsAppsViewModel.HasSelectedItems)
                || e.PropertyName == nameof(ExternalAppsViewModel.HasSelectedItems))
            {
                UpdateButtonStates();
            }
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(RemoveButtonText));
        }

        private bool _isUpdatingButtonStates = false;

        private void UpdateButtonStates()
        {
            if (_isUpdatingButtonStates) return;

            try
            {
                _isUpdatingButtonStates = true;

                bool oldCanInstallItems = CanInstallItems;
                bool oldCanRemoveItems = CanRemoveItems;

                bool isAnyTaskRunning = (WindowsAppsViewModel?.IsTaskRunning ?? false) ||
                                        (ExternalAppsViewModel?.IsTaskRunning ?? false);

                if (IsWindowsAppsTabSelected)
                {
                    var hasSelected = WindowsAppsViewModel.HasSelectedItems;
                    CanInstallItems = hasSelected && !isAnyTaskRunning;
                    CanRemoveItems = hasSelected && !isAnyTaskRunning;
                }
                else if (IsExternalAppsTabSelected)
                {
                    var hasSelected = ExternalAppsViewModel.HasSelectedItems;
                    CanInstallItems = hasSelected && !isAnyTaskRunning;
                    CanRemoveItems = hasSelected && !isAnyTaskRunning;
                }
                else
                {
                    CanInstallItems = false;
                    CanRemoveItems = false;
                }

                OnPropertyChanged(nameof(CanInstallItems));
                OnPropertyChanged(nameof(CanRemoveItems));

                if (oldCanInstallItems != CanInstallItems)
                {
                    InstallSelectedItemsCommand.NotifyCanExecuteChanged();
                }

                if (oldCanRemoveItems != CanRemoveItems)
                {
                    RemoveSelectedItemsCommand.NotifyCanExecuteChanged();
                }
            }
            finally
            {
                _isUpdatingButtonStates = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanInstallSelectedItems))]
        private async Task InstallSelectedItems()
        {
            if (IsWindowsAppsTabSelected)
            {
                await WindowsAppsViewModel.InstallApps();
            }
            else
            {
                await ExternalAppsViewModel.InstallApps();
            }

            UpdateButtonStates();
        }

        private bool CanInstallSelectedItems() => CanInstallItems;

        [RelayCommand(CanExecute = nameof(CanRemoveSelectedItems))]
        private async Task RemoveSelectedItems()
        {
            if (IsWindowsAppsTabSelected)
            {
                await WindowsAppsViewModel.RemoveApps();
            }
            else
            {
                await ExternalAppsViewModel.RemoveApps();
            }

            UpdateButtonStates();
        }

        private bool CanRemoveSelectedItems() => CanRemoveItems;

        [RelayCommand]
        private async Task RefreshInstallationStatus()
        {
            if (IsWindowsAppsTabSelected)
            {
                await WindowsAppsViewModel.RefreshInstallationStatusCommand.ExecuteAsync(null);
            }
            else
            {
                await ExternalAppsViewModel.RefreshInstallationStatusCommand.ExecuteAsync(null);
            }
        }

        [RelayCommand]
        private void ShowHelp()
        {
            if (IsWindowsAppsTabSelected)
            {
                var logService = serviceProvider.GetRequiredService<ILogService>();
                var scheduledTaskService = serviceProvider.GetRequiredService<IScheduledTaskService>();

                var viewModel = new WindowsAppsHelpContentViewModel(scheduledTaskService, logService);
                viewModel.CloseHelpCommand = HideHelpFlyoutCommand;
                viewModel.Initialize();
                var helpContent = new WindowsAppsHelpContent(viewModel);
                CurrentHelpContent = helpContent;
            }
            else
            {
                var helpContent = new ExternalAppsHelpContent();
                var viewModel = new ExternalAppsHelpViewModel { CloseHelpCommand = HideHelpFlyoutCommand };
                helpContent.DataContext = viewModel;
                CurrentHelpContent = helpContent;
            }

            CalculateHelpFlyoutPosition();
            IsHelpFlyoutVisible = true;
            IsHelpButtonActive = true;
            ShouldFocusHelpOverlay = !ShouldFocusHelpOverlay;

            if (Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
            {
                mainViewModel.IsDialogOverlayVisible = true;
            }
        }

        private void CalculateHelpFlyoutPosition()
        {
            if (HelpButtonElement == null) return;

            try
            {
                var softwareAppsView = FindAncestorOfType<UserControl>(HelpButtonElement);
                if (softwareAppsView == null) return;

                var buttonPosition = HelpButtonElement.TransformToAncestor(softwareAppsView).Transform(new Point(0, 0));
                HelpFlyoutLeft = buttonPosition.X - 520;
                HelpFlyoutTop = buttonPosition.Y + HelpButtonElement.ActualHeight + 5;

                if (softwareAppsView.ActualWidth > 0 && softwareAppsView.ActualHeight > 0)
                {
                    if (HelpFlyoutLeft < 20)
                    {
                        HelpFlyoutLeft = 20;
                    }

                    if (HelpFlyoutLeft + 520 > softwareAppsView.ActualWidth - 20)
                    {
                        HelpFlyoutLeft = softwareAppsView.ActualWidth - 540;
                    }

                    if (HelpFlyoutTop + 450 > softwareAppsView.ActualHeight - 20)
                    {
                        HelpFlyoutTop = buttonPosition.Y - 455;

                        if (HelpFlyoutTop < 20)
                        {
                            HelpFlyoutTop = 20;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var logService = serviceProvider.GetRequiredService<ILogService>();
                logService.LogWarning($"Failed to calculate help flyout position: {ex.Message}");
                HelpFlyoutLeft = 200;
                HelpFlyoutTop = 100;
            }
        }

        [RelayCommand]
        private void HideHelpFlyout()
        {
            IsHelpFlyoutVisible = false;
            IsHelpButtonActive = false;

            if (Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
            {
                // Only turn off overlay if no CustomDialog windows are currently open.
                // If a dialog is open, let the dialog's Closed event handle turning off the overlay.
                // This prevents the overlay from being removed when the help flyout auto-closes
                // due to a dialog being shown from within the flyout (StaysOpen="False" behavior).
                var hasOpenDialogs = Application.Current.Windows
                    .OfType<CustomDialog>()
                    .Any();

                if (!hasOpenDialogs)
                {
                    mainViewModel.IsDialogOverlayVisible = false;
                }
            }

            var helpControl = CurrentHelpContent as UserControl;
            var disposableViewModel = helpControl?.DataContext as IDisposable;

            if (helpControl is WindowsAppsHelpContent windowsHelp)
            {
                StopHelpContentAnimations(windowsHelp);
            }

            if (helpControl != null)
            {
                helpControl.DataContext = null;
            }

            CurrentHelpContent = null;

            disposableViewModel?.Dispose();

            System.Threading.Tasks.Task.Run(() =>
            {
                System.Threading.Thread.Sleep(100);
                GC.Collect(0, GCCollectionMode.Optimized);
            });
        }

        private static void StopHelpContentAnimations(DependencyObject parent)
        {
            if (parent == null) return;

            if (parent is UIElement element)
            {
                element.BeginAnimation(UIElement.OpacityProperty, null);
            }

            if (parent is FrameworkElement frameworkElement && frameworkElement.RenderTransform is System.Windows.Media.RotateTransform rotateTransform)
            {
                rotateTransform.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
            }

            int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                StopHelpContentAnimations(child);
            }
        }

        private static T FindAncestorOfType<T>(DependencyObject element) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is T) return (T)parent;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        private void ChildViewModel_SelectedItemsChanged(object sender, EventArgs e)
        {
            UpdateButtonStates();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _localizationService.LanguageChanged -= OnLanguageChanged;
            }
            base.Dispose(disposing);
        }
    }
}