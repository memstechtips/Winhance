using System.Text.Json;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Winhance.TestSupport;

namespace Winhance.Core.Tests.Localization;

// Key parity (LocalizationJsonValidityTests) proves all 29 files carry the same keys; nothing proved the
// values were in that language. A key added to en.json and copied unchanged into the other 28 passes every
// other test, which is how 14 keys shipped untranslated. Two tiers, because the two shapes need different
// treatment: identical in EVERY language is the copy signature and fails the build; identical in only some
// is a missed language and only prints, because Latin-script locales share words with English legitimately
// (nl, nl-BE, fr and de supply most of the low counts). Values compare Trim()-ordinal, since a
// trailing space is not a translation, and a locale missing the key counts in neither direction.
public class LocalizationUntranslatedValueTests
{
    private readonly ITestOutputHelper _output;

    public LocalizationUntranslatedValueTests(ITestOutputHelper output) => _output = output;

    // Being identical everywhere is what makes a key a candidate; what puts it here is that a name, a
    // literal or a glyph has no target-language form at all.
    private static readonly string[] UntranslatableKeys =
    [
        // Microsoft brand and product names, carried unchanged by every Windows UI language.
        "SettingGroup_Xbox",
        "Setting_privacy-turn-off-copilot_Name",
        // "Multi-Plane Overlay (MPO)" - the DirectX presentation path, named in English in Microsoft's docs.
        "Setting_gaming-disable-mpo_Name",
        // Windows Setup searches for the answer file by the literal name autounattend.xml.
        "Builder_Mode_Target_Autounattend",
        // Resolver brand plus its IP address. Options 0, 2 and 3 of the same setting are prose
        // ("Automatic (DHCP)", "Cloudflare Malware Blocking (1.1.1.2)") and are translated everywhere, so a
        // prefix rule over this family would exempt strings that do need translating.
        "Setting_gaming-dns-server_Option_1",
        "Setting_gaming-dns-server_Option_4",
        "Setting_gaming-dns-server_Option_5",
        "Setting_gaming-dns-server_Option_6",
        "Setting_gaming-dns-server_Option_7",
        "Setting_gaming-dns-server_Option_8",
        "Setting_gaming-dns-server_Option_9",
        // Single-character badges painted over the toggle by SettingItemViewModel.OverlayShortLabelFor:
        // "!", "x" and "?" are marks, not words.
        "Common_MalformedState_ShortLabel",
        "Common_UndeterminedState_ShortLabel",
        "Common_CustomState_ShortLabel",
    ];

    // Every option label of this setting is a date pattern ("M/d/yyyy") mirroring the literal it writes to
    // HKCU\Control Panel\International\sShortDate. The pattern letters are culture-invariant, so a translated
    // label would advertise a format the setting does not apply. Its Name and Description are prose, which is
    // why the rule stops at _Option_.
    private static readonly string[] UntranslatablePrefixes =
    [
        "Setting_explorer-customization-short-date_Option_",
    ];

    private static bool IsUntranslatable(string key) =>
        UntranslatableKeys.Contains(key, StringComparer.Ordinal)
        || UntranslatablePrefixes.Any(p => key.StartsWith(p, StringComparison.Ordinal));

    private static Dictionary<string, string> Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty);
    }

    // The counts are non-vacuity guards: a broken path or glob would otherwise hand both tiers a clean bill.
    private static Dictionary<string, Dictionary<string, string>> LoadLocales()
    {
        var locales = Directory.GetFiles(RepoPaths.LocalizationDir(), "*.json")
            .ToDictionary(f => Path.GetFileNameWithoutExtension(f), Load, StringComparer.Ordinal);

        locales.Should().HaveCountGreaterThan(20, "every shipped language file must be scanned");
        locales.Should().ContainKey("en");
        locales["en"].Should().HaveCountGreaterThan(500, "en.json is the reference and must have loaded");
        return locales;
    }

    // Carrying counts the non-English files that HAVE the key; Echoing is the subset repeating the English
    // string. A file missing the key lands in neither, leaving key parity to LocalizationJsonValidityTests.
    private static List<(string Key, string English, List<string> Echoing, int Carrying)> Coverage(
        Dictionary<string, Dictionary<string, string>> locales)
    {
        var translations = locales.Where(l => l.Key != "en").ToList();
        var coverage = new List<(string Key, string English, List<string> Echoing, int Carrying)>();

        foreach (var (key, english) in locales["en"])
        {
            if (string.IsNullOrWhiteSpace(english))
                continue;

            var echoing = translations
                .Where(l => l.Value.TryGetValue(key, out var translated)
                            && string.Equals(translated.Trim(), english.Trim(), StringComparison.Ordinal))
                .Select(l => l.Key)
                .OrderBy(l => l, StringComparer.Ordinal)
                .ToList();

            int carrying = translations.Count(l => l.Value.ContainsKey(key));

            if (echoing.Count > 0 && carrying > 0)
                coverage.Add((key, english, echoing, carrying));
        }

        return coverage;
    }

    // Several locale values are multi-line and a few hundred characters long; the gate log needs one
    // scannable row per offender.
    private static string OneLine(string value)
    {
        var flat = value.ReplaceLineEndings(" ");
        return flat.Length <= 80 ? flat : flat[..77] + "...";
    }

    [Fact]
    public void NoKey_CarriesTheEnglishStringInEveryLanguage()
    {
        var locales = LoadLocales();

        var neverTranslated = Coverage(locales)
            .Where(c => c.Echoing.Count == c.Carrying)
            .OrderBy(c => c.Key, StringComparer.Ordinal)
            .ToList();

        var offenders = neverTranslated.Where(c => !IsUntranslatable(c.Key)).ToList();

        _output.WriteLine($"Locale files scanned     : {locales.Count}");
        _output.WriteLine($"en.json keys             : {locales["en"].Count}");
        _output.WriteLine($"Exempt as untranslatable : {neverTranslated.Count - offenders.Count}");
        _output.WriteLine($"Never translated         : {offenders.Count}");
        _output.WriteLine(string.Empty);

        foreach (var c in offenders)
            _output.WriteLine($"{c.Key} = \"{OneLine(c.English)}\"");

        offenders.Select(c => c.Key).Should().BeEmpty(
            "a key holding the English string in every language is one that was added to en.json and copied "
            + "into the other files; translate it, or add it to UntranslatableKeys with the reason it has no "
            + "target-language form");
    }

    // Threshold: half the non-English locales, so 14 of 28. Matching English in a few locales is usually
    // legitimate - 107 of the 130 keys under the bar are identical only in Latin-script locales, and nl,
    // nl-BE, fr and de supply most of that - so a lower bar would bury the real misses in cognates. The
    // widest all-Latin agreement measured here is 12 ("Auto HDR"); every group at or above 14 also holds at
    // least two locales written in another script, where sharing a word with English is not what happened.
    // UntranslatableKeys is deliberately NOT applied here: an exempt key that one translator did translate
    // drops out of tier 1 and lands in this list, which is how a stale exemption surfaces.
    [Fact]
    public void PartiallyUntranslatedKeys_Report()
    {
        var locales = LoadLocales();
        int translationCount = locales.Count - 1;
        int threshold = translationCount / 2;

        var missedLanguages = Coverage(locales)
            .Where(c => c.Echoing.Count < c.Carrying && c.Echoing.Count >= threshold)
            .OrderByDescending(c => c.Echoing.Count)
            .ThenBy(c => c.Key, StringComparer.Ordinal)
            .ToList();

        _output.WriteLine($"Reported at or above     : {threshold} of {translationCount} non-English locales");
        _output.WriteLine($"Keys over the threshold  : {missedLanguages.Count}");
        _output.WriteLine(string.Empty);

        foreach (var c in missedLanguages)
        {
            _output.WriteLine($"[{c.Echoing.Count}/{c.Carrying}] {c.Key} = \"{OneLine(c.English)}\"");
            _output.WriteLine("    " + string.Join(" ", c.Echoing));
        }

        // Report-only, like DuplicateTranslations_Report: a missed language is a translation task, not a
        // broken build.
        missedLanguages.Should().NotBeNull();
    }
}
