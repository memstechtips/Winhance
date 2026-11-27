using System.Text.RegularExpressions;
using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Style;

// The log service stamps every line with the calling type, so a message that opens with its own
// "[Tag]" would print the source twice and drift at the next rename. The compiler cannot see a
// string prefix, so this does.
public class LogTagDisciplineTests
{
    private static readonly Regex TaggedLogCall =
        new(@"\.Log(?:Information|Warning|Error|Debug)?\(\s*(?:[\w.]*LogLevel\.\w+,\s*)?\$?""\[", RegexOptions.Compiled);

    [Fact]
    public void NoLogCall_OpensItsMessageWithAHandWrittenTag()
    {
        var solutionDir = RepoPaths.SolutionDir();
        var offenders = Directory.EnumerateFiles(Path.Combine(solutionDir, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Select(f => (File: f, Text: File.ReadAllText(f)))
            .SelectMany(x => TaggedLogCall.Matches(x.Text).Select(m =>
                $"{Path.GetRelativePath(solutionDir, x.File)}:{x.Text[..m.Index].Count(c => c == '\n') + 1}"))
            .ToList();

        offenders.Should().BeEmpty("the log service already stamps the calling type; say the rest in words");
    }
}
