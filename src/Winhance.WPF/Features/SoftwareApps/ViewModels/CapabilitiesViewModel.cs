using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels
{
    public partial class CapabilitiesViewModel : AppListViewModel<CapabilityInfo>
    {
        private readonly IAppLoadingService _appLoadingService;
        private readonly IInstallationOrchestrator _installationOrchestrator;

        public CapabilitiesViewModel(
            IAppLoadingService appLoadingService,
            IInstallationOrchestrator installationOrchestrator,
            ITaskProgressService taskProgressService,
            IPackageManager packageManager)
            : base(taskProgressService, packageManager)
        {
            _appLoadingService = appLoadingService;
            _installationOrchestrator = installationOrchestrator;
        }

        public override async Task LoadItemsAsync()
        {
            var capabilities = await _appLoadingService.LoadCapabilitiesAsync();
            Items.Clear();
            foreach (var item in capabilities)
            {
                Items.Add(item);
            }
        }

        public override async Task CheckInstallationStatusAsync()
        {
            await Task.Run(async () =>
            {
                foreach (var item in Items)
                {
                    // Use the AppLoadingService to check installation status
                    item.IsInstalled = await _appLoadingService.GetItemInstallStatusAsync(item);
                }
            });
        }

        [RelayCommand]
        private async Task InstallAsync()
        {
            var selected = Items.Where(item => item.IsSelected).ToList();
            if (!selected.Any())
            {
                return;
            }
            await _installationOrchestrator.InstallBatchAsync(selected);
        }

        [RelayCommand]
        private async Task RemoveAsync()
        {
            var selected = Items.Where(item => item.IsSelected).ToList();
            if (!selected.Any())
            {
                return;
            }
            await _installationOrchestrator.RemoveBatchAsync(selected);
        }
    }
}
