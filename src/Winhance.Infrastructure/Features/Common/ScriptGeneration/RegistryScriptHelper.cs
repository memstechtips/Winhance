using System;
using Microsoft.Win32;

namespace Winhance.Infrastructure.Features.Common.ScriptGeneration;

/// <summary>
/// Provides helper methods for working with registry scripts.
/// </summary>
public static class RegistryScriptHelper
{
    /// <summary>
    /// Converts a RegistryValueKind to the corresponding reg.exe type string.
    /// </summary>
    /// <param name="valueKind">The registry value kind.</param>
    /// <returns>The reg.exe type string.</returns>
    public static string GetRegTypeString(RegistryValueKind valueKind)
    {
        return valueKind switch
        {
            RegistryValueKind.String => "REG_SZ",
            RegistryValueKind.ExpandString => "REG_EXPAND_SZ",
            RegistryValueKind.Binary => "REG_BINARY",
            RegistryValueKind.DWord => "REG_DWORD",
            RegistryValueKind.MultiString => "REG_MULTI_SZ",
            RegistryValueKind.QWord => "REG_QWORD",
            _ => "REG_SZ"
        };
    }
}
