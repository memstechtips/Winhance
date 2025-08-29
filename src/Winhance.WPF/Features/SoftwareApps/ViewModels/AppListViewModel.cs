using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.SoftwareApps.Models;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    public abstract partial class AppListViewModel<T> : BaseViewModel where T : class
    {
        protected readonly IPackageManager? _packageManager;
        protected readonly ITaskProgressService _progressService;

        [ObservableProperty]
        private bool _isLoading;

        public ObservableCollection<T> Items { get; } = new();

        protected AppListViewModel(ITaskProgressService progressService, IPackageManager? packageManager)
            : base(progressService)
        {
            _packageManager = packageManager;
            _progressService = progressService;
        }

        public abstract Task LoadItemsAsync();
        public abstract Task CheckInstallationStatusAsync();
    }
}
