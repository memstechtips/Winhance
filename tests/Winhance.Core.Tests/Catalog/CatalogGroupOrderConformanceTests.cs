using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Localization;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

// A page draws its groups in the order the catalog first names them, so a group split into two runs reads as one
// group on screen and two in the file. Keeping each group in one run keeps the file in the order the page shows.
public class CatalogGroupOrderConformanceTests
{
    [Fact]
    public void ByFeature_EveryCatalog_KeepsEveryGroupInOneContiguousRun()
    {
        SettingCatalog.ByFeature.Should().NotBeEmpty();

        var failures = new List<string>();
        foreach (var (featureId, settings) in SettingCatalog.ByFeature)
        {
            settings.Should().NotBeEmpty(featureId);

            var closed = new HashSet<string>(StringComparer.Ordinal);
            string? current = null;
            foreach (var setting in settings)
            {
                var group = (setting.Display.GroupName ?? LocKey.SettingGroup.General).Value;
                if (group == current)
                    continue;
                if (current is not null)
                    closed.Add(current);
                if (closed.Contains(group))
                    failures.Add($"{featureId}: {setting.Id} reopens group {group}");
                current = group;
            }
        }

        failures.Should().BeEmpty();
    }
}
