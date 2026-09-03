namespace Winhance.Infrastructure.Features.Common.Services;

// The WMI namespace IWmiApi.Query/InvokeClassMethod hits when a caller has no reason to name
// anything else. Callers with their own namespace (WmiStorageService's Storage Management API,
// SystemBackupService's SystemRestore class) keep their own const instead of using this.
internal static class WmiScope
{
    internal const string Cimv2 = @"root\cimv2";
}
