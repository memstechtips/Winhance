using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Style;

// LocKey.Unchecked lets test fixtures name a string that is not in en.json. The compiler cannot express
// "internal, but not to us", so this gate keeps production code from calling it.
public class LocKeyIsSealedTests
{
    [Fact]
    public void NoProductionFile_ConstructsALocKeyFromARawString()
    {
        var src = Path.Combine(RepoPaths.SolutionDir(), "src");

        var offenders = Directory
            .EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => Path.GetFileName(f) != "LocKey.cs")
            .Where(f => File.ReadAllText(f).Contains("LocKey.Unchecked"))
            .Select(f => Path.GetRelativePath(src, f))
            .OrderBy(f => f)
            .ToList();

        offenders.Should().BeEmpty(
            "a key built from a raw string is exactly what the generator replaced; use the generated "
            + "member, or add the key to all 29 language files if it does not exist yet");
    }
}
