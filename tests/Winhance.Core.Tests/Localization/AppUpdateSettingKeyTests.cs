using System.Text.Json;
using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Localization;

// The Settings dropdowns build their keys from the enum value names, which the static key gate cannot see.
public class AppUpdateSettingKeyTests
{
    private static HashSet<string> EnglishKeys()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoPaths.LocalizationDir(), "en.json")));
        return doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();
    }

    [Fact]
    public void EnglishKeys_EveryUpdateCheckFrequencyAndAction_HasItsLabel()
    {
        var keys = EnglishKeys();

        Enum.GetValues<AppUpdateCheckFrequency>().Select(f => $"Settings_UpdateCheck_{f}")
            .Concat(Enum.GetValues<AppUpdateAction>().Select(a => $"Settings_UpdateAction_{a}"))
            .Where(key => !keys.Contains(key))
            .Should().BeEmpty();
    }
}
