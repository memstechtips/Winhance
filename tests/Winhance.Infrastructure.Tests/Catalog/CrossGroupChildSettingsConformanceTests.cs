using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

// Exactly ONE catalog Setting carries a cross-group child map (the ads/promotional master); every other carries
// null, never an empty dictionary.
public class CrossGroupChildSettingsConformanceTests
{
    private const string MasterId = "privacy-ads-promotional-master";

    [Fact]
    public void CrossGroupChildSettings_CatalogPinsTheOneCrossGroupMap()
    {
        var carriers = SettingCatalog.All.Where(s => s.Display.CrossGroupChildSettings is not null).ToList();
        var only = Assert.Single(carriers);
        Assert.Equal(MasterId, only.Id);

        var map = only.Display.CrossGroupChildSettings!;
        Assert.Equal(8, map.Count);
        Assert.Equal("Setting_privacy-ads-promotional-master_Child_Spotlight", map["privacy-rotating-lock-screen"]);
        Assert.Equal("Setting_privacy-ads-promotional-master_Child_StartSuggestions", map["start-show-suggestions"]);
    }
}
