using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface ISystemBackupService
{
    Task<BackupResult> EnsureInitialBackupsAsync(
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);
}
