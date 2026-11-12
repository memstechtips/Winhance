using System.Text;

namespace Winhance.Core.Features.Common.Helpers;

public static class PowerShellQuoting
{
    // PowerShell ends a string on the typographic quotes as well as the ASCII one (language spec 2.3.5.2), and the
    // locale files are full of them. Each is doubled as itself.
    public static string? Escape(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var escaped = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            escaped.Append(c);
            if (IsSingleQuote(c))
                escaped.Append(c);
        }

        return escaped.ToString();
    }

    // Reads back what Escape wrote: each doubled quote is one quote again.
    public static string Unescape(string escaped)
    {
        var text = new StringBuilder(escaped.Length);
        for (var i = 0; i < escaped.Length; i++)
        {
            text.Append(escaped[i]);
            if (IsSingleQuote(escaped[i]) && i + 1 < escaped.Length && escaped[i + 1] == escaped[i])
                i++;
        }

        return text.ToString();
    }

    private static bool IsSingleQuote(char c) => c is '\'' or '\u2018' or '\u2019' or '\u201A' or '\u201B';
}
