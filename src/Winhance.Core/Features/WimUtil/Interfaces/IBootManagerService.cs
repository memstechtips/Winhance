using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Core.Features.WimUtil.Interfaces;

public interface IBootManagerService
{
    Task<BootManagerOptions> InspectAsync(string workingDirectory, CancellationToken cancellationToken);

    Task<BootFileReplacements> PrepareAsync(string workingDirectory, BootManager bootManager, CancellationToken cancellationToken);
}
