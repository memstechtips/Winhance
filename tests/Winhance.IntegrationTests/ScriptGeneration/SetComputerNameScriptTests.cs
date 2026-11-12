using FluentAssertions;
using Moq;
using Winhance.Core.Features.Autounattend;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Autounattend;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Common.Utilities;
using Xunit;

namespace Winhance.IntegrationTests.ScriptGeneration;

// Runs the shipped rename script's name logic on Windows PowerShell with the two CIM queries faked, cut off before the
// loop that writes the registry, and checks the name against UniqueComputerName, which the settings preview uses.
[Trait("Category", "Integration")]
public class SetComputerNameScriptTests
{
    private static async Task<string> NameFromScriptAsync(string? typed, string? serial, string? mac)
    {
        var script = AutounattendScripts.SetComputerName(typed).ReplaceLineEndings("\n");
        var logic = script[..script.IndexOf("while ($true)", StringComparison.Ordinal)];
        var fake = "function Get-CimInstance { param($ClassName, $Filter)\n" +
                   $"  if ($ClassName -eq 'Win32_BIOS') {{ [pscustomobject]@{{ SerialNumber = '{serial}' }} }}\n" +
                   $"  else {{ [pscustomobject]@{{ MACAddress = '{mac}'; Index = 0 }} }} }}\n";

        var output = await new PowerShellRunner(new FileSystemService(Mock.Of<ILogService>())).RunScriptAsync(fake + logic + "\nWrite-Output $name\n");
        return output.Trim().Split('\n')[^1].Trim();
    }

    [Theory]
    [InlineData("OFFICE", "PF3ABCDE", "")]
    [InlineData("", "5cg-12 34x", "")]
    [InlineData("My PC!", "To Be Filled By O.E.M.", "00:15:5D:01:7F:3A")]
    [InlineData("WORKSTATION-01", "00000000", "00-15-5d-01-7f-3a")]
    [InlineData("LAB", "123456789", "00155D017F3A")]
    [InlineData("LAB", "", "00:15:5d:01:7f:3a")]
    public async Task Name_ForTheSerialAndCard_MatchesTheCSharpRules(string typed, string serial, string mac)
    {
        var expected = UniqueComputerName.Name(typed, UniqueComputerName.Ending(serial, mac)!);

        (await NameFromScriptAsync(typed, serial, mac)).Should().Be(expected);
    }

    [Fact]
    public async Task Name_WithNoSerialOrCard_EndsInFourRandomHexCharacters()
    {
        UniqueComputerName.Ending("NONE", "12").Should().BeNull();

        (await NameFromScriptAsync("LAB", "NONE", "12")).Should().MatchRegex("^LAB-[0-9A-F]{4}$");
    }
}
