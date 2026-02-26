using FluentAssertions;
using Microsoft.Win32;
using Winhance.Infrastructure.Features.AdvancedTools.Helpers;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools;

public class PowerShellScriptUtilitiesTests
{
    // ---------------------------------------------------------------
    // SanitizeVariableName
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("power-plan", "power_plan")]
    [InlineData("no-hyphens-here", "no_hyphens_here")]
    [InlineData("already_clean", "already_clean")]
    [InlineData("", "")]
    public void SanitizeVariableName_ReplacesHyphensWithUnderscores(string input, string expected)
    {
        PowerShellScriptUtilities.SanitizeVariableName(input).Should().Be(expected);
    }

    // ---------------------------------------------------------------
    // EscapePowerShellString
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("hello", "hello")]
    [InlineData("it's", "it''s")]
    [InlineData("it's a 'test'", "it''s a ''test''")]
    public void EscapePowerShellString_EscapesSingleQuotes(string? input, string? expected)
    {
        PowerShellScriptUtilities.EscapePowerShellString(input).Should().Be(expected);
    }

    // ---------------------------------------------------------------
    // ConvertRegistryPath
    // ---------------------------------------------------------------

    [Theory]
    [InlineData("HKEY_CURRENT_USER\\Software\\Foo", "HKCU:\\Software\\Foo")]
    [InlineData("HKEY_LOCAL_MACHINE\\SYSTEM\\Bar", "HKLM:\\SYSTEM\\Bar")]
    [InlineData("HKEY_CLASSES_ROOT\\TypeLib", "HKCR:\\TypeLib")]
    [InlineData("HKEY_USERS\\.DEFAULT", "HKU:\\.DEFAULT")]
    [InlineData("SomeOtherPath", "SomeOtherPath")]
    public void ConvertRegistryPath_ConvertsHiveNames(string input, string expected)
    {
        PowerShellScriptUtilities.ConvertRegistryPath(input).Should().Be(expected);
    }

    // ---------------------------------------------------------------
    // ConvertToRegistryType
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(RegistryValueKind.DWord, "DWord")]
    [InlineData(RegistryValueKind.QWord, "QWord")]
    [InlineData(RegistryValueKind.String, "String")]
    [InlineData(RegistryValueKind.ExpandString, "ExpandString")]
    [InlineData(RegistryValueKind.Binary, "Binary")]
    [InlineData(RegistryValueKind.MultiString, "MultiString")]
    [InlineData(RegistryValueKind.None, "String")]
    public void ConvertToRegistryType_ReturnsExpectedString(RegistryValueKind kind, string expected)
    {
        PowerShellScriptUtilities.ConvertToRegistryType(kind).Should().Be(expected);
    }

    // ---------------------------------------------------------------
    // FormatValueForPowerShell
    // ---------------------------------------------------------------

    [Fact]
    public void FormatValueForPowerShell_NullValue_ReturnsDollarNull()
    {
        PowerShellScriptUtilities.FormatValueForPowerShell(null!, RegistryValueKind.DWord)
            .Should().Be("$null");
    }

    [Theory]
    [InlineData(RegistryValueKind.String, "hello", "'hello'")]
    [InlineData(RegistryValueKind.ExpandString, "%PATH%", "'%PATH%'")]
    public void FormatValueForPowerShell_StringTypes_WrapsInSingleQuotes(
        RegistryValueKind kind, string value, string expected)
    {
        PowerShellScriptUtilities.FormatValueForPowerShell(value, kind).Should().Be(expected);
    }

    [Theory]
    [InlineData(RegistryValueKind.DWord, 42, "42")]
    [InlineData(RegistryValueKind.QWord, 9999L, "9999")]
    public void FormatValueForPowerShell_NumericTypes_ReturnsToString(
        RegistryValueKind kind, object value, string expected)
    {
        PowerShellScriptUtilities.FormatValueForPowerShell(value, kind).Should().Be(expected);
    }

    [Fact]
    public void FormatValueForPowerShell_BinaryByteArray_ReturnsHexArrayLiteral()
    {
        var bytes = new byte[] { 0x0A, 0xFF, 0x00 };

        var result = PowerShellScriptUtilities.FormatValueForPowerShell(bytes, RegistryValueKind.Binary);

        result.Should().Be("@(0x0A,0xFF,0x00)");
    }

    [Fact]
    public void FormatValueForPowerShell_BinarySingleByte_ReturnsHexSingleElement()
    {
        byte value = 0xAB;

        var result = PowerShellScriptUtilities.FormatValueForPowerShell(value, RegistryValueKind.Binary);

        result.Should().Be("@(0xAB)");
    }

    [Fact]
    public void FormatValueForPowerShell_UnknownType_WrapsInSingleQuotes()
    {
        var result = PowerShellScriptUtilities.FormatValueForPowerShell("fallback", RegistryValueKind.None);

        result.Should().Be("'fallback'");
    }
}
