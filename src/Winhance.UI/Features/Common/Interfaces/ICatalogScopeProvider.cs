using Winhance.Core.Features.Common.Catalog;

namespace Winhance.UI.Features.Common.Interfaces;

public interface ICatalogScopeProvider
{
    CatalogScope Current { get; }
}
