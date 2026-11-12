using FluentAssertions;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.Autounattend;
using Winhance.Infrastructure.Tests.Catalog;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.Autounattend;

[Collection(RepoFileWritersCollection.Name)]
public class AnswerFileEquivalenceTests
{
    private const string Script = "Write-Host 'golden'";

    private static readonly string FixtureDir = Path.Combine(
        RepoPaths.SolutionDir(), "tests", "Winhance.Infrastructure.Tests", "Autounattend", "Fixtures", "answer-file");

    // Names, not scenarios: the record is internal, and a public theory method taking it is CS0051.
    public static IEnumerable<object[]> ScenarioNames() =>
        AnswerFileScenarios.All.Select(scenario => new object[] { scenario.Name });

    [Fact]
    public void Fixtures_EveryFixtureOnDisk_HasAScenario()
    {
        var files = Directory.GetFiles(FixtureDir, "*.xml").Select(Path.GetFileNameWithoutExtension);

        files.Should().BeEquivalentTo(AnswerFileScenarios.All.Select(scenario => scenario.Name));
    }

    [Theory]
    [MemberData(nameof(ScenarioNames))]
    public void Serialize_EachScenario_RendersTheAnswerFileItsFixtureHolds(string name)
    {
        var scenario = AnswerFileScenarios.All.Single(candidate => candidate.Name == name);
        var expected = Fixture(name);
        var actual = Rendered(scenario.Selections);

        actual.Should().Be(expected, $"'{name}' renders its captured answer file. "
            + TextDiff.FirstDifference(expected, actual, "fixture", "rendered"));
    }

    [Fact]
    public void Serialize_EmptySelectionSet_RendersTheDefaultsFixture()
    {
        var expected = Fixture("defaults");
        var actual = Rendered(SelectionSet.Empty);

        actual.Should().Be(expected, "an empty set renders the default answer file. "
            + TextDiff.FirstDifference(expected, actual, "fixture", "rendered"));
    }

    private static string Rendered(SelectionSet set) =>
        TextDiff.NormalizeLineEndings(AutounattendDocumentBuilder.Serialize(
            AutounattendDocumentBuilder.Build(new AutounattendRenderContext(Script, "0.0.0", set))));

    private static string Fixture(string name) =>
        TextDiff.NormalizeLineEndings(File.ReadAllText(Path.Combine(FixtureDir, name + ".xml")));
}
