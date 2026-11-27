using System.Runtime.InteropServices;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Native;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

internal sealed class DismComponentReader(ILogService logService) : IDismComponentReader
{
    public Task<InstalledWindowsComponents> ReadInstalledAsync(bool capabilities, IReadOnlyCollection<string> featureNames, CancellationToken cancellationToken) =>
        DismSessionManager.ExecuteAsync(session => new InstalledWindowsComponents(
            capabilities ? TryRead("GetCapabilities", () => ReadCapabilities(session)) : null,
            TryRead("GetFeatureInfo", () => ReadFeatures(session, featureNames))),
            cancellationToken, logService);

    private HashSet<string>? TryRead(string operation, Func<HashSet<string>> read)
    {
        try
        {
            return read();
        }
        catch (Exception ex)
        {
            logService.LogError($"DISM {operation} failed: {ex.GetType().Name}: {ex.Message}", ex);
            return null;
        }
    }

    private HashSet<string> ReadCapabilities(uint session)
    {
        DismApi.ThrowIfFailed(DismApi.DismGetCapabilities(session, out IntPtr capPtr, out uint count), "GetCapabilities");
        try
        {
            var installed = new HashSet<string>(
                DismApi.MarshalArray<DismApi.DISM_CAPABILITY>(capPtr, count)
                    .Where(c => c.State == DismApi.DismStateInstalled)
                    .Select(c => Marshal.PtrToStringUni(c.Name)!)
                    .Where(n => n != null),
                StringComparer.OrdinalIgnoreCase);

            logService.LogInformation($"DismGetCapabilities: {installed.Count} installed out of {count} total");
            return installed;
        }
        finally
        {
            _ = DismApi.DismDelete(capPtr);
        }
    }

    private HashSet<string> ReadFeatures(uint session, IReadOnlyCollection<string> featureNames)
    {
        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in featureNames)
        {
            // A feature this edition's image does not have fails the call, and is simply not installed.
            if (DismApi.DismGetFeatureInfo(session, name, null, 0, out IntPtr infoPtr) < 0)
                continue;

            try
            {
                if (Marshal.PtrToStructure<DismApi.DISM_FEATURE>(infoPtr).State == DismApi.DismStateInstalled)
                    enabled.Add(name);
            }
            finally
            {
                _ = DismApi.DismDelete(infoPtr);
            }
        }

        logService.LogInformation($"DismGetFeatureInfo: {enabled.Count} enabled out of {featureNames.Count} asked");
        return enabled;
    }
}
