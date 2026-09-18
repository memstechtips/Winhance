using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.Style;

// Core takes its interop from CsWin32 where the Win32 metadata carries the API and from [LibraryImport]
// where it does not; a hand-written [DllImport] is the last resort, because it marshals by convention
// rather than by generated code - the signature that typed a GUID** as out IntPtr is why the app still
// shells out to powercfg.exe instead of calling PowerSetActiveScheme. Nothing in the compiler expresses
// that order of preference, so this does.
public class NativeInteropDisciplineTests
{
    // Core only. Winhance.UI declares its own by hand for shell and dialog APIs and was never part of this
    // migration, so widening the scope would ship this red instead of catching anything. The one
    // hand-written file that has to exist lives in Infrastructure: DISM has no Win32 metadata
    // (microsoft/win32metadata#1289, closed by a maintainer: the API is not in the SDK, so it would have to
    // come from the kit owner), leaving CsWin32 nothing to generate.
    private static readonly string CoreDirectory = Path.Combine("src", "Winhance.Core");

    private static readonly string HandWrittenByNecessity =
        Path.Combine("src", "Winhance.Infrastructure", "Features", "Common", "Native");

    [Fact]
    public void Core_declares_no_hand_written_DllImport()
    {
        var (filesScanned, declarations) = Scan(CoreDirectory);

        filesScanned.Should().BeGreaterThan(200,
            "Winhance.Core is a few hundred files - a collapse here means the scan lost the tree, not that Core shrank");

        var offenders = declarations.Select(d => $"{d.RelativePath}:{d.Line}").ToList();

        offenders.Should().BeEmpty(
            "generate the API with CsWin32, or use [LibraryImport] when the metadata does not carry it. "
            + "An API genuinely absent from the metadata, as DISM is, belongs in Infrastructure beside "
            + "DismApi.cs, never in Core. Offenders:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    // A clean Core proves nothing if the scan has stopped matching the attribute. DismApi.cs is the one
    // file guaranteed to declare imports by hand, so it is the control - and the only file allowed to.
    [Fact]
    public void The_scan_still_recognises_a_hand_written_DllImport()
    {
        var (_, declarations) = Scan(HandWrittenByNecessity);

        declarations.Should().NotBeEmpty(
            "DismApi.cs declares its DISM imports by hand; a scan that finds none of them has stopped matching");
        declarations.Should().OnlyContain(
            d => d.RelativePath.EndsWith("DismApi.cs", StringComparison.Ordinal),
            "any other hand-written interop under Infrastructure needs the justification DISM has");
    }

    private static (int FilesScanned, List<(string RelativePath, int Line)> Declarations) Scan(string scopedDirectory)
    {
        var root = RepoPaths.SolutionDir();
        var directory = Path.Combine(root, scopedDirectory);
        Directory.Exists(directory).Should().BeTrue($"scoped directory '{scopedDirectory}' should exist - has it moved?");

        var filesScanned = 0;
        var declarations = new List<(string RelativePath, int Line)>();

        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;

            filesScanned++;
            var source = File.ReadAllText(file);
            var code = BlankOutCommentsAndLiterals(source);

            foreach (Match match in Regex.Matches(code, @"\bDllImport(?:Attribute)?\s*\("))
            {
                var line = source.Take(match.Index).Count(c => c == '\n') + 1;
                declarations.Add((Path.GetRelativePath(root, file), line));
            }
        }

        return (filesScanned, declarations);
    }

    // Preserves every offset and newline. Without this the DllImport inside OneDriveRemovalScript's
    // PowerShell string reads as a declaration, and so do the eleven in ScriptPreambleSection.
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
}
