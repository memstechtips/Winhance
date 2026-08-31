using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

internal class OptionalFeatureService : IOptionalFeatureService
{
    // -FeatureName is documented as String[], so the whole batch is one cmdlet call.
    // LegacyCapabilityService cannot do that (Add-WindowsCapability takes a single -Name)
    // and emits one statement per name instead.
    public string BuildEnableStatement(IReadOnlyList<string> featureNames)
    {
        var names = string.Join(",", featureNames.Select(n => $"'{n.Replace("'", "''")}'"));
        return $"Enable-WindowsOptionalFeature -Online -FeatureName {names} -All -NoRestart";
    }

}
