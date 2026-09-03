using System.Xml.Linq;
using FluentAssertions;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Common.Utilities;
using Xunit;

namespace Winhance.IntegrationTests.ScriptGeneration;

// Runs the REAL Windows PowerShell parser over every piece of PowerShell that ships inside an
// answer file: the driver-install script the File element carries, the template's extractor
// script and every -Command payload already in the template. A brace
// lost in any of them is invisible to the compiler, the XML validator and substring assertions
// alike.
[Trait("Category", "Integration")]
public class GeneratedDriverInstallTests
{
    private static readonly XNamespace U = "urn:schemas-microsoft-com:unattend";
    private static readonly XNamespace X = "urn:winhance:unattend";

    private static PowerShellRunner Runner() => new(new FileSystemService());

    private static string? InlinePayload(string command)
    {
        const string markerText = "-Command \"";
        var index = command.IndexOf(markerText, StringComparison.Ordinal);
        if (index < 0)
            return null;

        var payload = command[(index + markerText.Length)..];
        payload.Should().EndWith("\"", because: $"an inline command must close its -Command quote: {command}");
        return payload[..^1];
    }

    // The bytes that reach the target: XML parsing turns the CDATA's CRLF into LF, and the
    // extractor writes InnerText.Trim().
    [Fact]
    public async Task InstallScript_ParsesUnderWindowsPowerShell()
    {
        await Runner().ValidateScriptSyntaxAsync(DriverInstallStepWriter.InstallScript.ReplaceLineEndings("\n").Trim());
    }

    [Fact]
    public async Task TemplateExtractScript_ParsesUnderWindowsPowerShell()
    {
        var doc = XDocument.Parse(AutounattendWriter.LoadTemplate());
        var script = doc.Root!.Element(X + "Extensions")!.Element(X + "ExtractScript")!.Value;

        script.Should().Contain("$Document.unattend.Extensions.File");
        await Runner().ValidateScriptSyntaxAsync(script);
    }

    [Fact]
    public async Task TemplateInlineCommands_ParseUnderWindowsPowerShell()
    {
        var doc = XDocument.Parse(AutounattendWriter.LoadTemplate());
        var payloads = doc.Descendants(U + "Path").Select(p => p.Value)
            .Concat(doc.Descendants(U + "CommandLine").Select(c => c.Value))
            .Select(InlinePayload)
            .Where(p => p is not null)
            .Distinct()
            .ToList();

        payloads.Should().HaveCountGreaterThan(2);

        var runner = Runner();
        foreach (var payload in payloads)
            await runner.ValidateScriptSyntaxAsync(payload!);
    }
}
