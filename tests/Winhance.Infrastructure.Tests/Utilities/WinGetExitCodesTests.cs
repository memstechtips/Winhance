using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;
using Xunit;

namespace Winhance.Infrastructure.Tests.Utilities;

public class WinGetExitCodesTests
{
    [Theory]
    [InlineData(WinGetExitCodes.Ok)]
    [InlineData(WinGetExitCodes.RestartRequired)]
    [InlineData(WinGetExitCodes.AlreadyInstalled)]
    [InlineData(WinGetExitCodes.UpdateNotApplicable)]
    public void IsSuccess_WithSuccessCodes_ReturnsTrue(int exitCode)
    {
        WinGetExitCodes.IsSuccess(exitCode).Should().BeTrue();
    }

    [Theory]
    [InlineData(WinGetExitCodes.PackageNotFound)]
    [InlineData(WinGetExitCodes.DownloadError)]
    [InlineData(WinGetExitCodes.BlockedByPolicy)]
    [InlineData(WinGetExitCodes.NetworkError)]
    [InlineData(1)]
    [InlineData(-1)]
    public void IsSuccess_WithFailureCodes_ReturnsFalse(int exitCode)
    {
        WinGetExitCodes.IsSuccess(exitCode).Should().BeFalse();
    }

    [Fact]
    public void IsUninstallVerifiable_ExecUninstallCommandFailed_ReturnsTrue()
    {
        WinGetExitCodes.IsUninstallVerifiable(WinGetExitCodes.ExecUninstallCommandFailed)
            .Should().BeTrue();
    }

    [Fact]
    public void IsUninstallVerifiable_NoUninstallInfoFound_ReturnsTrue()
    {
        WinGetExitCodes.IsUninstallVerifiable(WinGetExitCodes.NoUninstallInfoFound)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(WinGetExitCodes.Ok)]
    [InlineData(WinGetExitCodes.PackageNotFound)]
    [InlineData(1)]
    public void IsUninstallVerifiable_OtherCodes_ReturnsFalse(int exitCode)
    {
        WinGetExitCodes.IsUninstallVerifiable(exitCode).Should().BeFalse();
    }

    [Theory]
    [InlineData(WinGetExitCodes.Ok, InstallFailureReason.None)]
    [InlineData(WinGetExitCodes.RestartRequired, InstallFailureReason.None)]
    [InlineData(WinGetExitCodes.AlreadyInstalled, InstallFailureReason.None)]
    [InlineData(WinGetExitCodes.PackageNotFound, InstallFailureReason.PackageNotFound)]
    [InlineData(WinGetExitCodes.PackageNotInstalled, InstallFailureReason.PackageNotFound)]
    [InlineData(WinGetExitCodes.DownloadError, InstallFailureReason.DownloadError)]
    [InlineData(WinGetExitCodes.NetworkError, InstallFailureReason.NetworkError)]
    [InlineData(WinGetExitCodes.BlockedByPolicy, InstallFailureReason.BlockedByPolicy)]
    [InlineData(WinGetExitCodes.NoApplicableInstallers, InstallFailureReason.NoApplicableInstallers)]
    [InlineData(WinGetExitCodes.PackageAgreementsNotAccepted, InstallFailureReason.AgreementsNotAccepted)]
    [InlineData(WinGetExitCodes.InstallerHashMismatch, InstallFailureReason.HashMismatchOrInstallError)]
    [InlineData(WinGetExitCodes.OperationCancelled, InstallFailureReason.UserCancelled)]
    [InlineData(WinGetExitCodes.ManifestError, InstallFailureReason.Other)]
    public void MapExitCode_ReturnsCorrectReason(int exitCode, InstallFailureReason expected)
    {
        WinGetExitCodes.MapExitCode(exitCode).Should().Be(expected);
    }

    [Fact]
    public void MapExitCode_UnknownCode_ReturnsOther()
    {
        WinGetExitCodes.MapExitCode(99999).Should().Be(InstallFailureReason.Other);
    }
}
