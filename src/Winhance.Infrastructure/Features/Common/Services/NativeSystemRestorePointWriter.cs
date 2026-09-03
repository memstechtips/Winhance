using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.System.Restore;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

internal sealed class NativeSystemRestorePointWriter(ILogService logService) : ISystemRestorePointWriter
{
    // szDescription is a fixed MAX_DESC_W-char buffer whose setter throws instead of truncating,
    // so a longer name must be trimmed, and the last slot is reserved for the null terminator.
    internal const int MaxDescriptionLength = (int)PInvoke.MAX_DESC_W - 1;

    private const string SystemRestoreKeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
    private const string FrequencyValueName = "SystemRestorePointCreationFrequency";

    public (bool Success, int StatusCode) CreateRestorePoint(string description)
    {
        // Windows throttles restore point creation to once per 24 hours by default.
        // SRSetRestorePointW silently returns success without creating a restore point
        // if one was already created within the frequency window.
        // Temporarily set the frequency to 0 (no limit) so our call always creates one.
        DisableRestorePointFrequencyThrottle(out var previousValue);

        try
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
            return (success, (int)status.nStatus);
        }
        finally
        {
            RestoreRestorePointFrequencyThrottle(previousValue);
        }
    }

    internal static string TruncateDescription(string description) =>
        description.Length <= MaxDescriptionLength
            ? description
            : description[..MaxDescriptionLength];

    private void DisableRestorePointFrequencyThrottle(out int? previousValue)
    {
        previousValue = null;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(SystemRestoreKeyPath, writable: true);
            if (key == null) return;

            var existing = key.GetValue(FrequencyValueName);
            if (existing is int intVal)
            {
                previousValue = intVal;
                if (intVal == 0) return;
            }

            key.SetValue(FrequencyValueName, 0, RegistryValueKind.DWord);
            logService.Log(LogLevel.Info, "Temporarily disabled restore point creation frequency throttle");
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Could not disable restore point frequency throttle: {ex.Message}");
        }
    }

    private void RestoreRestorePointFrequencyThrottle(int? previousValue)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(SystemRestoreKeyPath, writable: true);
            if (key == null) return;

            if (previousValue.HasValue)
            {
                key.SetValue(FrequencyValueName, previousValue.Value, RegistryValueKind.DWord);
            }
            else
            {
                key.DeleteValue(FrequencyValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"Could not restore frequency throttle value: {ex.Message}");
        }
    }
}
