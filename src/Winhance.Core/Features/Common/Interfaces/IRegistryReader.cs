using Microsoft.Win32;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Read-only operations for the Windows Registry.
    /// </summary>
    public interface IRegistryReader
    {
        // Low-level reads
        object? GetValue(string fullKeyPath, string valueName);
        bool KeyExists(string fullKeyPath);
        bool ValueExists(string fullKeyPath, string valueName);

        // High-level reads based on domain models
        Task<object?> GetCurrentValueAsync(RegistrySetting setting);

        // Status helpers may rely on comparisons; exposed via IRegistryStatus
    }
}
