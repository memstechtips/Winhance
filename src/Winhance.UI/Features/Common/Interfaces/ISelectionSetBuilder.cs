using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.UI.Features.Common.Interfaces;

// Composes the sources into the one SelectionSet each entry point needs: snapshot + checked apps (the legacy
// "generate from this machine" entry points), snapshot + installed apps (the startup backup), or snapshot with the
// Builder session's edits laid over it by setting id (Builder Save).
public interface ISelectionSetBuilder
{
    Task<SelectionSet> FromMachineAsync();
    Task<SelectionSet> FromMachineForBackupAsync();
    Task<SelectionSet> FromBuilderSessionAsync();
    CatalogScope CurrentScope { get; }
}
