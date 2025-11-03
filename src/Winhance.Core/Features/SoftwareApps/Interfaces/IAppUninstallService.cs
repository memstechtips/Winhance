using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IAppUninstallService
{
    Task<OperationResult<bool>> UninstallAsync(
        ItemDefinition item,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);

    Task<UninstallMethod> DetermineUninstallMethodAsync(ItemDefinition item);
}

public enum UninstallMethod
{
    None,
    WinGet,
    Registry,
    CustomScript
}
