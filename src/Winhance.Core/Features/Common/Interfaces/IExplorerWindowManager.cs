namespace Winhance.Core.Features.Common.Interfaces;

public interface IExplorerWindowManager
{
    // An Explorer window already open on that folder is brought to the foreground instead of opening a new one.
    Task OpenFolderAsync(string folderPath);
}
