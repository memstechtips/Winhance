using FluentAssertions;
using Winhance.UI.Features.Common.Extensions.DI;
using Xunit;

namespace Winhance.UI.Tests.DI;

// Builds the exact host the app builds (CompositionRoot forces ValidateOnBuild + ValidateScopes on), so a service
// registered with a dependency nothing provides fails here instead of refusing to start on a user's machine.
public class WinhanceHostSmokeTests
{
    [Fact]
    public void ProductionHost_BuildsWithFullGraphValidation()
    {
        var build = () =>
        {
            using var host = CompositionRoot.CreateWinhanceHost().Build();
        };

        build.Should().NotThrow();
    }
}
