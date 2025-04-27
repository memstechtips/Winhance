using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services
{
    /// <summary>
    /// Adapter class that adapts IAppRemovalService to IInstallationService&lt;AppInfo&gt;.
    /// This is used to maintain backward compatibility with code that expects the AppRemovalService
    /// property of IPackageManager to return an IInstallationService&lt;AppInfo&gt;.
    /// </summary>
    public class AppRemovalServiceAdapter : IInstallationService<AppInfo>
    {
        private readonly IAppRemovalService _appRemovalService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppRemovalServiceAdapter"/> class.
        /// </summary>
        /// <param name="appRemovalService">The app removal service to adapt.</param>
        public AppRemovalServiceAdapter(IAppRemovalService appRemovalService)
        {
            _appRemovalService = appRemovalService ?? throw new ArgumentNullException(nameof(appRemovalService));
        }

        /// <inheritdoc/>
        public Task<OperationResult<bool>> InstallAsync(
            AppInfo item,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            // This is an adapter for removal service, so "install" actually means "remove"
            return _appRemovalService.RemoveAppAsync(item, progress, cancellationToken);
        }

        /// <inheritdoc/>
        public Task<OperationResult<bool>> CanInstallAsync(AppInfo item)
        {
            // Since this is an adapter for removal, we'll always return true
            // The actual validation will happen in the RemoveAppAsync method
            return Task.FromResult(OperationResult<bool>.Succeeded(true));
        }
    }
}