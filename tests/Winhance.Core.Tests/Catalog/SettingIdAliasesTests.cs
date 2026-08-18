using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

/// <summary>Tests the retired-id -> canonical-id alias used on config import. The 6 "-win10" This PC
/// folder ids must map to their canonical catalog peers; everything else passes through unchanged. Also asserts the
/// invariant that every alias TARGET exists in the live catalog and every SOURCE does not (i.e. is genuinely
/// retired/merged) so the map can't silently rot.</summary>
public class SettingIdAliasesTests
{
    [Theory]
    [InlineData("explorer-customization-thispc-folder-desktop-win10", "explorer-customization-thispc-folder-desktop")]
    [InlineData("explorer-customization-thispc-folder-documents-win10", "explorer-customization-thispc-folder-documents")]
    [InlineData("explorer-customization-thispc-folder-downloads-win10", "explorer-customization-thispc-folder-downloads")]
    [InlineData("explorer-customization-thispc-folder-music-win10", "explorer-customization-thispc-folder-music")]
    [InlineData("explorer-customization-thispc-folder-pictures-win10", "explorer-customization-thispc-folder-pictures")]
    [InlineData("explorer-customization-thispc-folder-videos-win10", "explorer-customization-thispc-folder-videos")]
    public void Normalize_MapsRetiredWin10Id_ToCanonical(string retired, string canonical)
    {
        Assert.Equal(canonical, SettingIdAliases.Normalize(retired));
    }

    [Theory]
    [InlineData("explorer-customization-thispc-folder-desktop")] // already canonical
    [InlineData("security-uac-level")]
    [InlineData("taskbar-search-box-10")]                        // a different "-10" id that is NOT merged
    [InlineData("")]
    [InlineData("not-a-real-id")]
    public void Normalize_LeavesNonAliasIdUnchanged(string id)
    {
        Assert.Equal(id, SettingIdAliases.Normalize(id));
    }

    [Fact]
    public void EveryAliasTarget_ExistsInTheLiveCatalog()
    {
        var catalogIds = SettingCatalog.All.Select(s => s.Id).ToHashSet();
        foreach (var win10Id in Win10ThisPcSources)
        {
            var canonical = SettingIdAliases.Normalize(win10Id);
            Assert.NotEqual(win10Id, canonical); // it WAS aliased
            Assert.Contains(canonical, catalogIds); // and its target is a real catalog setting
        }
    }

    [Fact]
    public void EveryAliasSource_IsAbsentFromTheCatalog()
    {
        // The retired "-win10" ids are merged away; if one reappears as a real catalog id the alias is wrong.
        var catalogIds = SettingCatalog.All.Select(s => s.Id).ToHashSet();
        foreach (var win10Id in Win10ThisPcSources)
            Assert.DoesNotContain(win10Id, catalogIds);
    }

    private static readonly string[] Win10ThisPcSources =
    {
        "explorer-customization-thispc-folder-desktop-win10",
        "explorer-customization-thispc-folder-documents-win10",
        "explorer-customization-thispc-folder-downloads-win10",
        "explorer-customization-thispc-folder-music-win10",
        "explorer-customization-thispc-folder-pictures-win10",
        "explorer-customization-thispc-folder-videos-win10",
    };
}
