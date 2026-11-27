using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.UI.Features.Common.Interfaces;

public interface ISelectionSetBuilder
{
    Task<SelectionSet> FromMachineAsync();
    Task<SelectionSet> FromMachineForBackupAsync();
    Task<SelectionSet> FromBuilderSessionAsync();
    CatalogScope CurrentScope { get; }
}
