using System.Xml.Linq;
using FluentAssertions;
using Winhance.Infrastructure.Features.Autounattend;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.Autounattend;

// Stripped before comparing: NetworkLocation (Microsoft deprecated it, the assembly does not write it), the
// Generator marker the assembly adds, and the extractor script, which changes with the app.
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

        var defaults = AnswerFileScenarios.All.Single(scenario => scenario.Name == "defaults").Selections;
        var fresh = XDocument.Parse(AutounattendDocumentBuilder.Serialize(
            AutounattendDocumentBuilder.Build(new AutounattendRenderContext(Script, "0.0.0", defaults))));
        fresh.Descendants().Where(e => e.Name.LocalName is "Generator" or "ExtractScript").Remove();

        var expected = Canonical(old.Root!).ToString();
        var actual = Canonical(fresh.Root!).ToString();
        actual.Should().Be(expected, TextDiff.FirstDifference(expected, actual, "template", "assembled"));
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
