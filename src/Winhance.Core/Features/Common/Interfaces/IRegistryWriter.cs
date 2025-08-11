using Microsoft.Win32;
using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Write operations for the Windows Registry.
    /// </summary>
    public interface IRegistryWriter
    {
        // Key operations
        bool CreateKeyIfNotExists(string fullKeyPath);
        bool DeleteKey(string fullKeyPath);

        // Value operations
        bool SetValue(string fullKeyPath, string valueName, object value, RegistryValueKind kind);
        bool DeleteValue(string fullKeyPath, string valueName);
    }
}
