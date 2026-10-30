using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal interface IUpdateInstallStepWriter
{
    Task<UpdateInstallStepResult> EnsureAsync(string workingDirectory, CancellationToken cancellationToken = default);

    Task<UpdateInstallStepResult> EnsureAsync(string xmlPath, string workingDirectory, CancellationToken cancellationToken = default);

    // True when the answer file changed.
    Task<bool> RemoveAsync(string workingDirectory, CancellationToken cancellationToken = default);
}
