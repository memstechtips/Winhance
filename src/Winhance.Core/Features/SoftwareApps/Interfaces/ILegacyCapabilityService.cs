using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface ILegacyCapabilityService
{
    string BuildEnableStatement(IReadOnlyList<string> capabilityNames);
    Task<bool> EnableCapabilitiesAsync(IReadOnlyList<string> capabilityNames, IReadOnlyList<string>? displayNames = null, IProgress<TaskProgressDetail>? progress = null, CancellationToken cancellationToken = default);
}
