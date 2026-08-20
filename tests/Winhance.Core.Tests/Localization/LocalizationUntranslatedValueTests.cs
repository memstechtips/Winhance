using System.Text.Json;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Winhance.TestSupport;

namespace Winhance.Core.Tests.Localization;

// Key parity (LocalizationJsonValidityTests) proves all 29 files carry the same keys; nothing proved the
// values were in that language. The first version of this test only failed a key that was identical in ALL 28
// non-English files, and ViewMode_Compact ("Compact View") walked straight through that bar: Hindi alone
// translated it, the other 27 carried the English, and it reached a user running the app in Afrikaans as an
// English tooltip. One translator doing the work cannot exempt the other 27, so unanimity is no longer the
// bar. Two rules fail the build - a string copied into another writing system, and a language left behind by
// the rest - and a third tier only reports. All three compare Trim()-ordinal, since a trailing space is not a
// translation, and a locale missing the key counts in neither direction.
public class LocalizationUntranslatedValueTests
{
    private readonly ITestOutputHelper _output;

    public LocalizationUntranslatedValueTests(ITestOutputHelper output) => _output = output;

    // The 11 shipped languages not written in Latin script. Sharing vocabulary with English is what makes an
    // identical value defensible, and none of these can do it: an Arabic, Greek, Hebrew, Devanagari, CJK or
    // Cyrillic value that matches English byte for byte was copied, not written. A brand name or a number
    // still can, which is what UntranslatableKeys is for.
    private static readonly HashSet<string> NonLatinLocales = new(StringComparer.Ordinal)
    {
        "ar", "el", "fa", "he", "hi", "ja", "ko", "ru", "uk", "zh-Hans", "zh-Hant",
    };

    // Rule 1's bar. Measured over the current files: 15 keys are echoed by exactly one non-Latin locale, and
    // 14 of them are one translator's individual lapse - the shape rule 2 is built for - while the 15th
    // ("byte {0}", Greek) is a loanword. At two the agreement spans two writing systems and coincidence is
    // gone. Three would be too high: the lowest offender only this rule can see is the svchost threshold's
    // Option_0 ("Default"), copied by Greek and Korean and nobody else.
    private const int ScriptMismatchThreshold = 2;

    // Rule 2's bar, in locales still showing English. Key parity puts Carrying at 28, so five stragglers means
    // 23 translators produced a target-language form and the string demonstrably has one. Every one of the 15
    // keys at or under five is a real miss; the first key past it ("byte {0}", nine) is not, which is where
    // "the odd one out" stops describing the data.
    private const int StragglerThreshold = 5;

    // The all-28 rule this file used to carry is gone rather than kept beside these two. Key parity means a
    // key copied into every file is also copied into all 11 non-Latin ones, so rule 1 fails it first; measured
    // against the current files it added no key of its own.

    // A key earns a place here only when the English string has no target-language form at all: a brand or
    // product name, a literal the code writes or matches, a magnitude with its unit symbol, or a bare glyph.
    // "The locales currently agree" is not a reason. Where the call was close it was settled by what the same
    // translators did with the same words elsewhere in the same file.
    private static readonly string[] UntranslatableKeys =
    [
        // The app's own name, and the Windows power scheme it creates. PowerPlanActivationService and
        // PowerService find that scheme by comparing its name to the literal "Winhance Power Plan", so a
        // translated label would name a plan powercfg never shows.
        "App_Title",
        "PowerPlan_WinhancePowerPlan_Name",

        // Brand, product and standards-body names, carried unchanged by every Windows UI language. Persian
        // reorders the Copilot ones ("Copilot in Excel"), which moves the two names around rather than
        // translating either.
        "SettingGroup_Xbox",
        "SettingGroup_ATIPowerPlay",
        "SettingGroup_InternetExplorer",
        "SettingGroup_PCI_Express",
        "Setting_privacy-turn-off-copilot_Name",
        "Setting_privacy-excel-copilot_Name",
        "Setting_privacy-onenote-copilot_Name",
        "Setting_privacy-word-copilot_Name",
        // "Multi-Plane Overlay (MPO)" - the DirectX presentation path, named in English in Microsoft's docs.
        "Setting_gaming-disable-mpo_Name",
        // The Windows release names, and the surname of the author of the online answer-file generator next
        // to the file format it emits. The ten locales that differ only reorder them ("XML de Schneegans").
        "WIMUtil_ButtonWindows10",
        "WIMUtil_ButtonWindows11",
        "WIMUtil_ButtonSchneegans",

        // Microsoft feature names Microsoft itself leaves in English. "Click to Do" stands in 27 of the 28
        // files. "Xbox Game DVR" names the GameDVR registry family the setting writes (GameDVR_Enabled,
        // AllowGameDVR), and the neighbouring Xbox keys are the evidence: the same translators who render
        // "Xbox Live Networking Service" and "Xbox Live Game Save" in their own script keep this one.
        "Setting_privacy-disable-click-to-do_Name",
        "Setting_gaming-xbox-game-dvr_Name",

        // A product name plus the abbreviation "AI", with no third word to carry. Japanese, Korean, both
        // Chinese files and Greek write "AI" verbatim in the 22 other keys that contain it - "AI Manager"
        // keeps its "AI" and translates only "Manager" - so nothing is left untranslated in these four.
        "SettingGroup_Windows_AI",
        "SettingGroup_Microsoft_Edge_AI",
        "SettingGroup_Microsoft_Office_AI",
        "Setting_privacy-edge-devtools-ai_Name",

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

        // A magnitude and its unit symbol. Listed one by one rather than by prefix because Option_0 of the
        // same setting is the word "Default", which ten locales did translate and the rest owe.
        "Setting_gaming-performance-svchost-split-threshold_Option_1",
        "Setting_gaming-performance-svchost-split-threshold_Option_2",
        "Setting_gaming-performance-svchost-split-threshold_Option_3",
        "Setting_gaming-performance-svchost-split-threshold_Option_4",
        "Setting_gaming-performance-svchost-split-threshold_Option_5",
        "Setting_gaming-performance-svchost-split-threshold_Option_6",
        "Setting_gaming-performance-svchost-split-threshold_Option_7",
        "Setting_gaming-performance-svchost-split-threshold_Option_8",
        "Setting_gaming-performance-svchost-split-threshold_Option_9",

        // "200ms" is the one option in each of these two settings with no word beside the number; its five
        // siblings all carry a parenthetical ("100ms (Moderate)") and every locale translated those, so the
        // exemption is not hiding a missed label.
        "Setting_gaming-performance-mouse-hover-time_Option_4",
        "Setting_taskbar-extended-hover-time_Option_4",

        // A placeholder and its punctuation, with no word in it. French, Japanese and both Chinese files
        // adjust the spacing or use a fullwidth colon, which is typography rather than translation.
        "Overview_OutcomeBanner_Label",

        // Windows itself labels this button "OK" in most of its UI languages, German, French and Japanese
        // included; the locales that chose a word of their own were free to.
        "Button_OK",

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

    private static int InAnotherScript(List<string> echoing) => echoing.Count(NonLatinLocales.Contains);

    private static Dictionary<string, string> Load(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.EnumerateObject()
            .ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty);
    }

    // The counts are non-vacuity guards: a broken path or glob would otherwise hand every tier a clean bill.
    // The two subset checks guard the same way against silent decay - rename a file (zh-Hans to zh-CN) and
    // rule 1's bar stops being reachable, rename a key and its exemption goes on being trusted forever.
    private static Dictionary<string, Dictionary<string, string>> LoadLocales()
    {
        var locales = Directory.GetFiles(RepoPaths.LocalizationDir(), "*.json")
            .ToDictionary(f => Path.GetFileNameWithoutExtension(f), Load, StringComparer.Ordinal);

        locales.Should().HaveCountGreaterThan(20, "every shipped language file must be scanned");
        locales.Should().ContainKey("en");
        locales["en"].Should().HaveCountGreaterThan(500, "en.json is the reference and must have loaded");
        NonLatinLocales.Should().BeSubsetOf(locales.Keys, "each non-Latin locale must still be a shipped file");
        UntranslatableKeys.Should().BeSubsetOf(locales["en"].Keys, "each exemption must still name a live key");
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

    // One row per key, carrying everything a translator needs: the English string and the exact files owing a
    // translation of it.
    private void Print(
        Dictionary<string, Dictionary<string, string>> locales,
        string heading,
        List<(string Key, string English, List<string> Echoing, int Carrying)> offenders)
    {
        _output.WriteLine($"Locale files scanned : {locales.Count}");
        _output.WriteLine($"en.json keys         : {locales["en"].Count}");
        _output.WriteLine($"{heading} : {offenders.Count}");
        _output.WriteLine(string.Empty);

        foreach (var c in offenders)
        {
            _output.WriteLine(
                $"[{c.Echoing.Count}/{c.Carrying}, {InAnotherScript(c.Echoing)} in another script] "
                + $"{c.Key} = \"{OneLine(c.English)}\"");
            _output.WriteLine("    " + string.Join(" ", c.Echoing));
        }
    }

    [Fact]
    public void NoKey_CarriesTheEnglishStringIntoAnotherWritingSystem()
    {
        var locales = LoadLocales();

        var copied = Coverage(locales)
            .Where(c => InAnotherScript(c.Echoing) >= ScriptMismatchThreshold && !IsUntranslatable(c.Key))
            .OrderByDescending(c => InAnotherScript(c.Echoing))
            .ThenBy(c => c.Key, StringComparer.Ordinal)
            .ToList();

        Print(locales, "Copied into another script", copied);

        copied.Select(c => c.Key).Should().BeEmpty(
            "a value that two or more non-Latin languages spell exactly like English was copied out of "
            + "en.json rather than translated; translate it, or add it to UntranslatableKeys with the reason "
            + "it has no target-language form");
    }

    [Fact]
    public void NoKey_IsLeftInEnglishByTheFewLocalesThatFellBehind()
    {
        var locales = LoadLocales();

        var missed = Coverage(locales)
            .Where(c => c.Echoing.Count <= StragglerThreshold
                        && InAnotherScript(c.Echoing) >= 1
                        && !IsUntranslatable(c.Key))
            .OrderBy(c => c.Echoing.Count)
            .ThenBy(c => c.Key, StringComparer.Ordinal)
            .ToList();

        Print(locales, "Left behind by a few locales", missed);

        missed.Select(c => c.Key).Should().BeEmpty(
            "the rest of the languages produced a translation of this string, so the few still showing "
            + "English missed it; the non-Latin ones among them cannot be sharing a word with English");
    }

    // What neither rule can adjudicate: Latin-script locales legitimately spell things the way English does.
    // The widest agreement measured here is 12 locales on "Auto HDR" - a Microsoft feature name - with
    // "Status", "Normal" and "Script" reaching 7 and 8 as ordinary cognates, so there is no honest place above
    // that for a failing bar. Print the band and read it, from six locales deep: 107 keys sit in this band
    // and all but a handful are one or two locales wide, which is noise rather than a list. UntranslatableKeys
    // is deliberately NOT applied: an exempt key that some translator did render lands here, which is how a
    // stale exemption surfaces.
    [Fact]
    public void EchoesAmongLatinScriptLocalesOnly_Report()
    {
        var locales = LoadLocales();

        var cognates = Coverage(locales)
            .Where(c => InAnotherScript(c.Echoing) == 0 && c.Echoing.Count >= 6)
            .OrderByDescending(c => c.Echoing.Count)
            .ThenBy(c => c.Key, StringComparer.Ordinal)
            .ToList();

        Print(locales, "Latin-script agreement to eyeball", cognates);

        // Report-only, like DuplicateTranslations_Report.
        cognates.Should().NotBeNull();
    }
}
