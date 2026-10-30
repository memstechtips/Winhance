using System;
using System.Collections.Generic;
using System.Text;

namespace Winhance.Localization.Generator;

// Parsed by hand: an analyzer must not drag System.Text.Json into the compiler host, where an assembly-version
// clash stops the generator loading (warning CS8785) and every LocKey reference then fails to compile.
internal static class FlatJson
{
    public static IReadOnlyDictionary<string, string> Parse(string text)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var i = 0;

        // A BOM survives GetText().ToString() when the file carries one, and these files do.
        if (text.Length > 0 && text[0] == '﻿') i = 1;

        SkipWhitespace(text, ref i);
        Expect(text, ref i, '{');

        SkipWhitespace(text, ref i);
        if (Peek(text, i) == '}') return result;

        while (true)
        {
            SkipWhitespace(text, ref i);
            var key = ReadString(text, ref i);

            SkipWhitespace(text, ref i);
            Expect(text, ref i, ':');

            SkipWhitespace(text, ref i);
            result[key] = ReadString(text, ref i);

            SkipWhitespace(text, ref i);
            var c = Peek(text, i);
            if (c == ',') { i++; continue; }
            if (c == '}') { i++; break; }
            throw new FormatException($"expected ',' or '}}' at offset {i}, found '{c}'");
        }

        return result;
    }

    private static char Peek(string s, int i) =>
        i < s.Length ? s[i] : throw new FormatException("unexpected end of file");

    private static void Expect(string s, ref int i, char c)
    {
        if (Peek(s, i) != c) throw new FormatException($"expected '{c}' at offset {i}, found '{s[i]}'");
        i++;
    }

    private static void SkipWhitespace(string s, ref int i)
    {
        while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n')) i++;
    }

    private static string ReadString(string s, ref int i)
    {
        Expect(s, ref i, '"');
        var sb = new StringBuilder();

        while (true)
        {
            var c = Peek(s, i++);
            if (c == '"') return sb.ToString();

            if (c != '\\') { sb.Append(c); continue; }

            var esc = Peek(s, i++);
            switch (esc)
            {
                case '"': sb.Append('"'); break;
                case '\\': sb.Append('\\'); break;
                case '/': sb.Append('/'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'u':
                    if (i + 4 > s.Length) throw new FormatException("truncated \\u escape");
                    sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                    i += 4;
                    break;
                default: throw new FormatException($"unknown escape '\\{esc}' at offset {i}");
            }
        }
    }
}
