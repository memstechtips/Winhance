using System.Globalization;

namespace Winhance.Core.Features.WimUtil.Models;

// sources\lang.ini on Microsoft's media names the languages its Setup can show, one per line:
//   [Available UI Languages]
//   en-us = 3
public static class MediaLanguages
{
    private const string Section = "[Available UI Languages]";

    public static IReadOnlyList<CultureInfo> Parse(string langIni)
    {
        var languages = new List<CultureInfo>();
        var inSection = false;
        foreach (var raw in langIni.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inSection = string.Equals(line, Section, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inSection || line.Split('=', 2)[0].Trim() is not { Length: > 0 } name)
                continue;

            // A name .NET does not know could not be offered on the card anyway.
            if (CultureInfo.GetCultures(CultureTypes.AllCultures)
                    .FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) is { } culture)
                languages.Add(culture);
        }
        return languages;
    }
}
