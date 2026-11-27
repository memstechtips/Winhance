namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface ILegacyCapabilityService
{
    string BuildEnableStatement(IReadOnlyList<string> capabilityNames);
}
