using System.Text;

namespace Winhance.Core.Features.Common.Localization;

public static class LocalizedText
{
    // Fullwidth punctuation is drawn in a full em box with the blank half inside the glyph, so the ASCII
    // space that separates two Latin sentences reads as a double gap after a CJK one. The ranges are CJK
    // Symbols and Punctuation and the fullwidth ASCII forms; halfwidth katakana starts at U+FF61 and is
    // left out because it is half-width and still needs the space. Not a localization key: every locale
    // file is rejected for a blank value, and LocalizationService reads an empty one as untranslated.
    public static string JoinSentences(IEnumerable<string> sentences)
    {
        var joined = new StringBuilder();

        foreach (var sentence in sentences)
        {
            if (string.IsNullOrWhiteSpace(sentence))
                continue;

            if (joined.Length > 0 && !CarriesItsOwnSpacing(joined[^1]))
                joined.Append(' ');

            joined.Append(sentence);
        }

        return joined.ToString();
    }

    private static bool CarriesItsOwnSpacing(char c) =>
        c is (>= '\u3000' and <= '\u303F') or (>= '\uFF01' and <= '\uFF60');
}
