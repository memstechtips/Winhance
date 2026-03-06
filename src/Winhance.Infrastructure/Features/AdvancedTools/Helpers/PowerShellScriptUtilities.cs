using System.Linq;
using Microsoft.Win32;

namespace Winhance.Infrastructure.Features.AdvancedTools.Helpers;

/// <summary>
/// Pure utility methods for PowerShell script generation.
/// </summary>
internal static class PowerShellScriptUtilities
{
    public static string SanitizeVariableName(string name)
    {
        return name.Replace("-", "_");
    }

    public static string? EscapePowerShellString(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return input.Replace("'", "''");
    }

    public static string ConvertRegistryPath(string registryPath)
    {
        return registryPath
            .Replace("HKEY_CURRENT_USER\\", "HKCU:\\")
            .Replace("HKEY_LOCAL_MACHINE\\", "HKLM:\\")
            .Replace("HKEY_CLASSES_ROOT\\", "HKCR:\\")
            .Replace("HKEY_USERS\\", "HKU:\\");
    }

    public static string ConvertToRegistryType(RegistryValueKind valueType)
    {
        return valueType switch
        {
            RegistryValueKind.DWord => "DWord",
            RegistryValueKind.QWord => "QWord",
            RegistryValueKind.String => "String",
            RegistryValueKind.ExpandString => "ExpandString",
            RegistryValueKind.Binary => "Binary",
            RegistryValueKind.MultiString => "MultiString",
            _ => "String"
        };
    }

    public static string FormatValueForPowerShell(object value, RegistryValueKind valueType)
    {
        if (value == null) return "$null";

        return valueType switch
        {
            RegistryValueKind.String or RegistryValueKind.ExpandString => $"'{value}'",
            RegistryValueKind.DWord or RegistryValueKind.QWord => value.ToString()!,
            RegistryValueKind.Binary when value is byte[] byteArray => $"@({string.Join(",", byteArray.Select(b => $"0x{b:X2}"))})",
            RegistryValueKind.Binary => $"@(0x{Convert.ToByte(value):X2})",
            _ => $"'{value}'"
        };
    }
}
