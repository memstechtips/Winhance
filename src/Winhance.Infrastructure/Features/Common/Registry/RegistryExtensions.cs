using System;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Registry
{
    /// <summary>
    /// Extension methods for registry operations.
    /// </summary>
    public static class RegistryExtensions
    {
        /// <summary>
        /// Converts a RegistryHive enum to its string representation (HKCU, HKLM, etc.)
        /// </summary>
        /// <param name="hive">The registry hive.</param>
        /// <returns>The string representation of the registry hive.</returns>
        public static string GetRegistryHiveString(this RegistryHive hive)
        {
            return hive switch
            {
                RegistryHive.LocalMachine => "HKLM",
                RegistryHive.CurrentUser => "HKCU",
                RegistryHive.ClassesRoot => "HKCR",
                RegistryHive.Users => "HKU",
                RegistryHive.CurrentConfig => "HKCC",
                _ => throw new ArgumentException($"Unsupported registry hive: {hive}")
            };
        }

        /// <summary>
        /// Gets the full registry path by combining the hive and subkey.
        /// </summary>
        /// <param name="hive">The registry hive.</param>
        /// <param name="subKey">The registry subkey.</param>
        /// <returns>The full registry path.</returns>
        public static string GetFullRegistryPath(this RegistryHive hive, string subKey)
        {
            return $"{hive.GetRegistryHiveString()}\\{subKey}";
        }
    }
}
