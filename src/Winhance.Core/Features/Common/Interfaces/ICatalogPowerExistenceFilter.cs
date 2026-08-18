using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Interfaces;

// Hides a powercfg setting whose GUID is not present on this machine (after trying to unhide it) or that is hardware-controlled.
public interface ICatalogPowerExistenceFilter
{
    Task<IReadOnlyList<Setting>> FilterAsync(IReadOnlyList<Setting> settings);
}
