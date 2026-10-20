using FluentAssertions;
using Winhance.Infrastructure.Extensions.DI;
using Xunit;

namespace Winhance.Infrastructure.Tests.Style;

// Winhance.UI composes Infrastructure through AddInfrastructureServices and otherwise consumes Core interfaces;
// nothing else in this assembly is API. A new public type here is a leak, not a convenience - test projects that
// need to new one up get InternalsVisibleTo in Winhance.Infrastructure.csproj instead.
// DesktopBackground is the one exception: the same source file is compiled again by PowerShell Add-Type inside
// the answer file, and a script can only reach a public type.
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

        exported.Should().Equal("Winhance.Desktop.DesktopBackground", typeof(InfrastructureServicesExtensions).FullName);
    }
}
