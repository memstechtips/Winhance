using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Core.Features.Common.Enums;
using LogLevel = Winhance.Core.Features.Common.Enums.LogLevel;

namespace Winhance.Infrastructure.Features.Common.Registry
{
    [SupportedOSPlatform("windows")]
    public partial class RegistryService
    {
        private RegistryKey? GetRootKey(string rootKeyName)
        {
            // Normalize the input by converting to uppercase
            rootKeyName = rootKeyName.ToUpper();
            
            // Check for full names first
            if (rootKeyName == "HKEY_LOCAL_MACHINE" || rootKeyName == "LOCALMACHINE")
                return Microsoft.Win32.Registry.LocalMachine;
            if (rootKeyName == "HKEY_CURRENT_USER" || rootKeyName == "CURRENTUSER")
                return Microsoft.Win32.Registry.CurrentUser;
            if (rootKeyName == "HKEY_CLASSES_ROOT")
                return Microsoft.Win32.Registry.ClassesRoot;
            if (rootKeyName == "HKEY_USERS")
                return Microsoft.Win32.Registry.Users;
            if (rootKeyName == "HKEY_CURRENT_CONFIG")
                return Microsoft.Win32.Registry.CurrentConfig;
            
            // Then check for abbreviated names using the extension method's constants
            if (rootKeyName == RegistryExtensions.GetRegistryHiveString(RegistryHive.LocalMachine))
                return Microsoft.Win32.Registry.LocalMachine;
            if (rootKeyName == RegistryExtensions.GetRegistryHiveString(RegistryHive.CurrentUser))
                return Microsoft.Win32.Registry.CurrentUser;
            if (rootKeyName == RegistryExtensions.GetRegistryHiveString(RegistryHive.ClassesRoot))
                return Microsoft.Win32.Registry.ClassesRoot;
            if (rootKeyName == RegistryExtensions.GetRegistryHiveString(RegistryHive.Users))
                return Microsoft.Win32.Registry.Users;
            if (rootKeyName == RegistryExtensions.GetRegistryHiveString(RegistryHive.CurrentConfig))
                return Microsoft.Win32.Registry.CurrentConfig;
            
            // If no match is found, return null
            return null;
        }
    }
}
