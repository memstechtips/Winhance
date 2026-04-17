using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface ISystemBackupService
{
    /// <summary>
    /// Creates a system restore point with the given name.
    /// Enables System Restore first if it is currently disabled.
    /// </summary>
    Task<BackupResult> CreateRestorePointAsync(
        string? name = null,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);
}
