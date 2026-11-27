using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Winhance.TestSupport;

namespace Winhance.UI.Tests.Conventions;

// The ledgers were generated on Linux by a script that applies these exact steps, so every rule here must stay a
// plain .NET regex (fixed-width lookbehinds, no $ anchors) that Python's re reads the same way.
// Before a rule runs, C# comments, strings and char literals and XAML comments are blanked to spaces with the
// newlines kept, so a rule never matches prose or a string that merely mentions the API.
internal static class ConventionSource
{
    public const string Ui = "src/Winhance.UI/";

    public static readonly string Root = RepoPaths.SolutionDir();
    public static readonly IReadOnlyList<string> CsAll = Files("*.cs", ".cs");
    public static readonly IReadOnlyList<string> XamlAll = Files("*.xaml", ".xaml");
    public static readonly IReadOnlyList<string> CsUi = [.. CsAll.Where(f => f.StartsWith(Ui, StringComparison.Ordinal))];
    public static readonly IReadOnlyList<string> XamlUi = [.. XamlAll.Where(f => f.StartsWith(Ui, StringComparison.Ordinal))];

    private static readonly ConcurrentDictionary<string, string> Stripped = new();
    private static readonly Regex XamlComment = new("<!--.*?-->", RegexOptions.Singleline);

    private static List<string> Files(string pattern, string extension) =>
        [.. RepoPaths.SourceFiles(pattern)
            .Where(f => f.EndsWith(extension, StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(Root, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];

    public static string Text(string path) =>
        Stripped.GetOrAdd(path, p =>
        {
            var raw = File.ReadAllText(Path.Combine(Root, p)).Replace("\r\n", "\n", StringComparison.Ordinal);
            return p.EndsWith(".cs", StringComparison.Ordinal) ? StripCSharp(raw) : StripXaml(raw);
        });

    public static Dictionary<string, int> Count(IEnumerable<string> files, string pattern, params string[] exempt)
    {
        var regex = new Regex(pattern);
        var counts = new Dictionary<string, int>();
        foreach (var file in files.Where(f => !exempt.Contains(f)))
        {
            var count = regex.Count(Text(file));
            if (count > 0)
                counts[file] = count;
        }
        return counts;
    }

    public static Dictionary<string, int> Merge(params Dictionary<string, int>[] parts)
    {
        var merged = new Dictionary<string, int>();
        foreach (var (file, count) in parts.SelectMany(p => p))
            merged[file] = merged.GetValueOrDefault(file) + count;
        return merged;
    }

    public static string StripXaml(string text) =>
        XamlComment.Replace(text, m => Regex.Replace(m.Value, "[^\n]", " "));

    public static string StripCSharp(string t)
    {
        var output = t.ToCharArray();
        var n = t.Length;
        var i = 0;
        while (i < n)
        {
            var c = t[i];
            int end;
            if (c == '/' && i + 1 < n && t[i + 1] == '/')
            {
                end = t.IndexOf('\n', i);
                if (end < 0)
                    end = n;
            }
            else if (c == '/' && i + 1 < n && t[i + 1] == '*')
            {
                end = t.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? n : end + 2;
            }
            else if (c == '"' || (c is '$' or '@' && IsStringStart(t, i)))
            {
                end = SkipString(t, i);
            }
            else if (c == '\'')
            {
                end = SkipChar(t, i);
            }
            else
            {
                i++;
                continue;
            }

            for (var k = i; k < Math.Min(end, n); k++)
            {
                if (output[k] != '\n')
                    output[k] = ' ';
            }
            i = end;
        }
        return new string(output);
    }

    private static bool IsStringStart(string t, int i)
    {
        var k = i;
        while (k < t.Length && t[k] is '$' or '@')
            k++;
        return k < t.Length && t[k] == '"' && k - i <= 3;
    }

    private static int SkipChar(string t, int i)
    {
        var n = t.Length;
        if (i + 1 < n && t[i + 1] == '\\')
        {
            var close = i + 3 <= n ? t.IndexOf('\'', i + 3) : -1;
            return close < 0 || close - i > 12 ? i + 1 : close + 1;
        }
        return i + 2 < n && t[i + 2] == '\'' ? i + 3 : i + 1;
    }

    private static int SkipHole(string t, int j)
    {
        var n = t.Length;
        var depth = 0;
        while (j < n)
        {
            var c = t[j];
            if (c == '"' || (c is '$' or '@' && IsStringStart(t, j)))
            {
                j = SkipString(t, j);
                continue;
            }
            if (c == '\'')
            {
                j = SkipChar(t, j);
                continue;
            }
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                if (depth == 0)
                    return j + 1;
                depth--;
            }
            j++;
        }
        return n;
    }

    private static int SkipString(string t, int i)
    {
        var n = t.Length;
        var k = i;
        var dollars = 0;
        var verbatim = false;
        while (t[k] is '$' or '@')
        {
            if (t[k] == '$')
                dollars++;
            else
                verbatim = true;
            k++;
        }

        if (k + 2 < n && t[k + 1] == '"' && t[k + 2] == '"')
        {
            var q = k;
            while (q < n && t[q] == '"')
                q++;
            var quotes = q - k;
            var close = t.IndexOf(new string('"', quotes), q, StringComparison.Ordinal);
            return close < 0 ? n : close + quotes;
        }

        var j = k + 1;
        while (j < n)
        {
            var c = t[j];
            if (verbatim)
            {
                if (c == '"')
                {
                    if (j + 1 < n && t[j + 1] == '"')
                    {
                        j += 2;
                        continue;
                    }
                    return j + 1;
                }
            }
            else
            {
                if (c == '\\')
                {
                    j += 2;
                    continue;
                }
                if (c == '"')
                    return j + 1;
                if (c == '\n')
                    return j;
            }

            if (dollars > 0 && c == '{')
            {
                if (j + 1 < n && t[j + 1] == '{')
                {
                    j += 2;
                    continue;
                }
                j = SkipHole(t, j + 1);
                continue;
            }
            j++;
        }
        return n;
    }

    public static int CloseParen(string t, int open)
    {
        var depth = 0;
        for (var j = open; j < t.Length; j++)
        {
            if (t[j] == '(')
            {
                depth++;
            }
            else if (t[j] == ')')
            {
                depth--;
                if (depth == 0)
                    return j;
            }
        }
        return t.Length - 1;
    }

    public static string MatchBody(string t, int open)
    {
        var depth = 0;
        for (var j = open; j < t.Length; j++)
        {
            if (t[j] == '{')
            {
                depth++;
            }
            else if (t[j] == '}')
            {
                depth--;
                if (depth == 0)
                    return t[open..(j + 1)];
            }
        }
        return t[open..];
    }

    // The statement after a closing paren: a { } block, or everything up to the ; at bracket depth 0, which covers
    // expression-bodied members and single-statement loop bodies.
    public static string StatementAfter(string t, int close)
    {
        var j = close + 1;
        while (j < t.Length && char.IsWhiteSpace(t[j]))
            j++;
        if (j < t.Length && t[j] == '{')
            return MatchBody(t, j);

        var depth = 0;
        for (var k = j; k < t.Length; k++)
        {
            var c = t[k];
            if (c is '(' or '[' or '{')
            {
                depth++;
            }
            else if (c is ')' or ']' or '}')
            {
                depth--;
                if (depth < 0)
                    return t[j..k];
            }
            else if (c == ';' && depth == 0)
            {
                return t[j..(k + 1)];
            }
        }
        return t[j..];
    }

    public static int BlockEnd(string t, int start)
    {
        var depth = 0;
        for (var k = start; k < t.Length; k++)
        {
            if (t[k] == '{')
            {
                depth++;
            }
            else if (t[k] == '}')
            {
                if (depth == 0)
                    return k;
                depth--;
            }
        }
        return t.Length;
    }
}
