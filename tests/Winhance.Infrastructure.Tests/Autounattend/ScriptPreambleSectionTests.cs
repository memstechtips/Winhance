using System.Text;
using FluentAssertions;
using Winhance.Infrastructure.Features.Autounattend.ScriptSections;
using Xunit;

namespace Winhance.Infrastructure.Tests.Autounattend;

public class ScriptPreambleSectionTests
{
    private static string HelpersCalledBy(string script)
    {
        var sb = new StringBuilder();
        ScriptPreambleSection.AppendHelperFunctions(sb, script);
        return sb.ToString();
    }

    private static int DefinedFunctions(string output) => output.Split("\nfunction ").Length - 1;

    [Fact]
    public void AppendHeader_ContainsSynopsisBlock()
    {
        var sb = new StringBuilder();

        ScriptPreambleSection.AppendHeader(sb);

        var output = sb.ToString();
        output.Should().Contain(".SYNOPSIS");
        output.Should().Contain("Winhance");
    }

    [Fact]
    public void AppendHeader_ContainsParamBlock()
    {
        var sb = new StringBuilder();

        ScriptPreambleSection.AppendHeader(sb);

        var output = sb.ToString();
        output.Should().Contain("param(");
        output.Should().Contain("[switch]$UserCustomizations");
    }

    [Fact]
    public void AppendHeader_ContainsExamples()
    {
        var sb = new StringBuilder();

        ScriptPreambleSection.AppendHeader(sb);

        var output = sb.ToString();
        output.Should().Contain(".EXAMPLE");
        output.Should().Contain("Winhancements.ps1");
    }

    [Fact]
    public void AppendLoggingSetup_ContainsLogPath()
    {
        var sb = new StringBuilder();

        ScriptPreambleSection.AppendLoggingSetup(sb);

        var output = sb.ToString();
        output.Should().Contain("$LogPath");
        output.Should().Contain("Winhancements.txt");
    }

    [Fact]
    public void AppendLoggingSetup_ContainsWriteLogFunction()
    {
        var sb = new StringBuilder();

        ScriptPreambleSection.AppendLoggingSetup(sb);

        var output = sb.ToString();
        output.Should().Contain("function Write-Log");
        output.Should().Contain("[string]$Message");
    }

    [Fact]
    public void AppendLoggingSetup_ContainsModeDetection()
    {
        var sb = new StringBuilder();

        ScriptPreambleSection.AppendLoggingSetup(sb);

        var output = sb.ToString();
        output.Should().Contain("$UserCustomizations");
        output.Should().Contain("User Customizations Only");
        output.Should().Contain("System Customizations");
    }

    [Theory]
    [InlineData("Get-TargetUser")]
    [InlineData("Get-UserSID")]
    [InlineData("Set-RegistryValue")]
    [InlineData("Remove-RegistryValue")]
    [InlineData("Remove-RegistryKey")]
    [InlineData("New-RegistryKey")]
    [InlineData("Set-BinaryBit")]
    [InlineData("Set-BinaryByte")]
    [InlineData("Set-RegistryCompositeValue")]
    [InlineData("Set-RegistryStringFlag")]
    [InlineData("Open-RegistryKeyForAcl")]
    [InlineData("Unlock-RegistryKey")]
    [InlineData("Lock-RegistryKey")]
    [InlineData("Start-ProcessAsUser")]
    public void AppendHelperFunctions_ACallWritesThatHelperOnce(string name)
    {
        var output = HelpersCalledBy($"{name} -Path 'HKCU:\\Software\\Winhance' -Description 'test'");

        output.Split($"function {name} {{").Should().HaveCount(2, $"{name} must be defined exactly once");
    }

    // PowerShell ends a here-string only on a line whose first character is the terminator, and this helper
    // carries a whole C# file inside one.
    [Fact]
    public void AppendHelperFunctions_DesktopTypeHelper_ClosesItsHereStringAtColumnZero()
    {
        var output = HelpersCalledBy("Add-WinhanceDesktopType");

        output.Should().Contain("function Add-WinhanceDesktopType {")
            .And.Contain("namespace Winhance.Desktop")
            .And.Contain("\n'@")
            .And.NotContain("\n '@");
    }

    [Fact]
    public void AppendHelperFunctions_WritesOnlyTheHelpersTheScriptCalls()
    {
        var output = HelpersCalledBy("Set-RegistryValue -Path 'HKLM:\\SOFTWARE\\Winhance' -Name 'V' -Type 'DWord' -Value 1 -Description 'test'");

        output.Should().Contain("function Set-RegistryValue {");
        DefinedFunctions(output).Should().Be(1);
    }

    [Fact]
    public void AppendHelperFunctions_ALockCall_AlsoWritesTheHelperItCalls_InDeclaredOrder()
    {
        var output = HelpersCalledBy("Lock-RegistryKey -Path 'HKLM:\\SYSTEM\\Winhance' -Description 'test'");

        DefinedFunctions(output).Should().Be(2);
        output.IndexOf("function Open-RegistryKeyForAcl {", StringComparison.Ordinal)
            .Should().BeGreaterThanOrEqualTo(0)
            .And.BeLessThan(output.IndexOf("function Lock-RegistryKey {", StringComparison.Ordinal));
    }

    [Fact]
    public void AppendHelperFunctions_MatchesACallInAnyCase_ButNotInsideALongerName()
    {
        var output = HelpersCalledBy("unlock-registrykey -Path 'HKLM:\\SYSTEM\\Winhance' -Description 'test'");

        output.Should().Contain("function Unlock-RegistryKey {")
            .And.Contain("function Open-RegistryKeyForAcl {")
            .And.NotContain("function Lock-RegistryKey {");
    }

    [Theory]
    [InlineData("Set-RegistryValueX -Path 'HKLM:\\SOFTWARE\\Winhance'")]
    [InlineData("function Set-RegistryValue { param($Path) }")]
    [InlineData("Write-Log \"nothing to do\" \"INFO\"")]
    public void AppendHelperFunctions_WritesNothingForAScriptThatCallsNoHelper(string script)
    {
        HelpersCalledBy(script).Should().BeEmpty();
    }

    [Fact]
    public void AppendHelperFunctions_StartProcessAsUser_CarriesItsWin32Interop()
    {
        var output = HelpersCalledBy("$success = Start-ProcessAsUser -CommandLine $cmdLine");

        output.Should().Contain("DllImport")
            .And.Contain("advapi32.dll")
            .And.Contain("CreateProcessAsUserW")
            .And.Contain("WTSGetActiveConsoleSessionId");
    }

    [Fact]
    public void AppendCompletionBlock_ContainsCompletedMessage()
    {
        var sb = new StringBuilder();

        ScriptPreambleSection.AppendCompletionBlock(sb);

        var output = sb.ToString();
        output.Should().Contain("Script Completed");
        output.Should().Contain("SUCCESS");
    }

    [Fact]
    public void AppendCompletionBlock_ContainsSeparatorLines()
    {
        var sb = new StringBuilder();

        ScriptPreambleSection.AppendCompletionBlock(sb);

        sb.ToString().Should().Contain("================");
    }
}
