using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Winhance.IntegrationTests.Helpers;
using Xunit;

namespace Winhance.IntegrationTests.Localization;

// A literal GetString("...") has no fallback at the call site, so a miss renders [Key]. Catalog keys are LocKey
// members generated from en.json, so a missing one does not compile and needs no test here.
public class LocalizationKeyReferenceTests
{
    private static readonly string SrcDir = Path.Combine(TestContext.SolutionDir, "src");

    private static readonly string EnJsonPath = Path.Combine(
        TestContext.SolutionDir, "src", "Winhance.Core", "Features", "Common", "Localization", "en.json");

    // Matches a localization key passed as the FIRST argument to any GetString(...) call, on any
    // receiver (_localization.GetString("X"), localizationService.GetString("X", arg), etc.).
    // Only pure string literals are captured; dynamically-built keys (GetString(someVar),
    // GetString($"Setting_{id}")) are intentionally skipped here.
    private static readonly Regex GetStringLiteral = new(
        @"GetString\(\s*""([^""]+)""", RegexOptions.Compiled);

    // A dialog title / message / button label written as a literal, or a literal message handed straight to
    // ShowMessage / ShowInformation/Warning/ErrorAsync. $"..." counts: an interpolated string is still English
    // around the holes.
    // "" is allowed - it means "use the localized default".
    private static readonly Regex HardcodedDialogText = new(
        @"\b(?:(?:Confirm|Cancel|Secondary|Close|Primary)?ButtonText|Title|Message|Content)\s*=\s*\$?""[^""]" +
        @"|\bShow(?:Message|(?:Information|Warning|Error|CustomContentDialog|TaskOutputDialog)Async)\(\s*\$?""[^""]",
        RegexOptions.Compiled);

    // Whole string literals, escapes included; the test flags any longer than the empty "".
    private static readonly Regex StringLiteral = new(@"""(?:[^""\\]|\\.)*""", RegexOptions.Compiled);

    // These two used to carry English defaults ("OK", "Cancel", "Error", ...) that every caller omitting the
    // argument silently shipped to 28 languages. DialogService supplies the localized default now.
    private static readonly string[] DialogContracts =
    [
        Path.Combine("Winhance.Core", "Features", "Common", "Interfaces", "IDialogService.cs"),
        Path.Combine("Winhance.Core", "Features", "Common", "Models", "ConfirmationRequest.cs"),
    ];

    private static HashSet<string> EnglishKeys()
    {
        var json = File.ReadAllText(EnJsonPath);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();
    }

    private static IEnumerable<string> AllCsFiles() =>
        Directory.EnumerateFiles(SrcDir, "*.cs", SearchOption.AllDirectories).Where(f => !IsBuildOutput(f));

    [Fact]
    public void StaticLiteralLocalizationKeys_MustExistInEnglish()
    {
        var enKeys = EnglishKeys();

        // key -> set of files referencing it (for actionable failure output)
        var references = new Dictionary<string, SortedSet<string>>();

        foreach (var file in AllCsFiles())
        {
            var text = File.ReadAllText(file);
            foreach (Match m in GetStringLiteral.Matches(text))
            {
                var key = m.Groups[1].Value;
                if (!references.TryGetValue(key, out var files))
                {
                    files = new SortedSet<string>();
                    references[key] = files;
                }
                files.Add(Path.GetRelativePath(SrcDir, file));
            }
        }

        var missing = references
            .Where(kvp => !enKeys.Contains(kvp.Key))
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => $"  \"{kvp.Key}\"  <- {string.Join(", ", kvp.Value)}")
            .ToList();

        missing.Should().BeEmpty(because:
            "every localization key passed as a literal to GetString(\"...\") has no call-site " +
            "fallback — a missing key renders the raw \"[Key]\" string in the UI. " +
            "Add these keys to en.json (and the other language files), or fix the typo'd call site:\n" +
            string.Join("\n", missing));
    }

    [Fact]
    public void DialogText_IsNeverAHardcodedLiteral()
    {
        var offenders = new List<string>();
        var scanned = 0;
        foreach (var file in AllCsFiles())
        {
            scanned++;
            // Whole-file text, not per line: the literal often sits on the line after the call.
            var text = File.ReadAllText(file);
            foreach (Match m in HardcodedDialogText.Matches(text))
            {
                var line = text.AsSpan(0, m.Index).Count('\n') + 1;
                var snippet = text.Substring(m.Index, Math.Min(90, text.Length - m.Index)).ReplaceLineEndings(" ").Trim();
                offenders.Add($"  {Path.GetRelativePath(SrcDir, file)}:{line}: {snippet}");
            }
        }

        scanned.Should().BeGreaterThan(100, because: "the scan must actually see the source tree");
        offenders.Should().BeEmpty(because:
            "dialog titles, messages and button labels must come from a localization key (GetString(\"...\")), " +
            "not an English literal - 28 of the 29 languages would see English. Pass \"\" (or omit the argument) " +
            "to get the localized default instead of writing OK / Cancel / Error yourself:\n" +
            string.Join("\n", offenders));
    }

    [Fact]
    public void DialogContracts_CarryNoStringLiterals()
    {
        var offenders = new List<string>();
        foreach (var relative in DialogContracts)
        {
            var file = Path.Combine(SrcDir, relative);
            File.Exists(file).Should().BeTrue(because: $"{relative} moved - update DialogContracts in this test");
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal)
                    && StringLiteral.Matches(lines[i]).Any(m => m.Length > 2))
                    offenders.Add($"  {relative}:{i + 1}: {lines[i].Trim()}");
            }
        }

        offenders.Should().BeEmpty(because:
            "a default parameter or property value on the dialog contracts is text every caller that omits it will " +
            "show verbatim in all 29 languages - keep them empty and let DialogService localize:\n" +
            string.Join("\n", offenders));
    }

    private static bool IsBuildOutput(string path) =>
        path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
        path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
}
