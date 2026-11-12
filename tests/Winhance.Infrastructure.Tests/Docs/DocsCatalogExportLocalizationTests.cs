using Xunit;

namespace Winhance.Infrastructure.Tests.Docs;

public class DocsCatalogExportLocalizationTests
{
    [Fact]
    public void TryGetString_ShippedSettingName_ResolvesFromEnJson()
    {
        var loc = EnJsonLocalization.Load();

        Assert.True(loc.TryGetString("Setting_sound-startup_Name", out var name));
        Assert.Equal("Startup Sound During Boot", name);
        Assert.Equal("Startup Sound During Boot", loc.GetString("Setting_sound-startup_Name"));
    }

    [Fact]
    public void TryGetString_MissingKey_IsReportedMissingNotBracketed()
    {
        var loc = EnJsonLocalization.Load();

        Assert.False(loc.TryGetString("Setting_does-not-exist_Name", out var value));
        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void GetString_WithPlaceholder_FormatsIt()
    {
        var loc = EnJsonLocalization.Load();

        Assert.Equal("Requires Windows build 26100 or higher", loc.GetString("Compatibility_MinBuild", "26100"));
    }
}
