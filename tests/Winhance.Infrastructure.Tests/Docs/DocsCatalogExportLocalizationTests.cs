using Xunit;

namespace Winhance.Infrastructure.Tests.Docs;

public class DocsCatalogExportLocalizationTests
{
    [Fact]
    public void Resolves_a_shipped_setting_name_from_en_json()
    {
        var loc = EnJsonLocalization.Load();

        Assert.True(loc.TryGetString("Setting_sound-startup_Name", out var name));
        Assert.Equal("Startup Sound During Boot", name);
        Assert.Equal("Startup Sound During Boot", loc.GetString("Setting_sound-startup_Name"));
    }

    [Fact]
    public void Missing_key_is_reported_missing_not_bracketed()
    {
        var loc = EnJsonLocalization.Load();

        Assert.False(loc.TryGetString("Setting_does-not-exist_Name", out var value));
        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void Formats_placeholders()
    {
        var loc = EnJsonLocalization.Load();

        Assert.Equal("Requires Windows build 26100 or higher", loc.GetString("Compatibility_MinBuild", "26100"));
    }
}
