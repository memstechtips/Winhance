using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

/// <summary>Contributor typo guard. Every catalog Setting's
/// <see cref="Display.AddedInVersion"/> - the string that drives the badge via INewBadgeService - must parse as
/// a System.Version (format YY.MM.DD, optional leading 'v'), or a contributor typo silently breaks the badge with no
/// other test catching it (CatalogValidator does not check this). Iterates the live catalog; machine-independent.</summary>
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
