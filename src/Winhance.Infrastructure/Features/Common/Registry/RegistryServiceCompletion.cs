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
            return rootKeyName.ToUpper() switch
            {
                "HKEY_LOCAL_MACHINE" or "HKLM" or "LOCALMACHINE" => Microsoft.Win32.Registry.LocalMachine,
                "HKEY_CURRENT_USER" or "HKCU" => Microsoft.Win32.Registry.CurrentUser,
                "HKEY_CLASSES_ROOT" or "HKCR" => Microsoft.Win32.Registry.ClassesRoot,
                "HKEY_USERS" or "HKU" => Microsoft.Win32.Registry.Users,
                "HKEY_CURRENT_CONFIG" or "HKCC" => Microsoft.Win32.Registry.CurrentConfig,
                _ => null
            };
        }
    }
}
