using Winhance.Core.Features.Common.Catalog;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

// One place that turns the two UI filter toggles into the scope the catalog registry answers over, so
// a caller never has to remember that a filter being ON means the scope flag is OFF.
public sealed class CatalogScopeProvider : ICatalogScopeProvider
{
    private readonly IWindowsVersionFilterService _versionFilter;
    private readonly IHardwareFilterService _hardwareFilter;

    public CatalogScopeProvider(IWindowsVersionFilterService versionFilter, IHardwareFilterService hardwareFilter)
    {
        _versionFilter = versionFilter;
        _hardwareFilter = hardwareFilter;
    }

    public CatalogScope Current => new(
        IncludeOtherOsVersions: !_versionFilter.IsFilterEnabled,
        IncludeOtherHardware: !_hardwareFilter.IsFilterEnabled);
}
