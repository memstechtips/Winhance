using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

internal class LegacyCapabilityService(IServicingSession servicingSession) : ILegacyCapabilityService
{
    // Add-WindowsCapability documents no array -Name, so the batch is one statement per name rather
    // than the single call OptionalFeatureService makes. It also has no -NoRestart switch at all
    // (Get-Command, DISM module 3.0 on build 26100, 2026-08-26), so the asymmetry with the feature
    // statement is forced by the cmdlets - passing it would fail the command at runtime.
    public string BuildEnableStatement(IReadOnlyList<string> capabilityNames) =>
        string.Join("; ", capabilityNames.Select(n => $"Add-WindowsCapability -Online -Name '{n.Replace("'", "''")}'"));

    public Task<bool> EnableCapabilitiesAsync(
        IReadOnlyList<string> capabilityNames,
        IReadOnlyList<string>? displayNames = null,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (capabilityNames is null || capabilityNames.Count == 0)
            return Task.FromResult(false);

        var label = string.Join(", ", displayNames is { Count: > 0 } ? displayNames : capabilityNames);
        return servicingSession.RunAsync([BuildEnableStatement(capabilityNames)], label, progress, cancellationToken);
    }
}
