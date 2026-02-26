using FluentAssertions;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;
using Xunit;
using static Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities.WinGetProgressParser;

namespace Winhance.Infrastructure.Tests.Utilities;

public class WinGetProgressParserTests
{
    // ──────────────────────────────────────────────
    //  TranslateLine
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void TranslateLine_NullOrWhitespace_ReturnsNull(string? input)
    {
        WinGetProgressParser.TranslateLine(input).Should().BeNull();
    }

    [Theory]
    [InlineData("InstallationDisclaimer1")]
    [InlineData("InstallationDisclaimer2")]
    [InlineData("PackageDependencies")]
    public void TranslateLine_SuppressedResourceKeys_ReturnsNull(string key)
    {
        WinGetProgressParser.TranslateLine(key).Should().BeNull();
    }

    [Theory]
    [InlineData("PackageRequiresDependencies", "Installing dependencies...")]
    [InlineData("InstallerHashVerified", "Hash verified")]
    [InlineData("ExtractingArchive", "Extracting archive...")]
    [InlineData("ExtractArchiveSucceeded", "Archive extracted")]
    [InlineData("InstallFlowStartingPackageInstall", "Installing...")]
    [InlineData("InstallFlowInstallSuccess", "Installation successful")]
    [InlineData("SourceOpenFailedSuggestion", "WinGet source unavailable")]
    [InlineData("UnexpectedErrorExecutingCommand", "Unexpected error")]
    [InlineData("InstallingDependencies", "Installing dependencies...")]
    public void TranslateLine_MappedResourceKeys_ReturnsTranslation(string key, string expected)
    {
        WinGetProgressParser.TranslateLine(key).Should().Be(expected);
    }

    [Fact]
    public void TranslateLine_ResourceKeyLookup_IsCaseInsensitive()
    {
        WinGetProgressParser.TranslateLine("installerhashverified").Should().Be("Hash verified");
        WinGetProgressParser.TranslateLine("INSTALLERHASHVERIFIED").Should().Be("Hash verified");
    }

    [Fact]
    public void TranslateLine_StepPrefix_PreservedWithTranslation()
    {
        var result = WinGetProgressParser.TranslateLine("(1/2) PackageRequiresDependencies");
        result.Should().Be("[1/2] Installing dependencies...");
    }

    [Fact]
    public void TranslateLine_StepPrefix_SuppressedResourceKey_ReturnsNull()
    {
        var result = WinGetProgressParser.TranslateLine("(1/3) InstallationDisclaimer1");
        result.Should().BeNull();
    }

    [Fact]
    public void TranslateLine_StepPrefix_PlainText_PreservedWithBrackets()
    {
        var result = WinGetProgressParser.TranslateLine("(2/3) Some normal text");
        result.Should().Be("[2/3] Some normal text");
    }

    [Fact]
    public void TranslateLine_DownloadUrl_ShowsFilenameOnly()
    {
        var line = "Downloading https://example.com/packages/installer-v1.2.3.exe";
        var result = WinGetProgressParser.TranslateLine(line);
        result.Should().Be("Downloading installer-v1.2.3.exe...");
    }

    [Fact]
    public void TranslateLine_DownloadUrl_NoFilename_ReturnsFallback()
    {
        // A URL with no path segment filename falls through to generic message
        var line = "Downloading https://example.com/";
        var result = WinGetProgressParser.TranslateLine(line);
        result.Should().Be("Downloading...");
    }

    [Fact]
    public void TranslateLine_ReportIdentityFound_TranslatedToFriendlyFormat()
    {
        var line = "ReportIdentityFound Google Chrome [Google.Chrome] ShowVersion 120.0.1";
        var result = WinGetProgressParser.TranslateLine(line);
        result.Should().Be("Found: Google Chrome v120.0.1");
    }

    [Fact]
    public void TranslateLine_HexErrorCode_Preserved()
    {
        var line = "0x8a15000f : Something went wrong";
        var result = WinGetProgressParser.TranslateLine(line);
        result.Should().Be("Error: Something went wrong");
    }

    [Fact]
    public void TranslateLine_NormalText_PassedThrough()
    {
        var line = "Some random winget output text";
        var result = WinGetProgressParser.TranslateLine(line);
        result.Should().Be("Some random winget output text");
    }

    [Fact]
    public void TranslateLine_LeadingWhitespace_IsTrimmed()
    {
        var line = "   Some random winget output text   ";
        var result = WinGetProgressParser.TranslateLine(line);
        result.Should().Be("Some random winget output text");
    }

    [Fact]
    public void TranslateLine_DependencyDetailLine_IsTrimmedAndPassedThrough()
    {
        // Lines starting with "  - " get trimmed before the StartsWith check,
        // so they fall through to "return trimmed" (the trimmed value).
        var line = "  - Microsoft.VCRedist.2015+.x64";
        var result = WinGetProgressParser.TranslateLine(line);
        result.Should().Be("- Microsoft.VCRedist.2015+.x64");
    }

    // ──────────────────────────────────────────────
    //  ParseLine
    // ──────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void ParseLine_NullOrWhitespace_ReturnsNull(string? input)
    {
        WinGetProgressParser.ParseLine(input).Should().BeNull();
    }

    [Fact]
    public void ParseLine_FoundPhrase_ReturnsFoundPhase()
    {
        var result = WinGetProgressParser.ParseLine("Found Google Chrome [Google.Chrome]");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Found);
        result.Percent.Should().BeNull();
    }

    [Fact]
    public void ParseLine_PackageFound_ReturnsFoundPhase()
    {
        var result = WinGetProgressParser.ParseLine("package found: Google Chrome");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Found);
    }

    [Fact]
    public void ParseLine_ReportIdentityFoundKey_ReturnsFoundPhase()
    {
        var result = WinGetProgressParser.ParseLine("ReportIdentityFound Chrome [Google.Chrome] ShowVersion 120.0");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Found);
    }

    [Fact]
    public void ParseLine_DownloadWithPercentage_ReturnsDownloadingPhaseWithPercent()
    {
        var result = WinGetProgressParser.ParseLine("Downloading 52%");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Downloading);
        result.Percent.Should().Be(52);
    }

    [Fact]
    public void ParseLine_ProgressBar_ReturnsDownloadingPhaseWithPercent()
    {
        var result = WinGetProgressParser.ParseLine("██████████████████████████████  100%");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Downloading);
        result.Percent.Should().Be(100);
    }

    [Fact]
    public void ParseLine_PercentWithDecimal_ParsesCorrectly()
    {
        var result = WinGetProgressParser.ParseLine("Download progress: 52.3%");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Downloading);
        result.Percent.Should().Be(52.3);
    }

    [Fact]
    public void ParseLine_ByteProgress_CalculatesPercentage()
    {
        var result = WinGetProgressParser.ParseLine("1.2 MB / 2.4 MB");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Downloading);
        result.Percent.Should().Be(50);
    }

    [Fact]
    public void ParseLine_ByteProgressGB_CalculatesPercentage()
    {
        var result = WinGetProgressParser.ParseLine("0.5 GB / 1.0 GB");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Downloading);
        result.Percent.Should().Be(50);
    }

    [Fact]
    public void ParseLine_InstallingKeyword_ReturnsInstallingPhase()
    {
        var result = WinGetProgressParser.ParseLine("Installing package...");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Installing);
        result.Percent.Should().BeNull();
    }

    [Fact]
    public void ParseLine_StartingPackageInstall_ReturnsInstallingPhase()
    {
        var result = WinGetProgressParser.ParseLine("Starting package install...");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Installing);
    }

    [Fact]
    public void ParseLine_InstallFlowStartingPackageInstallKey_ReturnsInstallingPhase()
    {
        var result = WinGetProgressParser.ParseLine("InstallFlowStartingPackageInstall");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Installing);
    }

    [Fact]
    public void ParseLine_InstallerHashVerifiedKey_ReturnsInstallingPhase()
    {
        var result = WinGetProgressParser.ParseLine("InstallerHashVerified");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Installing);
    }

    [Fact]
    public void ParseLine_ExtractingArchiveKey_ReturnsInstallingPhase()
    {
        var result = WinGetProgressParser.ParseLine("ExtractingArchive");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Installing);
    }

    [Fact]
    public void ParseLine_ExtractArchiveSucceededKey_ReturnsInstallingPhase()
    {
        var result = WinGetProgressParser.ParseLine("ExtractArchiveSucceeded");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Installing);
    }

    [Fact]
    public void ParseLine_InstallWithPercentage_ReturnsInstallingPhaseWithPercent()
    {
        var result = WinGetProgressParser.ParseLine("Installing 75%");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Installing);
        result.Percent.Should().Be(75);
    }

    [Fact]
    public void ParseLine_UninstallingKeyword_ReturnsUninstallingPhase()
    {
        var result = WinGetProgressParser.ParseLine("Uninstalling package...");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Uninstalling);
        result.Percent.Should().BeNull();
    }

    [Fact]
    public void ParseLine_SuccessfullyInstalled_ReturnsCompletePhase()
    {
        var result = WinGetProgressParser.ParseLine("Successfully installed");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Complete);
        result.Percent.Should().Be(100);
    }

    [Fact]
    public void ParseLine_InstallationSuccessful_ReturnsCompletePhase()
    {
        var result = WinGetProgressParser.ParseLine("Installation successful");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Complete);
        result.Percent.Should().Be(100);
    }

    [Fact]
    public void ParseLine_SuccessfullyUninstalled_ReturnsCompletePhase()
    {
        var result = WinGetProgressParser.ParseLine("Successfully uninstalled");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Complete);
        result.Percent.Should().Be(100);
    }

    [Fact]
    public void ParseLine_UninstallSuccessful_ReturnsCompletePhase()
    {
        var result = WinGetProgressParser.ParseLine("Uninstall successful");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Complete);
        result.Percent.Should().Be(100);
    }

    [Fact]
    public void ParseLine_InstallFlowInstallSuccessKey_ReturnsCompletePhase()
    {
        var result = WinGetProgressParser.ParseLine("InstallFlowInstallSuccess");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Complete);
        result.Percent.Should().Be(100);
    }

    [Fact]
    public void ParseLine_NoApplicableInstaller_ReturnsErrorPhase()
    {
        var result = WinGetProgressParser.ParseLine("No applicable installer found");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Error);
        result.Percent.Should().BeNull();
    }

    [Theory]
    [InlineData("No package found matching input criteria")]
    [InlineData("No installed package found")]
    public void ParseLine_PackageFoundPhrases_ReturnFoundPhase(string line)
    {
        // These match "package found" before reaching the error check
        var result = WinGetProgressParser.ParseLine(line);
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Found);
    }

    [Fact]
    public void ParseLine_DownloadingWithoutPercent_ReturnsDownloadingWithNullPercent()
    {
        var result = WinGetProgressParser.ParseLine("Downloading...");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Downloading);
        result.Percent.Should().BeNull();
    }

    [Fact]
    public void ParseLine_UnrecognizedLine_ReturnsNull()
    {
        var result = WinGetProgressParser.ParseLine("Some random unrecognized output");
        result.Should().BeNull();
    }

    [Fact]
    public void ParseLine_PercentWithoutPhaseContext_DefaultsToDownloading()
    {
        // A line with a percentage but no "download" or "install" keyword
        // defaults to Downloading phase
        var result = WinGetProgressParser.ParseLine("Progress: 45%");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Downloading);
        result.Percent.Should().Be(45);
    }

    // ──────────────────────────────────────────────
    //  WinGetProgressInfo record
    // ──────────────────────────────────────────────

    [Fact]
    public void WinGetProgressInfo_RecordEquality_WorksCorrectly()
    {
        var a = new WinGetProgressInfo(WinGetPhase.Downloading, 50);
        var b = new WinGetProgressInfo(WinGetPhase.Downloading, 50);
        a.Should().Be(b);
    }

    [Fact]
    public void WinGetProgressInfo_RecordInequality_WorksCorrectly()
    {
        var a = new WinGetProgressInfo(WinGetPhase.Downloading, 50);
        var b = new WinGetProgressInfo(WinGetPhase.Installing, 50);
        a.Should().NotBe(b);
    }
}
