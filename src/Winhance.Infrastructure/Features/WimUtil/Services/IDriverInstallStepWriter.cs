using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal interface IDriverInstallStepWriter
{
    Task<DriverInstallStepResult> EnsureAsync(string workingDirectory, CancellationToken cancellationToken = default);

    Task<DriverInstallStepResult> EnsureAsync(string xmlPath, string workingDirectory, CancellationToken cancellationToken = default);
}
