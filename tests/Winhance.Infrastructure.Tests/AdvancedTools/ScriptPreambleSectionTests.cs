using System.Text;
using FluentAssertions;
using Winhance.Infrastructure.Features.AdvancedTools.ScriptSections;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools;

public class ScriptPreambleSectionTests
{
    // ---------------------------------------------------------------
    // AppendHeader
    // ---------------------------------------------------------------

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

    // ---------------------------------------------------------------
    // AppendLoggingSetup
    // ---------------------------------------------------------------

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

    // ---------------------------------------------------------------
    // AppendHelperFunctions
    // ---------------------------------------------------------------

    [Fact]
    public void AppendHelperFunctions_ContainsGetTargetUser()
    {
        var sb = new StringBuilder();

        ScriptPreambleSection.AppendHelperFunctions(sb);

        var output = sb.ToString();
        output.Should().Contain("function Get-TargetUser");
    }

    [Fact]
    public void AppendHelperFunctions_ContainsGetUserSID()
    {
        var sb = new StringBuilder();

        ScriptPreambleSection.AppendHelperFunctions(sb);

        sb.ToString().Should().Contain("function Get-UserSID");
    }

    [Fact]
    public void AppendHelperFunctions_ContainsSetRegistryValue()
    {
        var sb = new StringBuilder();

        ScriptPreambleSection.AppendHelperFunctions(sb);

        sb.ToString().Should().Contain("function Set-RegistryValue");
    }

    [Fact]
    public void AppendHelperFunctions_ContainsRemoveRegistryValue()
    {
        var sb = new StringBuilder();

        ScriptPreambleSection.AppendHelperFunctions(sb);

        sb.ToString().Should().Contain("function Remove-RegistryValue");
    }

    [Fact]
    public void AppendHelperFunctions_ContainsRemoveRegistryKey()
    {
        var sb = new StringBuilder();

        ScriptPreambleSection.AppendHelperFunctions(sb);

        sb.ToString().Should().Contain("function Remove-RegistryKey");
    }

    [Fact]
    public void AppendHelperFunctions_ContainsNewRegistryKey()
    {
        var sb = new StringBuilder();

        ScriptPreambleSection.AppendHelperFunctions(sb);

        sb.ToString().Should().Contain("function New-RegistryKey");
    }

    [Fact]
    public void AppendHelperFunctions_ContainsSetBinaryBit()
    {
        var sb = new StringBuilder();

        ScriptPreambleSection.AppendHelperFunctions(sb);

        sb.ToString().Should().Contain("function Set-BinaryBit");
    }

    [Fact]
    public void AppendHelperFunctions_ContainsSetBinaryByte()
    {
        var sb = new StringBuilder();

        ScriptPreambleSection.AppendHelperFunctions(sb);

        sb.ToString().Should().Contain("function Set-BinaryByte");
    }

    [Fact]
    public void AppendHelperFunctions_ContainsStartProcessAsUser()
    {
        var sb = new StringBuilder();

        ScriptPreambleSection.AppendHelperFunctions(sb);

        sb.ToString().Should().Contain("function Start-ProcessAsUser");
    }

    // ---------------------------------------------------------------
    // AppendStartProcessAsUser
    // ---------------------------------------------------------------

    [Fact]
    public void AppendStartProcessAsUser_ContainsWin32Interop()
    {
        var sb = new StringBuilder();

        ScriptPreambleSection.AppendStartProcessAsUser(sb);

        var output = sb.ToString();
        output.Should().Contain("DllImport");
        output.Should().Contain("advapi32.dll");
        output.Should().Contain("CreateProcessAsUserW");
    }

    [Fact]
    public void AppendStartProcessAsUser_ContainsSessionDetection()
    {
        var sb = new StringBuilder();

        ScriptPreambleSection.AppendStartProcessAsUser(sb);

        sb.ToString().Should().Contain("WTSGetActiveConsoleSessionId");
    }

    // ---------------------------------------------------------------
    // AppendCompletionBlock
    // ---------------------------------------------------------------

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

    // ---------------------------------------------------------------
    // Integration: all preamble sections together
    // ---------------------------------------------------------------

    [Fact]
    public void AllPreambleSections_CombinedOutput_IsNonEmpty()
    {
        var sb = new StringBuilder();

        ScriptPreambleSection.AppendHeader(sb);
        ScriptPreambleSection.AppendLoggingSetup(sb);
        ScriptPreambleSection.AppendHelperFunctions(sb);

        sb.Length.Should().BeGreaterThan(0);
        sb.ToString().Should().Contain("param(");
        sb.ToString().Should().Contain("function Write-Log");
        sb.ToString().Should().Contain("function Set-RegistryValue");
    }
}
