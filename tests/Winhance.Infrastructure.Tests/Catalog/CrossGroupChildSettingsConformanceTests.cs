using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

/// <summary>Pins <see cref="Display.CrossGroupChildSettings"/> (child setting id -> localization key, the source
/// the UI's cross-group info banner is built from) on the CATALOG side alone. Exactly ONE catalog Setting carries a
/// cross-group child map - the ads/promotional master, with 8 entries and the known pairs; every other Setting
/// carries null, never an empty dictionary.
///
/// Machine-independent: compiled objects only, no I/O. Run: dotnet test --filter CrossGroupChildSettingsConformance</summary>
public class CrossGroupChildSettingsConformanceTests
{
    private const string MasterId = "privacy-ads-promotional-master";

    /// <summary>Exactly one catalog Setting carries a cross-group child map; it is the ads/promotional master,
    /// with 8 entries and the known first/last pairs.</summary>
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
