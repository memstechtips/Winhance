using FluentAssertions;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;
using Xunit;

namespace Winhance.Infrastructure.Tests.Utilities;

public class WinGetExitCodesTests
{
    [Theory]
    [InlineData(WinGetExitCodes.Ok)]
    [InlineData(WinGetExitCodes.RebootRequiredToFinish)]
    [InlineData(WinGetExitCodes.PackageAlreadyInstalled)]
    [InlineData(WinGetExitCodes.UpdateNotApplicable)]
    public void IsSuccess_WithSuccessCodes_ReturnsTrue(int exitCode)
    {
        WinGetExitCodes.IsSuccess(exitCode).Should().BeTrue();
    }

    [Theory]
    [InlineData(WinGetExitCodes.PackageNotFound)]
    [InlineData(WinGetExitCodes.DownloadFailed)]
    [InlineData(WinGetExitCodes.BlockedByPolicy)]
    [InlineData(WinGetExitCodes.NoNetwork)]
    [InlineData(WinGetExitCodes.MultiplePackagesFound)]
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
    [InlineData(WinGetExitCodes.RebootRequiredToFinish, InstallFailureReason.None)]
    [InlineData(WinGetExitCodes.PackageAlreadyInstalled, InstallFailureReason.None)]
    [InlineData(WinGetExitCodes.PackageNotFound, InstallFailureReason.PackageNotFound)]
    [InlineData(WinGetExitCodes.DownloadFailed, InstallFailureReason.DownloadError)]
    [InlineData(WinGetExitCodes.NoNetwork, InstallFailureReason.NetworkError)]
    [InlineData(WinGetExitCodes.BlockedByPolicy, InstallFailureReason.BlockedByPolicy)]
    [InlineData(WinGetExitCodes.NoApplicableInstallers, InstallFailureReason.NoApplicableInstallers)]
    [InlineData(WinGetExitCodes.PackageAgreementsNotAccepted, InstallFailureReason.AgreementsNotAccepted)]
    [InlineData(WinGetExitCodes.InstallerHashMismatch, InstallFailureReason.HashMismatchOrInstallError)]
    [InlineData(WinGetExitCodes.InstallerFailed, InstallFailureReason.HashMismatchOrInstallError)]
    [InlineData(WinGetExitCodes.CancelledByUser, InstallFailureReason.UserCancelled)]
    [InlineData(WinGetExitCodes.MultiplePackagesFound, InstallFailureReason.Other)]
    public void MapExitCode_ReturnsCorrectReason(int exitCode, InstallFailureReason expected)
    {
        WinGetExitCodes.MapExitCode(exitCode).Should().Be(expected);
    }

    // Raw values and names from winget-cli's doc/windows/package-manager/winget/returnCodes.md; the theory above
    // names the constants, so a mistyped one would pass it.
    [Theory]
    [InlineData(unchecked((int)0x8A150005), InstallFailureReason.UserCancelled)] // CTRL_SIGNAL_RECEIVED
    [InlineData(unchecked((int)0x8A150006), InstallFailureReason.HashMismatchOrInstallError)] // SHELLEXEC_INSTALL_FAILED
    [InlineData(unchecked((int)0x8A150007), InstallFailureReason.Other)] // UNSUPPORTED_MANIFESTVERSION
    [InlineData(unchecked((int)0x8A150008), InstallFailureReason.DownloadError)] // DOWNLOAD_FAILED
    [InlineData(unchecked((int)0x8A15000B), InstallFailureReason.Other)] // SOURCES_INVALID
    [InlineData(unchecked((int)0x8A15000E), InstallFailureReason.Other)] // PACKAGE_IS_BUNDLE
    [InlineData(unchecked((int)0x8A150010), InstallFailureReason.NoApplicableInstallers)] // NO_APPLICABLE_INSTALLER
    [InlineData(unchecked((int)0x8A150011), InstallFailureReason.HashMismatchOrInstallError)] // INSTALLER_HASH_MISMATCH
    [InlineData(unchecked((int)0x8A150013), InstallFailureReason.Other)] // SOURCE_ARG_ALREADY_EXISTS
    [InlineData(unchecked((int)0x8A150014), InstallFailureReason.PackageNotFound)] // NO_APPLICATIONS_FOUND
    [InlineData(unchecked((int)0x8A150015), InstallFailureReason.Other)] // NO_SOURCES_DEFINED
    [InlineData(unchecked((int)0x8A150016), InstallFailureReason.Other)] // MULTIPLE_APPLICATIONS_FOUND
    [InlineData(unchecked((int)0x8A150019), InstallFailureReason.Other)] // COMMAND_REQUIRES_ADMIN
    [InlineData(unchecked((int)0x8A15001B), InstallFailureReason.BlockedByPolicy)] // MSSTORE_BLOCKED_BY_POLICY
    [InlineData(unchecked((int)0x8A15001C), InstallFailureReason.BlockedByPolicy)] // MSSTORE_APP_BLOCKED_BY_POLICY
    [InlineData(unchecked((int)0x8A15002A), InstallFailureReason.Other)] // INVALID_MANIFEST
    [InlineData(unchecked((int)0x8A15002B), InstallFailureReason.None)] // UPDATE_NOT_APPLICABLE
    [InlineData(unchecked((int)0x8A15003A), InstallFailureReason.BlockedByPolicy)] // BLOCKED_BY_POLICY
    [InlineData(unchecked((int)0x8A150041), InstallFailureReason.AgreementsNotAccepted)] // PACKAGE_AGREEMENTS_NOT_ACCEPTED
    [InlineData(unchecked((int)0x8A150046), InstallFailureReason.AgreementsNotAccepted)] // SOURCE_AGREEMENTS_NOT_ACCEPTED
    [InlineData(unchecked((int)0x8A150061), InstallFailureReason.None)] // PACKAGE_ALREADY_INSTALLED
    [InlineData(unchecked((int)0x8A150086), InstallFailureReason.DownloadError)] // INSTALLER_ZERO_BYTE_FILE
    [InlineData(unchecked((int)0x8A150107), InstallFailureReason.NetworkError)] // INSTALL_NO_NETWORK
    [InlineData(unchecked((int)0x8A150109), InstallFailureReason.None)] // INSTALL_REBOOT_REQUIRED_TO_FINISH
    [InlineData(unchecked((int)0x8A15010A), InstallFailureReason.Other)] // INSTALL_REBOOT_REQUIRED_FOR_INSTALL
    [InlineData(unchecked((int)0x8A15010B), InstallFailureReason.None)] // INSTALL_REBOOT_INITIATED
    [InlineData(unchecked((int)0x8A15010C), InstallFailureReason.UserCancelled)] // INSTALL_CANCELLED_BY_USER
    [InlineData(unchecked((int)0x8A15010F), InstallFailureReason.BlockedByPolicy)] // INSTALL_BLOCKED_BY_POLICY
    public void MapExitCode_RawHResult_ReturnsItsReason(int exitCode, InstallFailureReason expected)
    {
        WinGetExitCodes.MapExitCode(exitCode).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(unchecked((int)0x8A15002B), true)] // UPDATE_NOT_APPLICABLE
    [InlineData(unchecked((int)0x8A150061), true)] // PACKAGE_ALREADY_INSTALLED
    [InlineData(unchecked((int)0x8A150109), true)] // INSTALL_REBOOT_REQUIRED_TO_FINISH
    [InlineData(unchecked((int)0x8A15010B), true)] // INSTALL_REBOOT_INITIATED
    [InlineData(unchecked((int)0x8A150015), false)] // NO_SOURCES_DEFINED
    [InlineData(unchecked((int)0x8A150019), false)] // COMMAND_REQUIRES_ADMIN
    [InlineData(unchecked((int)0x8A15002A), false)] // INVALID_MANIFEST
    [InlineData(unchecked((int)0x8A15010A), false)] // INSTALL_REBOOT_REQUIRED_FOR_INSTALL
    public void IsSuccess_RawHResult_MatchesWhatTheCodeMeans(int exitCode, bool expected)
    {
        WinGetExitCodes.IsSuccess(exitCode).Should().Be(expected);
    }

    [Fact]
    public void MapExitCode_UnknownCode_ReturnsOther()
    {
        WinGetExitCodes.MapExitCode(99999).Should().Be(InstallFailureReason.Other);
    }
}
