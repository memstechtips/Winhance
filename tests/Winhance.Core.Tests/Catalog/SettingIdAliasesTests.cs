using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

// Also asserts every alias TARGET exists in the live catalog and every SOURCE does not, so the map can't silently rot.
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
    [InlineData("explorer-customization-context-menu", "context-menu-classic")]
    [InlineData("explorer-take-ownership", "context-menu-take-ownership")]
    [InlineData("explorer-context-menu-firewall", "context-menu-firewall")]
    [InlineData("explorer-context-menu-toggle-extensions", "context-menu-toggle-extensions")]
    [InlineData("explorer-context-menu-windows-terminal", "context-menu-windows-terminal")]
    [InlineData("explorer-context-menu-ask-copilot", "context-menu-ask-copilot")]
    [InlineData("explorer-context-menu-sfc", "context-menu-sfc")]
    [InlineData("explorer-context-menu-dism", "context-menu-dism")]
    [InlineData("explorer-context-menu-chkdsk", "context-menu-chkdsk")]
    [InlineData("explorer-context-menu-ps1-edit-run", "context-menu-ps1-edit-run")]
    [InlineData("explorer-context-menu-compress-to", "context-menu-compress-to")]
    public void Normalize_MapsRetiredContextMenuId_ToItsOwnPage(string retired, string canonical)
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
        foreach (var win10Id in RetiredSources)
        {
            var canonical = SettingIdAliases.Normalize(win10Id);
            Assert.NotEqual(win10Id, canonical);
            Assert.Contains(canonical, catalogIds);
        }
    }

    [Fact]
    public void EveryAliasSource_IsAbsentFromTheCatalog()
    {
        // Retired ids are merged or moved away; if one reappears as a real catalog id the alias is wrong.
        var catalogIds = SettingCatalog.All.Select(s => s.Id).ToHashSet();
        foreach (var win10Id in RetiredSources)
            Assert.DoesNotContain(win10Id, catalogIds);
    }

    private static readonly string[] RetiredSources =
    {
        "explorer-customization-context-menu",
        "explorer-take-ownership",
        "explorer-context-menu-firewall",
        "explorer-context-menu-toggle-extensions",
        "explorer-context-menu-windows-terminal",
        "explorer-context-menu-ask-copilot",
        "explorer-context-menu-sfc",
        "explorer-context-menu-dism",
        "explorer-context-menu-chkdsk",
        "explorer-context-menu-ps1-edit-run",
        "explorer-context-menu-compress-to",
        "explorer-customization-thispc-folder-desktop-win10",
        "explorer-customization-thispc-folder-documents-win10",
        "explorer-customization-thispc-folder-downloads-win10",
        "explorer-customization-thispc-folder-music-win10",
        "explorer-customization-thispc-folder-pictures-win10",
        "explorer-customization-thispc-folder-videos-win10",
    };
}
