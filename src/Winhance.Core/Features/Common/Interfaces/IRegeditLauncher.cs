namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Checks registry key existence and launches regedit at a given path.
    /// </summary>
    public interface IRegeditLauncher
    {
        bool KeyExists(string registryPath);
        void OpenAtPath(string registryPath);
    }
}
