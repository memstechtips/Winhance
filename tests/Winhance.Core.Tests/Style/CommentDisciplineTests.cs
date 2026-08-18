using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Style;

// Winhance carries no XML documentation (the PowerToys model, decided 2026-08-18): the code is
// self-documenting through naming, and a comment is a `//` note that says something the code cannot.
// Nothing in the compiler enforces that, so this does - a `///` block, a `#region` or a `/* */`
// comment anywhere under src/ or tests/ fails the build.
public class CommentDisciplineTests
{
    private static IEnumerable<string> SourceFiles() =>
        new[] { "src", "tests" }
            .SelectMany(root => Directory.EnumerateFiles(
                Path.Combine(RepoPaths.SolutionDir(), root), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static List<string> Offenders(Func<string, bool> lineIsOffender)
    {
        var offenders = new List<string>();
        foreach (var file in SourceFiles())
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lineIsOffender(lines[i].TrimStart()))
                    offenders.Add($"{Path.GetRelativePath(RepoPaths.SolutionDir(), file)}:{i + 1}");
            }
        }
        return offenders;
    }

    [Fact]
    public void NoSourceFile_CarriesXmlDocComments()
    {
        Offenders(l => l.StartsWith("///", StringComparison.Ordinal))
            .Should().BeEmpty("XML doc comments are not used in this codebase; say it as a // note or not at all");
    }

    [Fact]
    public void NoSourceFile_CarriesRegions()
    {
        Offenders(l => l.StartsWith("#region", StringComparison.Ordinal) || l.StartsWith("#endregion", StringComparison.Ordinal))
            .Should().BeEmpty("#region hides code instead of organising it");
    }

    [Fact]
    public void NoSourceFile_CarriesBlockComments()
    {
        Offenders(l => l.StartsWith("/*", StringComparison.Ordinal))
            .Should().BeEmpty("comments are // lines only");
    }
}
