using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface ISystemBackupService
{
    // Enables System Restore first if it is disabled.
    Task<BackupResult> CreateRestorePointAsync(
        string? name = null,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);
}
