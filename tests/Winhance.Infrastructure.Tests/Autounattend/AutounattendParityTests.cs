using System.Globalization;
using System.Xml.Linq;
using FluentAssertions;
using Winhance.Infrastructure.Features.Autounattend;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.Autounattend;

// Stripped before comparing: NetworkLocation (Microsoft deprecated it, the assembly does not write it), the
// template's empty passes (Setup reads only passes that carry settings), the Generator marker the assembly adds,
// the extractor script, which changes with the app, and the two adapter commands. Both sides emit them, but
// autounattend-oobe-network writes them so all three account modes read one decision, which puts them at another
// position in the list; AutounattendWritersTests pins that position.
public class AutounattendParityTests
{
    private const string Script = "Write-Host 'parity'";

    private static readonly string FixturePath = Path.Combine(
        RepoPaths.SolutionDir(), "tests", "Winhance.Infrastructure.Tests", "Autounattend", "Fixtures", "autounattend-template.xml");

    [Fact]
    public void The_default_selections_reproduce_the_retired_template()
    {
        var template = File.ReadAllText(FixturePath).Replace("<!--SCRIPT_PLACEHOLDER-->", "<![CDATA[" + Script + "]]>", StringComparison.Ordinal);
        var old = XDocument.Parse(template);
        old.Descendants().Where(e => e.Name.LocalName is "NetworkLocation" or "ExtractScript").Remove();
        StripAdapters(old);
        old.Root!.Elements().Where(e => e.Name.LocalName == "settings" && !e.HasElements).Remove();

        var defaults = AnswerFileScenarios.All.Single(scenario => scenario.Name == "defaults").Selections;
        var fresh = XDocument.Parse(AutounattendDocumentBuilder.Serialize(
            AutounattendDocumentBuilder.Build(new AutounattendRenderContext(Script, "0.0.0", defaults))));
        fresh.Descendants().Where(e => e.Name.LocalName is "Generator" or "ExtractScript").Remove();
        StripAdapters(fresh);

        var expected = Canonical(old.Root!).ToString();
        var actual = Canonical(fresh.Root!).ToString();
        actual.Should().Be(expected, TextDiff.FirstDifference(expected, actual, "template", "assembled"));
    }

    // Drops the two adapter commands and renumbers what is left, because Order is a position in the list: the
    // template had the cut at 3 and NetFx3 at 4, the assembly has them the other way round, and leaving the
    // holes would fail the comparison on the numbering rather than on the content this test is about.
    private static void StripAdapters(XDocument doc)
    {
        var lists = doc.Descendants()
            .Where(e => e.Name.LocalName is "RunSynchronous" or "FirstLogonCommands")
            .ToList();

        foreach (var list in lists)
        {
            list.Elements()
                .Where(c => c.Elements().Any(v => v.Value.Contains("-NetAdapter", StringComparison.Ordinal)))
                .Remove();

            int order = 1;
            foreach (var command in list.Elements())
                if (command.Element(command.Name.Namespace + "Order") is { } number)
                    number.Value = (order++).ToString(CultureInfo.InvariantCulture);
        }
    }

    private static XElement Canonical(XElement element) =>
        new(element.Name,
            element.Attributes().Where(a => !a.IsNamespaceDeclaration).OrderBy(a => a.Name.ToString(), StringComparer.Ordinal),
            element.Nodes().Select(Canonical).Where(n => n is not null));

    private static XNode? Canonical(XNode node) => node switch
    {
        XElement e => Canonical(e),
        XCData c => new XCData(Normalize(c.Value)),
        XText t when Normalize(t.Value).Length > 0 => new XText(Normalize(t.Value)),
        _ => null,
    };

    private static string Normalize(string text) => TextDiff.NormalizeLineEndings(text).Trim();
}
