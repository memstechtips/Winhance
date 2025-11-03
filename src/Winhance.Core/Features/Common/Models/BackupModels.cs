using System;
using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Models
{
    public class BackupResult
    {
        public bool Success { get; set; }
        public bool RestorePointCreated { get; set; }
        public bool RestorePointExisted { get; set; }
        public bool SystemRestoreEnabled { get; set; }
        public bool SystemRestoreWasDisabled { get; set; }
        public DateTime? RestorePointDate { get; set; }
        public List<string> Warnings { get; set; } = new();
        public string? ErrorMessage { get; set; }

        public bool RegistryBackupCreated { get; set; }
        public bool RegistryBackupExisted { get; set; }
        public List<string> RegistryBackupPaths { get; set; } = new();
    }

    public class BackupStatus
    {
        public bool RestorePointExists { get; set; }
        public bool SystemRestoreEnabled { get; set; }
        public DateTime? RestorePointDate { get; set; }
    }
}
