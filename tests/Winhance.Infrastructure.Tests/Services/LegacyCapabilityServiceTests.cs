using FluentAssertions;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class LegacyCapabilityServiceTests
{
    private LegacyCapabilityService CreateSut() => new();

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
}
