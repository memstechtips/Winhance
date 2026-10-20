using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.TestSupport;

// An unstubbed Moq registry answers null for GetById and GetFeatureIdForSetting whatever GetByFeature was told;
// the real registry answers all three from one catalog.
public static class CatalogRegistryMock
{
    // Looked up when the mock is called, so a GetByFeature setup added after this call is seen too.
    public static Mock<ICatalogSettingsRegistry> ResolveIdsFromFeatures(this Mock<ICatalogSettingsRegistry> mock)
    {
        mock.Setup(r => r.GetById(It.IsAny<string>(), It.IsAny<CatalogScope>()))
            .Returns((string id, CatalogScope _) => Owned(mock, id).Select(pair => pair.Setting).FirstOrDefault());
        mock.Setup(r => r.GetFeatureIdForSetting(It.IsAny<string>()))
            .Returns((string id) => Owned(mock, id).Select(pair => pair.FeatureId).FirstOrDefault());
        return mock;
    }

    private static IEnumerable<(string FeatureId, Setting Setting)> Owned(Mock<ICatalogSettingsRegistry> mock, string id) =>
        FeatureDefinitions.All
            .SelectMany(
                feature => mock.Object.GetByFeature(feature.Id, CatalogScope.CurrentMachine) ?? Array.Empty<Setting>(),
                (feature, setting) => (FeatureId: feature.Id, Setting: setting))
            .Where(pair => pair.Setting.Id == SettingIdAliases.Normalize(id));
}
