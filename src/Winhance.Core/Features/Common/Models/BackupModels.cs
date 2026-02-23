using System;

namespace Winhance.Core.Features.Common.Models
{
    public class BackupResult
    {
        public bool Success { get; set; }
        public bool RestorePointCreated { get; set; }
        public bool SystemRestoreWasDisabled { get; set; }
        public DateTime? RestorePointDate { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
