using System;

namespace Winhance.Core.Features.Common.Models
{
    public record BackupResult
    {
        public bool Success { get; init; }
        public bool RestorePointCreated { get; init; }
        public bool SystemRestoreWasDisabled { get; init; }
        public DateTime? RestorePointDate { get; init; }
        public string? ErrorMessage { get; init; }

        public static BackupResult CreateSuccess(
            DateTime? restorePointDate = null,
            bool restorePointCreated = false,
            bool systemRestoreWasDisabled = false)
        {
            return new BackupResult
            {
                Success = true,
                RestorePointDate = restorePointDate,
                RestorePointCreated = restorePointCreated,
                SystemRestoreWasDisabled = systemRestoreWasDisabled
            };
        }

        public static BackupResult CreateFailure(
            string errorMessage,
            bool systemRestoreWasDisabled = false)
        {
            return new BackupResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                SystemRestoreWasDisabled = systemRestoreWasDisabled
            };
        }
    }
}
