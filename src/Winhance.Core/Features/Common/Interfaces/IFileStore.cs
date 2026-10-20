using Winhance.Core.Features.Common.Selections;

namespace Winhance.Core.Features.Common.Interfaces;

// A browsed picture the target machine does not have travels inside the saved file as bytes.
public interface IFileStore
{
    // On save. SelectionSet.Files is the ONLY signal downstream that a file travels as bytes, so nothing else may fill it.
    Task<SelectionSet> LoadAsync(SelectionSet set);

    // On import: puts the carried files on this PC and points the values that named them at the local copies.
    Task<SelectionSet> MaterializeAsync(SelectionSet set);
}
