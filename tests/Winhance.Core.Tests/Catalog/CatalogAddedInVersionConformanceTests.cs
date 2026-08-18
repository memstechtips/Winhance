using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

// A contributor typo in AddedInVersion silently breaks the badge and nothing else catches it (CatalogValidator does not check this).
public class CatalogAddedInVersionConformanceTests
{
    [Fact]
    public void Every_catalog_AddedInVersion_parses_as_System_Version()
    {
        var withVersion = SettingCatalog.All
            .Where(s => !string.IsNullOrWhiteSpace(s.Display.AddedInVersion))
            .ToList();

        // Non-vacuity: the badge system ships version-tagged settings.
        withVersion.Should().NotBeEmpty();

        var failures = new List<string>();
        foreach (var s in withVersion)
        {
            var normalised = s.Display.AddedInVersion!.Trim().TrimStart('v');
            if (!System.Version.TryParse(normalised, out _))
                failures.Add($"{s.Id} AddedInVersion=\"{s.Display.AddedInVersion}\" is not parseable");
        }

        failures.Should().BeEmpty(
            "every catalog Display.AddedInVersion must parse as System.Version (YY.MM.DD, optional leading 'v')");
    }
}
