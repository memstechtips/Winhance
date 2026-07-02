using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>The pairing primitive SettingCatalog.Find - the O(1) lookup every SettingDefinition-retirement
/// reader-port uses to resolve a def id to its catalog Setting. Proves canonical + -win10 alias + miss.</summary>
public class SettingCatalogFindTests
{
    [Fact]
    public void Find_resolves_canonical_alias_and_miss()
    {
        // a known canonical setting resolves to itself
        var canonical = "explorer-customization-thispc-folder-desktop";
        var s = SettingCatalog.Find(canonical);
        Assert.NotNull(s);
        Assert.Equal(canonical, s!.Id);

        // its retired -win10 alias resolves to the SAME (merged canonical) setting
        Assert.Same(s, SettingCatalog.Find(canonical + "-win10"));

        // an unknown id is a miss, not a throw
        Assert.Null(SettingCatalog.Find("definitely-not-a-real-setting-id"));

        // every catalog setting is findable by its own id (ById covers All)
        Assert.All(SettingCatalog.All, x => Assert.Same(x, SettingCatalog.Find(x.Id)));
        Assert.Equal(SettingCatalog.All.Count, SettingCatalog.ById.Count);
    }
}
