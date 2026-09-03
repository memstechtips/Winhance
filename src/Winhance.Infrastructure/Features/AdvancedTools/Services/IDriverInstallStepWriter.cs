using Winhance.Core.Features.AdvancedTools.Models;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal interface IDriverInstallStepWriter
{
    Task<DriverInstallStepResult> EnsureAsync(string workingDirectory, CancellationToken cancellationToken = default);
}
