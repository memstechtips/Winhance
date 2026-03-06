using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.SoftwareApps.Interfaces;

public interface IAppDomainService
{
    string DomainName { get; }
    Task<IEnumerable<ItemDefinition>> GetAppsAsync();
    void InvalidateStatusCache();
    event EventHandler? WinGetReady;
}