using System.Text;
using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Common.Utilities;
using Xunit;

namespace Winhance.IntegrationTests.ScriptGeneration;

// Runs the Edge script's Taskband splice under the real Windows PowerShell: a wrong offset there corrupts every pin
// on the taskbar, not just Edge's.
[Trait("Category", "Integration")]
public class EdgeTaskbarPinScriptTests
{
    private static byte[] Item(string name) => [7, 7, .. Encoding.Unicode.GetBytes(name), 0, 0];

    private static byte[] Link(string name) => [9, .. Encoding.Unicode.GetBytes(name), 9, 9];

    private static byte[] Sized(byte[] body) => [.. BitConverter.GetBytes((uint)body.Length), .. body];

    // Favorites: 0x00 + DWORD size + item per pin, then 0xFF. FavoritesResolve: DWORD size + shell link per pin.
    private static byte[] Favorites(params string[] names) => [.. names.SelectMany(n => Sized(Item(n)).Prepend((byte)0)), 0xFF];

    private static byte[] Resolve(params string[] names) => [.. names.SelectMany(n => Sized(Link(n)))];

    private static async Task<string> SpliceAsync(byte[] favorites, byte[] resolve)
    {
        var script = EdgeRemovalScript.GetScript();
        var start = script.IndexOf("function Remove-EdgePinEntry", StringComparison.Ordinal);
        var run = new StringBuilder(script[start..(script.IndexOf("\n}", start, StringComparison.Ordinal) + 2)])
            .AppendLine()
            .AppendLine($"$pins = Remove-EdgePinEntry ([Convert]::FromBase64String('{Convert.ToBase64String(favorites)}')) ([Convert]::FromBase64String('{Convert.ToBase64String(resolve)}'))")
            .AppendLine("if ($pins) { 'F=' + [Convert]::ToBase64String($pins.Favorites); 'R=' + [Convert]::ToBase64String($pins.Resolve) } else { 'NONE' }");

        return await new PowerShellRunner(new FileSystemService()).RunScriptAsync(run.ToString());
    }

    [Fact]
    public async Task RemoveEdgePinEntry_EdgeBetweenTwoPins_KeepsTheOthersByteForByte()
    {
        var output = await SpliceAsync(
            Favorites("File Explorer.lnk", "Microsoft Edge.lnk", "Notepad.lnk"),
            Resolve("File Explorer.lnk", "Microsoft Edge.lnk", "Notepad.lnk"));

        output.Should().Contain("F=" + Convert.ToBase64String(Favorites("File Explorer.lnk", "Notepad.lnk")));
        output.Should().Contain("R=" + Convert.ToBase64String(Resolve("File Explorer.lnk", "Notepad.lnk")));
    }

    [Fact]
    public async Task RemoveEdgePinEntry_NoEdgePin_ChangesNothing()
    {
        var output = await SpliceAsync(Favorites("File Explorer.lnk", "Notepad.lnk"), Resolve("File Explorer.lnk", "Notepad.lnk"));

        output.Should().Contain("NONE");
    }

    [Fact]
    public async Task RemoveEdgePinEntry_ListsOfDifferentLength_ChangesNothing()
    {
        var output = await SpliceAsync(
            Favorites("File Explorer.lnk", "Microsoft Edge.lnk", "Notepad.lnk"),
            Resolve("File Explorer.lnk", "Microsoft Edge.lnk"));

        output.Should().Contain("NONE");
    }
}
