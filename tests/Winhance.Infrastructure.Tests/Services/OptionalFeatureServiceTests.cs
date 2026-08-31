using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class OptionalFeatureServiceTests
{
    private readonly Mock<IServicingSession> _session = new();
    private IReadOnlyList<string>? _statements;
    private string? _label;

    public OptionalFeatureServiceTests()
    {
        _session
            .Setup(x => x.RunAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, string, IProgress<TaskProgressDetail>?, CancellationToken>(
                (statements, label, _, _) => { _statements = statements; _label = label; })
            .ReturnsAsync(true);
    }

    private OptionalFeatureService CreateSut() => new(_session.Object);

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

    [Fact]
    public async Task EnableFeaturesAsync_RunsOneSessionCarryingTheStatement()
    {
        var sut = CreateSut();

        var launched = await sut.EnableFeaturesAsync(["NetFx3", "Recall"]);

        launched.Should().BeTrue();
        _session.Verify(
            x => x.RunAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _statements.Should().ContainSingle()
            .Which.Should().Be(sut.BuildEnableStatement(["NetFx3", "Recall"]));
    }

    [Fact]
    public async Task EnableFeaturesAsync_LabelNamesEveryFeatureByDisplayName()
    {
        await CreateSut().EnableFeaturesAsync(["NetFx3", "Recall"], ["Legacy .NET", "Recall"]);

        _label.Should().Be("Legacy .NET, Recall");
    }

    [Fact]
    public async Task EnableFeaturesAsync_EmptyList_StartsNoSession()
    {
        var launched = await CreateSut().EnableFeaturesAsync([]);

        launched.Should().BeFalse();
        _session.Verify(
            x => x.RunAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
