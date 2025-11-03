using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Verification;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Verification.Methods;

public class AppDiscoveryVerificationMethod(IAppStatusDiscoveryService appStatusDiscoveryService, ILogService logService) : VerificationMethodBase("AppDiscovery", priority: 15), IVerificationMethod

{
    protected override async Task<VerificationResult> VerifyPresenceAsync(
        string packageId,
        CancellationToken cancellationToken)
    {
        try
        {
            var statusResults = await appStatusDiscoveryService.GetInstallationStatusByIdAsync([packageId]);
            bool isInstalled = statusResults.GetValueOrDefault(packageId, false);

            if (isInstalled)
            {
                return new VerificationResult
                {
                    IsVerified = true,
                    Message = $"Found package: {packageId}",
                    MethodUsed = "AppDiscovery"
                };
            }

            return VerificationResult.Failure($"Package '{packageId}' not found", "AppDiscovery");
        }
        catch (Exception ex)
        {
            logService?.LogError($"Error checking package {packageId}: {ex.Message}", ex);
            return VerificationResult.Failure($"Error checking package: {ex.Message}", "AppDiscovery");
        }
    }

    protected override async Task<VerificationResult> VerifyVersionAsync(
        string packageId,
        string version,
        CancellationToken cancellationToken)
    {
        var presenceResult = await VerifyPresenceAsync(packageId, cancellationToken);

        if (!presenceResult.IsVerified)
        {
            return presenceResult;
        }

        return new VerificationResult
        {
            IsVerified = true,
            Message = $"Package '{packageId}' is installed (version check not implemented)",
            MethodUsed = "AppDiscovery"
        };
    }
}