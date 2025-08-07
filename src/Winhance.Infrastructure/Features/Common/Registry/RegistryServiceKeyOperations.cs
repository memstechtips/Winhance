using Microsoft.Win32;
using System;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Infrastructure.Features.Common.Registry
{
    /// <summary>
    /// Registry service implementation for key operations.
    /// </summary>
    public partial class RegistryService
    {
        /// <summary>
        /// Opens a registry key with the specified access rights.
        /// </summary>
        /// <param name="keyPath">The full path to the registry key.</param>
        /// <param name="writable">Whether to open the key with write access.</param>
        /// <returns>The opened registry key, or null if it could not be opened.</returns>
        private RegistryKey? OpenRegistryKey(string keyPath, bool writable)
        {
            string[] pathParts = keyPath.Split('\\');
            RegistryKey? rootKey = GetRootKey(pathParts[0]);

            if (rootKey == null)
            {
                _logService.Log(LogLevel.Error, $"Invalid root key: {pathParts[0]}");
                return null;
            }

            string subPath = string.Join('\\', pathParts.Skip(1));

            if (writable)
            {
                // If we need write access, try to ensure the key exists with proper access rights
                return EnsureKeyWithFullAccess(rootKey, subPath);
            }
            else
            {
                // For read-only access, just try to open the key normally
                return rootKey.OpenSubKey(subPath, false);
            }
        }

        /// <summary>
        /// Creates a registry key if it doesn't exist.
        /// </summary>
        /// <param name="keyPath">The full path to the registry key.</param>
        /// <returns>True if the key exists or was created successfully; otherwise, false.</returns>
        public bool CreateKeyIfNotExists(string keyPath)
        {
            if (!CheckWindowsPlatform())
                return false;

            try
            {
                _logService.Log(LogLevel.Info, $"Creating registry key if it doesn't exist: {keyPath}");

                string[] pathParts = keyPath.Split('\\');
                RegistryKey? rootKey = GetRootKey(pathParts[0]);

                if (rootKey == null)
                {
                    _logService.Log(LogLevel.Error, $"Invalid root key: {pathParts[0]}");
                    return false;
                }

                string subKeyPath = string.Join('\\', pathParts.Skip(1));

                // Create the key with direct Registry API and security settings
                RegistryKey? targetKey = EnsureKeyWithFullAccess(rootKey, subKeyPath);

                if (targetKey == null)
                {
                    _logService.Log(LogLevel.Warning, $"Could not create registry key: {keyPath}");
                    return false;
                }

                targetKey.Close();
                
                // No caching - direct registry access only
                _logService.Log(LogLevel.Debug, $"Registry key created: {keyPath}");
                
                _logService.Log(LogLevel.Success, $"Successfully created or verified registry key: {keyPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error creating registry key {keyPath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deletes a registry key.
        /// </summary>
        /// <param name="keyPath">The full path to the registry key.</param>
        /// <returns>True if the key was deleted successfully; otherwise, false.</returns>
        public bool DeleteKey(string keyPath)
        {
            if (!CheckWindowsPlatform())
                return false;

            try
            {
                _logService.Log(LogLevel.Info, $"Deleting registry key: {keyPath}");

                string[] pathParts = keyPath.Split('\\');
                RegistryKey? rootKey = GetRootKey(pathParts[0]);

                if (rootKey == null)
                {
                    _logService.Log(LogLevel.Error, $"Invalid root key: {pathParts[0]}");
                    return false;
                }

                string subKeyPath = string.Join('\\', pathParts.Skip(1));
                string parentPath = string.Join('\\', subKeyPath.Split('\\').Take(subKeyPath.Split('\\').Length - 1));
                string keyName = subKeyPath.Split('\\').Last();

                // Open the parent key with write access
                using (RegistryKey? parentKey = rootKey.OpenSubKey(parentPath, true))
                {
                    if (parentKey == null)
                    {
                        _logService.Log(LogLevel.Warning, $"Parent key does not exist: {pathParts[0]}\\{parentPath}");
                        return false;
                    }

                    // Delete the key
                    parentKey.DeleteSubKey(keyName, false);
                    
                    // No caching - direct registry access only
                    _logService.Log(LogLevel.Debug, $"Registry key deleted: {keyPath}");

                    _logService.Log(LogLevel.Success, $"Successfully deleted registry key: {keyPath}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error deleting registry key {keyPath}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if a registry key exists by directly accessing the registry.
        /// </summary>
        /// <param name="keyPath">The full path to the registry key.</param>
        /// <returns>True if the key exists; otherwise, false.</returns>
        public bool KeyExists(string keyPath)
        {
            if (!CheckWindowsPlatform())
                return false;

            try
            {
                _logService.Log(LogLevel.Debug, $"Checking if registry key exists: {keyPath}");

                string[] pathParts = keyPath.Split('\\');
                RegistryKey? rootKey = GetRootKey(pathParts[0]);

                if (rootKey == null)
                {
                    _logService.Log(LogLevel.Error, $"Invalid root key: {pathParts[0]}");
                    return false;
                }

                string subKeyPath = string.Join('\\', pathParts.Skip(1));

                // Try to open the key directly from the registry
                using (RegistryKey? key = rootKey.OpenSubKey(subKeyPath))
                {
                    bool exists = key != null;
                    _logService.Log(LogLevel.Debug, $"Registry key {keyPath} exists: {exists}");
                    return exists;
                }
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking if registry key exists {keyPath}: {ex.Message}");
                return false;
            }
        }
    }
}
