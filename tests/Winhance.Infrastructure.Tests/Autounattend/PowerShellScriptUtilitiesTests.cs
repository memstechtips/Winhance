using FluentAssertions;
using Microsoft.Win32;
using Winhance.Infrastructure.Features.Autounattend.Helpers;
using Xunit;

namespace Winhance.Infrastructure.Tests.Autounattend;

public class PowerShellScriptUtilitiesTests
{

    [Theory]
    [InlineData("power-plan", "power_plan")]
    [InlineData("no-hyphens-here", "no_hyphens_here")]
    [InlineData("already_clean", "already_clean")]
    [InlineData("", "")]
    public void SanitizeVariableName_ReplacesHyphensWithUnderscores(string input, string expected)
    {
        PowerShellScriptUtilities.SanitizeVariableName(input).Should().Be(expected);
    }

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

    [Theory]
    [InlineData("l\u2018a", "l\u2018\u2018a")]
    [InlineData("l\u2019Explorateur", "l\u2019\u2019Explorateur")]
    [InlineData("\u201Aa", "\u201A\u201Aa")]
    [InlineData("\u201Ba", "\u201B\u201Ba")]
    [InlineData("it''s", "it''''s")]
    [InlineData("'\u2019", "''\u2019\u2019")]
    [InlineData("\u201CShow\u201D \u201E \" $x `n", "\u201CShow\u201D \u201E \" $x `n")]
    [InlineData("line one\nline two", "line one\nline two")]
    public void EscapePowerShellString_DoublesEveryCharacterPowerShellReadsAsASingleQuote(string input, string expected)
    {
        PowerShellScriptUtilities.EscapePowerShellString(input).Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("hello", "hello")]
    [InlineData("say \"hi\"", "say `\"hi`\"")]
    [InlineData("\u201CShow all icons\u201D", "`\u201CShow all icons`\u201D")]
    [InlineData("\u201Eunten\u201C", "`\u201Eunten`\u201C")]
    [InlineData("\"\"", "`\"`\"")]
    [InlineData("$env:TEMP $(calc)", "`$env:TEMP `$(calc)")]
    [InlineData("tick ` tock", "tick `` tock")]
    [InlineData("it's \u2018a \u2019b \u201Ac \u201Bd", "it's \u2018a \u2019b \u201Ac \u201Bd")]
    [InlineData("line one\nline two", "line one\nline two")]
    public void EscapeForDoubleQuotedString_BackticksEveryCharacterPowerShellReadsAsLive(string? input, string expected)
    {
        PowerShellScriptUtilities.EscapeForDoubleQuotedString(input).Should().Be(expected);
    }

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

    [Fact]
    public void FormatValueForPowerShell_NullValue_ReturnsDollarNull()
    {
        PowerShellScriptUtilities.FormatValueForPowerShell(null!, RegistryValueKind.DWord)
            .Should().Be("$null");
    }

    [Theory]
    [InlineData(RegistryValueKind.String, "hello", "'hello'")]
    [InlineData(RegistryValueKind.ExpandString, "%PATH%", "'%PATH%'")]
    // The es-ES long date: Win32 date pictures keep a literal run in single quotes.
    [InlineData(RegistryValueKind.String, "dddd, d 'de' MMMM 'de' yyyy", "'dddd, d ''de'' MMMM ''de'' yyyy'")]
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

    [Theory]
    [InlineData(RegistryValueKind.DWord, "42", "42")]
    [InlineData(RegistryValueKind.DWord, "-1", "-1")]
    [InlineData(RegistryValueKind.QWord, " 9999 ", "9999")]
    public void FormatValueForPowerShell_NumericTypes_WholeNumberStringStaysBare(
        RegistryValueKind kind, string value, string expected)
    {
        PowerShellScriptUtilities.FormatValueForPowerShell(value, kind).Should().Be(expected);
    }

    [Theory]
    [InlineData(RegistryValueKind.DWord, "1; calc", "'1; calc'")]
    [InlineData(RegistryValueKind.QWord, "$(calc)", "'$(calc)'")]
    [InlineData(RegistryValueKind.DWord, "1'; calc; '", "'1''; calc; '''")]
    [InlineData(RegistryValueKind.DWord, "0x10", "'0x10'")]
    [InlineData(RegistryValueKind.DWord, true, "'True'")]
    public void FormatValueForPowerShell_NumericTypes_QuotesAnythingElse(
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
    public void FormatValueForPowerShell_BinaryEmptyByteArray_ReturnsTypedEmptyArray()
    {
        // Empty REG_BINARY (e.g. taskbar-clean's Favorites) must emit a typed empty
        // array so Set-ItemProperty -Type Binary writes a zero-length value, not "@()".
        var result = PowerShellScriptUtilities.FormatValueForPowerShell(Array.Empty<byte>(), RegistryValueKind.Binary);

        result.Should().Be("([byte[]]@())");
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
        PowerShellScriptUtilities.FormatValueForPowerShell("it's", RegistryValueKind.None).Should().Be("'it''s'");
    }
}
