using FluentAssertions;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class OptionalFeatureServiceTests
{
    private OptionalFeatureService CreateSut() => new();

    [Fact]
    public void BuildEnableStatement_ThreeFeatures_IsOneCmdletCall()
    {
        var statement = CreateSut().BuildEnableStatement(
            ["NetFx3", "Microsoft-Hyper-V-All", "Containers-DisposableClientVM"]);

        statement.Should().Be(
            "Enable-WindowsOptionalFeature -Online -FeatureName " +
            "'NetFx3','Microsoft-Hyper-V-All','Containers-DisposableClientVM' -All -NoRestart");
    }

    [Fact]
    public void BuildEnableStatement_NameWithApostrophe_DoublesItForPowerShell()
    {
        var statement = CreateSut().BuildEnableStatement(["Some'Feature"]);

        statement.Should().Contain("-FeatureName 'Some''Feature' -All");
    }
}
