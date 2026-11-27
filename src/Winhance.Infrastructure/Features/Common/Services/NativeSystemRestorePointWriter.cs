using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.System.Restore;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

internal sealed class NativeSystemRestorePointWriter(IWindowsRegistryService registry, ILogService logService) : ISystemRestorePointWriter
{
    // szDescription is a fixed MAX_DESC_W-char buffer whose setter throws instead of truncating,
    // so a longer name must be trimmed, and the last slot is reserved for the null terminator.
    internal const int MaxDescriptionLength = (int)PInvoke.MAX_DESC_W - 1;

    internal const string SystemRestoreKeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
    internal const string FrequencyValueName = "SystemRestorePointCreationFrequency";

    public (bool Success, int StatusCode, uint? SequenceNumber) CreateRestorePoint(string description) =>
        WithFrequencyLimitLifted(limitLifted =>
        {
            var restorePointInfo = new RESTOREPOINTINFOW
            {
                dwEventType = RESTOREPOINTINFO_EVENT_TYPE.BEGIN_SYSTEM_CHANGE,
                dwRestorePtType = RESTOREPOINTINFO_TYPE.MODIFY_SETTINGS,
                llSequenceNumber = 0,
                // The generated fixed buffer takes a span, not a string.
                szDescription = TruncateDescription(description).AsSpan()
            };

            bool success = PInvoke.SRSetRestorePoint(restorePointInfo, out var status);

            // Under the limit Windows reports the sequence number of the earlier point instead of making one.
            uint? sequenceNumber = success && limitLifted ? (uint)status.llSequenceNumber : null;
            return (success, (int)status.nStatus, sequenceNumber);
        });

    // SRSetRestorePointW skips the point and still reports success when one was made within
    // SystemRestorePointCreationFrequency minutes (24 hours when the value is absent); 0 never skips.
    // The value is set to 0 for this one call, then the user's value, or its absence, is put back.
    internal T WithFrequencyLimitLifted<T>(Func<bool, T> call)
    {
        var previous = registry.GetValue(SystemRestoreKeyPath, FrequencyValueName);
        if (previous is 0)
            return call(true);

        // Only a DWORD can be put back exactly with what was read, so any other kind is left alone.
        if (previous is not (null or int))
            return call(false);

        if (!registry.SetValue(SystemRestoreKeyPath, FrequencyValueName, 0, RegistryValueKind.DWord))
        {
            logService.Log(LogLevel.Warning, "Could not lift the restore point frequency limit");
            return call(false);
        }

        try
        {
            return call(true);
        }
        finally
        {
            var restored = previous is int value
                ? registry.SetValue(SystemRestoreKeyPath, FrequencyValueName, value, RegistryValueKind.DWord)
                : registry.DeleteValue(SystemRestoreKeyPath, FrequencyValueName);
            if (!restored)
                logService.Log(LogLevel.Warning, "Could not put back the restore point frequency limit");
        }
    }

    public int RemoveRestorePoint(uint sequenceNumber) => (int)PInvoke.SRRemoveRestorePoint(sequenceNumber);

    internal static string TruncateDescription(string description) =>
        description.Length <= MaxDescriptionLength
            ? description
            : description[..MaxDescriptionLength];
}
