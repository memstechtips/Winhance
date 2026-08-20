using System.Text.Json;
using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Localization;
using Winhance.IntegrationTests.Helpers;
using Xunit;

namespace Winhance.IntegrationTests.Localization;

// Option-display keys are normally SOFT (a missing one falls back to the English label baked into the
// catalog). A DETECT-ONLY state is the exception: it has no dropdown item, so its Setting_{id}_Option_{i} key is
// the ONLY place its name can come from - miss it in one language and that language shows the raw English
// label; miss it in en.json and every language does.
public class DetectOnlyStateLocalizationTests
{
    private static readonly string LocalizationFolder =
        Path.Combine(TestContext.SolutionDir, "src", "Winhance.UI", "Features", "Common", "Localization");

    // Every detect-only (setting, state index) crossed with every localization file, so a failure names the exact file AND key.
    public static IEnumerable<object[]> DetectOnlyKeysPerFile()
    {
        var keys = new List<string>();
        foreach (var setting in SettingCatalog.All)
        {
            for (int i = 0; i < setting.States.Count; i++)
            {
                var state = setting.States[i];
                if (state.IsDetectOnly && !SettingLocalizationKeys.IsLocalizationKey(state.Label))
                    keys.Add(SettingLocalizationKeys.OptionDisplay(setting, i));
            }
        }

        foreach (var file in Directory.GetFiles(LocalizationFolder, "*.json"))
            foreach (var key in keys)
                yield return [Path.GetFileName(file), key];
    }

    [Theory]
    [MemberData(nameof(DetectOnlyKeysPerFile))]
    public void EveryDetectOnlyStateHasANonBlankLocalizedName(string fileName, string key)
    {
        var json = File.ReadAllText(Path.Combine(LocalizationFolder, fileName));
        using var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty(key, out var value).Should().BeTrue(
            $"{fileName} must define \"{key}\" - a detect-only state has no dropdown item, so this key is the "
            + "only source for the name the card shows");
        value.GetString().Should().NotBeNullOrWhiteSpace($"{fileName} key \"{key}\" must not be blank");
    }

    [Fact]
    public void TheCatalogActuallyHasADetectOnlyState()
    {
        // Non-vacuity. A Theory over an empty MemberData set passes silently, which would turn the gate
        // above into decoration the day the last detect-only state is retired or renamed.
        SettingCatalog.All.SelectMany(s => s.States).Should().Contain(st => st.IsDetectOnly);
    }
}
