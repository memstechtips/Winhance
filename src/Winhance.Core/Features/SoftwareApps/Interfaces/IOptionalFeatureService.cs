using System.Threading;
using System.Threading.Tasks;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IOptionalFeatureService
{
    Task<bool> EnableFeatureAsync(string featureName, string displayName = null, CancellationToken cancellationToken = default);
    Task<bool> DisableFeatureAsync(string featureName, string displayName = null, CancellationToken cancellationToken = default);
}