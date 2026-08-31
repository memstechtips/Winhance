using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

internal class OptionalFeatureService(IServicingSession servicingSession) : IOptionalFeatureService
{
    // -FeatureName is documented as String[], so the whole batch is one cmdlet call.
    // LegacyCapabilityService cannot do that (Add-WindowsCapability takes a single -Name)
    // and emits one statement per name instead.
    public string BuildEnableStatement(IReadOnlyList<string> featureNames)
    {
        var names = string.Join(",", featureNames.Select(n => $"'{n.Replace("'", "''")}'"));
        return $"Enable-WindowsOptionalFeature -Online -FeatureName {names} -All -NoRestart";
    }

    public Task<bool> EnableFeaturesAsync(
        IReadOnlyList<string> featureNames,
        IReadOnlyList<string>? displayNames = null,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (featureNames is null || featureNames.Count == 0)
            return Task.FromResult(false);

        var label = string.Join(", ", displayNames is { Count: > 0 } ? displayNames : featureNames);
        return servicingSession.RunAsync([BuildEnableStatement(featureNames)], label, progress, cancellationToken);
    }
}
