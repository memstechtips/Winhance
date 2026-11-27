using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IServicingSession
{
    Task<bool> RunAsync(IReadOnlyList<string> statements, string label, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default);
}
