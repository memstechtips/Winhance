using System.Globalization;
using System.Text;
using Microsoft.Win32;

namespace Winhance.Infrastructure.Features.Autounattend.Helpers;

internal static class PowerShellScriptUtilities
{
    public static string SanitizeVariableName(string name)
    {
        return name.Replace("-", "_");
    }

    // PowerShell ends a string on the typographic quotes as well as the ASCII one (language spec 2.3.5.2), and the
    // locale files are full of them. Each is doubled as itself.
    public static string? EscapePowerShellString(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var escaped = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            escaped.Append(c);
            if (c is '\'' or '\u2018' or '\u2019' or '\u201A' or '\u201B')
                escaped.Append(c);
        }

        return escaped.ToString();
    }

    public static string EscapeForDoubleQuotedString(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        var escaped = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            if (c is '`' or '$' or '"' or '\u201C' or '\u201D' or '\u201E')
                escaped.Append('`');
            escaped.Append(c);
        }

        return escaped.ToString();
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
            RegistryValueKind.String or RegistryValueKind.ExpandString =>
                $"'{EscapePowerShellString(value.ToString())}'",
            RegistryValueKind.DWord or RegistryValueKind.QWord => FormatWholeNumber(value),
            RegistryValueKind.Binary when value is byte[] byteArray =>
                byteArray.Length == 0
                    ? "([byte[]]@())"
                    : $"@({string.Join(",", byteArray.Select(b => $"0x{b:X2}"))})",
            RegistryValueKind.Binary => $"@(0x{Convert.ToByte(value):X2})",
            _ => $"'{EscapePowerShellString(value.ToString())}'"
        };
    }

    // A .winhance file can put any JSON value on a DWord target, and a bare value is code; anything that is
    // not a whole number is quoted.
    private static string FormatWholeNumber(object value) => value switch
    {
        sbyte or byte or short or ushort or int or uint or long or ulong =>
            Convert.ToString(value, CultureInfo.InvariantCulture)!,
        string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) =>
            number.ToString(CultureInfo.InvariantCulture),
        _ => $"'{EscapePowerShellString(value.ToString())}'",
    };
}
