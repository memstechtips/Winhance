using FluentAssertions;
using Winhance.Infrastructure.Extensions.DI;
using Xunit;

namespace Winhance.Infrastructure.Tests.Style;

// Winhance.UI composes Infrastructure through AddInfrastructureServices and otherwise consumes Core interfaces;
// nothing else in this assembly is API. A new public type here is a leak, not a convenience - test projects that
// need to new one up get InternalsVisibleTo in Winhance.Infrastructure.csproj instead.
public class PublicSurfaceTests
{
    [Fact]
    public void Only_the_DI_entry_point_is_public()
    {
        var exported = typeof(InfrastructureServicesExtensions).Assembly
            .GetExportedTypes()
            .Where(t => t.Namespace?.StartsWith("Winhance.", StringComparison.Ordinal) == true)
            .Select(t => t.FullName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        exported.Should().Equal(typeof(InfrastructureServicesExtensions).FullName);
    }
}
