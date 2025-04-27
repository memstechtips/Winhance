using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;

namespace Winhance.Infrastructure.Features.Common.Registry
{
    public partial class RegistryService
    {
        // Windows API imports for registry operations
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegOpenKeyEx(IntPtr hKey, string subKey, int ulOptions, int samDesired, out IntPtr hkResult);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegCloseKey(IntPtr hKey);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegGetKeySecurity(IntPtr hKey, int securityInformation, byte[] pSecurityDescriptor, ref int lpcbSecurityDescriptor);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegSetKeySecurity(IntPtr hKey, int securityInformation, byte[] pSecurityDescriptor);

        // Constants for Windows API
        private const int KEY_ALL_ACCESS = 0xF003F;
        private const int OWNER_SECURITY_INFORMATION = 0x00000001;
        private const int DACL_SECURITY_INFORMATION = 0x00000004;
        private const int ERROR_SUCCESS = 0;

        // Root key handles
        private static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(-2147483647);
        private static readonly IntPtr HKEY_LOCAL_MACHINE = new IntPtr(-2147483646);
        private static readonly IntPtr HKEY_CLASSES_ROOT = new IntPtr(-2147483648);
        private static readonly IntPtr HKEY_USERS = new IntPtr(-2147483645);
        private static readonly IntPtr HKEY_CURRENT_CONFIG = new IntPtr(-2147483643);

        /// <summary>
        /// Takes ownership of a registry key and grants full control to the current user.
        /// </summary>
        /// <param name="rootKey">The root registry key.</param>
        /// <param name="subKeyPath">The path to the subkey.</param>
        /// <returns>True if ownership was successfully taken, false otherwise.</returns>
        private bool TakeOwnershipOfKey(RegistryKey rootKey, string subKeyPath)
        {
            try
            {
                _logService.LogInformation($"Attempting to take ownership of registry key: {rootKey.Name}\\{subKeyPath}");

                // Get the root key handle
                IntPtr hRootKey = GetRootKeyHandle(rootKey);
                if (hRootKey == IntPtr.Zero)
                {
                    _logService.LogError("Invalid root key handle");
                    return false;
                }

                // Open the key with special permissions
                IntPtr hKey;
                int result = RegOpenKeyEx(hRootKey, subKeyPath, 0, KEY_ALL_ACCESS, out hKey);
                if (result != ERROR_SUCCESS)
                {
                    _logService.LogError($"Failed to open registry key for ownership change: {result}");
                    return false;
                }

                try
                {
                    // Get the current security descriptor
                    int securityDescriptorSize = 0;
                    RegGetKeySecurity(hKey, OWNER_SECURITY_INFORMATION | DACL_SECURITY_INFORMATION, null, ref securityDescriptorSize);
                    byte[] securityDescriptor = new byte[securityDescriptorSize];
                    result = RegGetKeySecurity(hKey, OWNER_SECURITY_INFORMATION | DACL_SECURITY_INFORMATION, securityDescriptor, ref securityDescriptorSize);
                    if (result != ERROR_SUCCESS)
                    {
                        _logService.LogError($"Failed to get registry key security: {result}");
                        return false;
                    }

                    // Create a new security descriptor
                    RawSecurityDescriptor rawSD = new RawSecurityDescriptor(securityDescriptor, 0);
                    
                    // Set the owner to the current user
                    WindowsIdentity currentUser = WindowsIdentity.GetCurrent();
                    rawSD.Owner = currentUser.User;

                    // Create a new DACL
                    RawAcl rawAcl = rawSD.DiscretionaryAcl != null ?
                        rawSD.DiscretionaryAcl :
                        new RawAcl(8, 1);

                    // Add access rules
                    // Current user
                    if (currentUser.User != null)
                    {
                        rawAcl.InsertAce(0, new CommonAce(
                            AceFlags.None,
                            AceQualifier.AccessAllowed,
                            (int)RegistryRights.FullControl,
                            currentUser.User,
                            false,
                            null));
                    }

                    // Administrators
                    rawAcl.InsertAce(0, new CommonAce(
                        AceFlags.None,
                        AceQualifier.AccessAllowed,
                        (int)RegistryRights.FullControl,
                        new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                        false,
                        null));

                    // SYSTEM
                    rawAcl.InsertAce(0, new CommonAce(
                        AceFlags.None,
                        AceQualifier.AccessAllowed,
                        (int)RegistryRights.FullControl,
                        new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                        false,
                        null));

                    // Set the DACL
                    rawSD.DiscretionaryAcl = rawAcl;

                    // Convert back to byte array
                    byte[] newSD = new byte[rawSD.BinaryLength];
                    rawSD.GetBinaryForm(newSD, 0);

                    // Set the new security descriptor
                    result = RegSetKeySecurity(hKey, OWNER_SECURITY_INFORMATION | DACL_SECURITY_INFORMATION, newSD);
                    if (result != ERROR_SUCCESS)
                    {
                        _logService.LogError($"Failed to set registry key security: {result}");
                        return false;
                    }

                    _logService.LogSuccess($"Successfully took ownership of registry key: {rootKey.Name}\\{subKeyPath}");
                    return true;
                }
                finally
                {
                    // Close the key
                    RegCloseKey(hKey);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error taking ownership of registry key: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Gets the native handle for a root registry key.
        /// </summary>
        private IntPtr GetRootKeyHandle(RegistryKey rootKey)
        {
            if (rootKey == Microsoft.Win32.Registry.CurrentUser)
                return HKEY_CURRENT_USER;
            else if (rootKey == Microsoft.Win32.Registry.LocalMachine)
                return HKEY_LOCAL_MACHINE;
            else if (rootKey == Microsoft.Win32.Registry.ClassesRoot)
                return HKEY_CLASSES_ROOT;
            else if (rootKey == Microsoft.Win32.Registry.Users)
                return HKEY_USERS;
            else if (rootKey == Microsoft.Win32.Registry.CurrentConfig)
                return HKEY_CURRENT_CONFIG;
            else
                return IntPtr.Zero;
        }

        private RegistryKey? EnsureKeyWithFullAccess(RegistryKey rootKey, string subKeyPath)
        {
            try
            {
                // Try to open existing key first
                RegistryKey? key = rootKey.OpenSubKey(subKeyPath, true);
                
                if (key != null)
                {
                    return key; // Key exists and we got write access
                }
                
                // Check if the key exists but we don't have access
                RegistryKey? readOnlyKey = rootKey.OpenSubKey(subKeyPath, false);
                if (readOnlyKey != null)
                {
                    // Key exists but we don't have write access, try to take ownership
                    readOnlyKey.Close();
                    _logService.LogInformation($"Registry key exists but we don't have write access: {rootKey.Name}\\{subKeyPath}");
                    
                    // Try to take ownership of the key
                    bool ownershipTaken = TakeOwnershipOfKey(rootKey, subKeyPath);
                    if (ownershipTaken)
                    {
                        // Try to open the key again with write access
                        key = rootKey.OpenSubKey(subKeyPath, true);
                        if (key != null)
                        {
                            _logService.LogSuccess($"Successfully opened registry key after taking ownership: {rootKey.Name}\\{subKeyPath}");
                            return key;
                        }
                    }
                    
                    _logService.LogWarning($"Failed to get write access to registry key even after taking ownership: {rootKey.Name}\\{subKeyPath}");
                }
                
                // Key doesn't exist or we couldn't get access - we need to create the entire path
                string[] parts = subKeyPath.Split('\\');
                string currentPath = "";
                
                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i];
                    
                    if (currentPath == "")
                    {
                        currentPath = part;
                    }
                    else
                    {
                        currentPath += "\\" + part;
                    }
                    
                    // Try to open this part of the path
                    key = rootKey.OpenSubKey(currentPath, true);
                    
                    if (key == null)
                    {
                        // This part doesn't exist, create it with full rights
                        RegistrySecurity security = new RegistrySecurity();
                        
                        // Get current user security identifier - handle null case
                        var currentUser = WindowsIdentity.GetCurrent().User;
                        if (currentUser != null)
                        {
                            // Add current user with full control
                            security.AddAccessRule(new RegistryAccessRule(
                                currentUser,
                                RegistryRights.FullControl,
                                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                                PropagationFlags.None,
                                AccessControlType.Allow));
                        }
                            
                        // Add Administrators with full control
                        security.AddAccessRule(new RegistryAccessRule(
                            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
                            RegistryRights.FullControl,
                            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                            PropagationFlags.None,
                            AccessControlType.Allow));
                            
                        // Add SYSTEM with full control
                        security.AddAccessRule(new RegistryAccessRule(
                            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                            RegistryRights.FullControl,
                            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                            PropagationFlags.None,
                            AccessControlType.Allow));
                        
                        // Create the key with explicit security settings
                        key = rootKey.CreateSubKey(currentPath, RegistryKeyPermissionCheck.ReadWriteSubTree, security);
                        
                        if (key == null)
                        {
                            _logService.LogError($"Failed to create registry key: {currentPath}");
                            return null;
                        }
                        
                        // Close intermediate keys to avoid leaks
                        if (i < parts.Length - 1)
                        {
                            key.Close();
                            key = null;
                        }
                    }
                    else if (i < parts.Length - 1)
                    {
                        // Close intermediate keys to avoid leaks
                        key.Close();
                        key = null;
                    }
                }
                
                // At this point, 'key' should be the full path key we wanted to create
                // If it's null, open the full path explicitly
                if (key == null)
                {
                    key = rootKey.OpenSubKey(subKeyPath, true);
                }
                
                return key;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error ensuring registry key with full access: {subKeyPath}", ex);
                return null;
            }
        }
    }
}
