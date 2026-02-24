using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Abstracts Explorer window management (open folder, bring existing window to foreground).
/// Moves P/Invoke and COM Shell interop out of ViewModels for testability.
/// </summary>
public interface IExplorerWindowManager
{
    /// <summary>
    /// Opens a folder in Windows Explorer. If an Explorer window for that folder
    /// is already open, brings it to the foreground instead of opening a new one.
    /// </summary>
    Task OpenFolderAsync(string folderPath);
}
