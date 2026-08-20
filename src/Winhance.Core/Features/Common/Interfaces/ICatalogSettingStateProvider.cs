using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

// Its machine-independent behaviour (the Windows-grounded IsEnabled rule, the selection value-match fallback) is
// gated by CatalogSettingStateProviderConformanceTests.
public interface ICatalogSettingStateProvider
{
    Task<Dictionary<string, SettingStateResult>> GetStatesAsync(IReadOnlyList<Setting> settings);
}
