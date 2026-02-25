using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.AdvancedTools.Interfaces
{
    public interface IOscdimgToolManager
    {
        string GetOscdimgPath();

        Task<bool> IsOscdimgAvailableAsync();

        Task<bool> EnsureOscdimgAvailableAsync(
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);
    }
}
