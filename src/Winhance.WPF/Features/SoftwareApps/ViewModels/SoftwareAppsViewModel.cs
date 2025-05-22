using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    /// <summary>
    /// ViewModel for the SoftwareAppsView that coordinates WindowsApps and ExternalApps sections.
    /// </summary>
    public partial class SoftwareAppsViewModel : BaseViewModel
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IPackageManager _packageManager;

        [ObservableProperty]
        private string _statusText =
            "Manage Windows Apps, Capabilities & Features and Install External Software";

        [ObservableProperty]
        private WindowsAppsViewModel _windowsAppsViewModel;

        [ObservableProperty]
        private ExternalAppsViewModel _externalAppsViewModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="SoftwareAppsViewModel"/> class.
        /// </summary>
        /// <param name="progressService">The task progress service.</param>
        /// <param name="packageManager">The package manager.</param>
        /// <param name="serviceProvider">The service provider for dependency resolution.</param>
        public SoftwareAppsViewModel(
            ITaskProgressService progressService,
            IPackageManager packageManager,
            IServiceProvider serviceProvider
        )
            : base(progressService)
        {
            _packageManager =
                packageManager ?? throw new ArgumentNullException(nameof(packageManager));
            _serviceProvider =
                serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            // Resolve the dependencies via DI container
            WindowsAppsViewModel = _serviceProvider.GetRequiredService<WindowsAppsViewModel>();
            ExternalAppsViewModel = _serviceProvider.GetRequiredService<ExternalAppsViewModel>();
        }

        /// <summary>
        /// Initializes child view models and prepares the view.
        /// </summary>
        [RelayCommand]
        public async Task Initialize()
        {
            StatusText = "Initializing Software Apps...";
            IsLoading = true;

            try
            {
                // Initialize WindowsAppsViewModel if not already initialized
                if (!WindowsAppsViewModel.IsInitialized)
                {
                    await WindowsAppsViewModel.LoadAppsAndCheckInstallationStatusAsync();
                }

                // Initialize ExternalAppsViewModel if not already initialized
                if (!ExternalAppsViewModel.IsInitialized)
                {
                    await ExternalAppsViewModel.LoadAppsAndCheckInstallationStatusAsync();
                }

                StatusText =
                    "Manage Windows Apps, Capabilities & Features and Install External Software";
            }
            catch (Exception ex)
            {
                StatusText = $"Error initializing: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Called when the view is navigated to.
        /// </summary>
        /// <param name="parameter">Navigation parameter.</param>
        public override async void OnNavigatedTo(object parameter)
        {
            try
            {
                // Initialize when navigated to this view
                await Initialize();
            }
            catch (Exception ex)
            {
                StatusText = $"Error during navigation: {ex.Message}";
                // Log the error or handle it appropriately
            }
        }
    }
}
