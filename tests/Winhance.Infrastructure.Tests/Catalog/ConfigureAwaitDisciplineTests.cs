using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

// The apply engine writes through a SYNCHRONOUS port and a few boundaries bridge to Task-returning services
// with GetAwaiter().GetResult(); those are safe only because every await beneath them uses
// ConfigureAwait(false). Drop that anywhere on the path and a bridge becomes a hard UI freeze; nothing in the
// compiler enforces it, so this does.
public class ConfigureAwaitDisciplineTests
{
    // Scoped to the apply/detection engine, not the whole solution: WinRT bitmap APIs await
    // IAsyncOperation (needs an .AsTask() hop first) and LiveSettingWriteStrategy awaits dialogs, where
    // resuming on the UI thread is correct. Allowlisting those instead would swallow new violations in
    // a listed file.
    //
    // Optimize/Services is in scope because IStateWriter's one remaining GetAwaiter().GetResult() blocks
    // on PowerPlanActivationService, which lives there.
    private static readonly string[] ScopedDirectories =
    {
        Path.Combine("src", "Winhance.Core", "Features", "Common", "Catalog"),
        Path.Combine("src", "Winhance.Infrastructure", "Features", "Common"),
        Path.Combine("src", "Winhance.Infrastructure", "Features", "Optimize", "Services"),
    };

    [Fact]
    public void Every_await_on_the_apply_path_configures_away_the_synchronization_context()
    {
        var root = RepoPaths.SolutionDir();
        var violations = new List<string>();
        var scanned = 0;

        foreach (var relative in ScopedDirectories)
        {
            var directory = Path.Combine(root, relative);
            Directory.Exists(directory).Should().BeTrue($"scoped directory '{relative}' should exist - has it moved?");

            foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                    || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                    continue;

                var source = File.ReadAllText(file);
                var code = BlankOutCommentsAndLiterals(source);

                foreach (Match match in Regex.Matches(code, @"\bawait\s"))
                {
                    // 'await using' and 'await foreach' carry their own ConfigureAwait forms and are not
                    // what this rule is about.
                    var tail = code.Substring(match.Index + match.Length,
                        Math.Min(9, code.Length - match.Index - match.Length));
                    if (tail.StartsWith("using", StringComparison.Ordinal)
                        || tail.StartsWith("foreach", StringComparison.Ordinal))
                        continue;

                    scanned++;
                    var statement = code[match.Index..EndOfStatement(code, match.Index + match.Length)];
                    if (statement.Contains("ConfigureAwait", StringComparison.Ordinal))
                        continue;

                    var line = source.Take(match.Index).Count(c => c == '\n') + 1;
                    violations.Add($"{Path.GetRelativePath(root, file)}:{line}");
                }
            }
        }

        scanned.Should().BeGreaterThan(100,
            "the scanner should be finding the engine's awaits - a sudden collapse means the scope moved, not that the code got simpler");

        violations.Should().BeEmpty(
            "every await on the apply/detection path must use ConfigureAwait(false); without it a "
            + "sync-over-async boundary in WindowsStateWriter deadlocks the UI thread. Offenders:"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    // Preserves every offset and newline. Without this the word "await" inside a comment reads as code.
    private static string BlankOutCommentsAndLiterals(string text)
    {
        var result = new StringBuilder(text.Length);
        var i = 0;

        while (i < text.Length)
        {
            var c = text[i];

            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                var end = text.IndexOf('\n', i);
                if (end < 0) end = text.Length;
                Blank(result, text, i, end);
                i = end;
            }
            else if (c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                var end = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? text.Length : end + 2;
                Blank(result, text, i, end);
                i = end;
            }
            else if (c == '@' && i + 1 < text.Length && text[i + 1] == '"')
            {
                var j = i + 2;
                while (j < text.Length)
                {
                    if (text[j] == '"' && (j + 1 >= text.Length || text[j + 1] != '"')) { j++; break; }
                    if (text[j] == '"') { j += 2; continue; }   // "" escape inside a verbatim string
                    j++;
                }
                Blank(result, text, i, j);
                i = j;
            }
            else if (c is '"' or '\'')
            {
                var j = i + 1;
                while (j < text.Length && text[j] != c)
                {
                    if (text[j] == '\\') j++;
                    j++;
                }
                j = Math.Min(j + 1, text.Length);
                Blank(result, text, i, j);
                i = j;
            }
            else
            {
                result.Append(c);
                i++;
            }
        }

        return result.ToString();
    }

    private static void Blank(StringBuilder sink, string text, int start, int end)
    {
        for (var k = start; k < end; k++)
            sink.Append(text[k] == '\n' ? '\n' : ' ');
    }

    // Depth-aware: a naive IndexOf(';') stops at the first semicolon inside an
    // await Task.Run(() => { ... }) lambda and misses the trailing }).ConfigureAwait(false).
    private static int EndOfStatement(string code, int from)
    {
        var depth = 0;
        for (var k = from; k < code.Length; k++)
        {
            switch (code[k])
            {
                case '(' or '[' or '{': depth++; break;
                case ')' or ']' or '}': depth--; break;
                case ';' when depth <= 0: return k;
            }
        }

        return code.Length;
    }
}
