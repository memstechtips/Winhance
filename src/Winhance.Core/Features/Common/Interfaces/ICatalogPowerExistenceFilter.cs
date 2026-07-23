using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>Hides a powercfg Setting whose GUID is not present on this machine (after trying to unhide it), or
/// that is hardware-controlled. Reads Availability.ValidatesExistence + the setting's PowerCfg and Task targets.</summary>
public interface ICatalogPowerExistenceFilter
{
    Task<IReadOnlyList<Setting>> FilterAsync(IReadOnlyList<Setting> settings);
}
