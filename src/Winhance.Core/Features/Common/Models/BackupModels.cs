namespace Winhance.Core.Features.Common.Models;

public sealed record BackupResult
{
    public bool Success { get; init; }
    public bool RestorePointCreated { get; init; }
    public DateTime? RestorePointDate { get; init; }
    public string? ErrorMessage { get; init; }

    // System Protection was off and this run turned it on, so a failure can offer to turn it back off.
    public bool TurnedProtectionOn { get; init; }

    public static BackupResult CreateSuccess(
        DateTime? restorePointDate = null,
        bool restorePointCreated = false)
    {
        return new BackupResult
        {
            Success = true,
            RestorePointDate = restorePointDate,
            RestorePointCreated = restorePointCreated
        };
    }

    public static BackupResult CreateFailure(string errorMessage, bool turnedProtectionOn = false)
    {
        return new BackupResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            TurnedProtectionOn = turnedProtectionOn
        };
    }
}
