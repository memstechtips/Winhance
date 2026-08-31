using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class LegacyCapabilityServiceTests
{
    private readonly Mock<IServicingSession> _session = new();
    private IReadOnlyList<string>? _statements;
    private string? _label;

    public LegacyCapabilityServiceTests()
    {
        _session
            .Setup(x => x.RunAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<string>, string, IProgress<TaskProgressDetail>?, CancellationToken>(
                (statements, label, _, _) => { _statements = statements; _label = label; })
            .ReturnsAsync(true);
    }

    private LegacyCapabilityService CreateSut() => new(_session.Object);

    [Fact]
    public void BuildEnableStatement_ThreeCapabilities_IsOneCallPerName()
    {
        var statement = CreateSut().BuildEnableStatement(
            ["OpenSSH.Client", "OpenSSH.Server", "App.StepsRecorder"]);

        statement.Should().Be(
            "Add-WindowsCapability -Online -Name 'OpenSSH.Client'; " +
            "Add-WindowsCapability -Online -Name 'OpenSSH.Server'; " +
            "Add-WindowsCapability -Online -Name 'App.StepsRecorder'");
    }

    [Fact]
    public void BuildEnableStatement_NameWithApostrophe_DoublesItForPowerShell()
    {
        var statement = CreateSut().BuildEnableStatement(["Some'Capability"]);

        statement.Should().Contain("-Name 'Some''Capability'");
    }

    // Add-WindowsCapability has no -NoRestart parameter (Get-Command, DISM module 3.0 on build 26100).
    // Adding it to match the feature statement would fail the command at runtime.
    [Fact]
    public void BuildEnableStatement_DoesNotPassNoRestart()
    {
        var statement = CreateSut().BuildEnableStatement(["OpenSSH.Client"]);

        statement.Should().NotContain("-NoRestart");
    }

    [Fact]
    public async Task EnableCapabilitiesAsync_RunsOneSessionCarryingTheStatement()
    {
        var sut = CreateSut();

        var launched = await sut.EnableCapabilitiesAsync(["OpenSSH.Client", "OpenSSH.Server"]);

        launched.Should().BeTrue();
        _session.Verify(
            x => x.RunAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _statements.Should().ContainSingle()
            .Which.Should().Be(sut.BuildEnableStatement(["OpenSSH.Client", "OpenSSH.Server"]));
    }

    [Fact]
    public async Task EnableCapabilitiesAsync_LabelNamesEveryCapabilityByDisplayName()
    {
        await CreateSut().EnableCapabilitiesAsync(
            ["OpenSSH.Client", "Media.WindowsMediaPlayer"],
            ["OpenSSH Client", "Windows Media Player"]);

        _label.Should().Be("OpenSSH Client, Windows Media Player");
    }

    [Fact]
    public async Task EnableCapabilitiesAsync_EmptyList_StartsNoSession()
    {
        var launched = await CreateSut().EnableCapabilitiesAsync([]);

        launched.Should().BeFalse();
        _session.Verify(
            x => x.RunAsync(It.IsAny<IReadOnlyList<string>>(), It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
