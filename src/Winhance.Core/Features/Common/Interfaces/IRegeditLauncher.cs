namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Launches regedit at a given registry path.
/// </summary>
public interface IRegeditLauncher
{
    void OpenAtPath(string registryPath);
}
