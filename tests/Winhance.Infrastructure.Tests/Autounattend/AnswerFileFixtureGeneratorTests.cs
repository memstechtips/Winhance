using Winhance.Infrastructure.Features.Autounattend;
using Winhance.Infrastructure.Tests.Catalog;
using Winhance.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Autounattend;

// A no-op under the gate: the fixtures are rewritten only while Fixtures/answer-file/REGENERATE exists.
// Run: touch the marker, then winhance-harness AnswerFileFixtureGenerator, then read the git diff of the fixtures.
[Collection(RepoFileWritersCollection.Name)]
public class AnswerFileFixtureGeneratorTests
{
    private const string Script = "Write-Host 'golden'";

    private static readonly string FixtureDir = Path.Combine(
        RepoPaths.SolutionDir(), "tests", "Winhance.Infrastructure.Tests", "Autounattend", "Fixtures", "answer-file");

    private readonly ITestOutputHelper _output;

    public AnswerFileFixtureGeneratorTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Regenerate_fixtures_when_the_marker_is_present()
    {
        var marker = Path.Combine(FixtureDir, "REGENERATE");
        if (!File.Exists(marker))
        {
            _output.WriteLine("No REGENERATE marker; fixtures left as they are.");
            return;
        }

        foreach (var scenario in AnswerFileScenarios.All)
        {
            var rendered = AutounattendDocumentBuilder.Serialize(
                AutounattendDocumentBuilder.Build(new AutounattendRenderContext(Script, "0.0.0", scenario.Selections)));
            File.WriteAllText(Path.Combine(FixtureDir, scenario.Name + ".xml"), rendered);
            _output.WriteLine($"wrote {scenario.Name}.xml");
        }

        File.Delete(marker);
    }
}
