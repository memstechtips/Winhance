using System.Text.Json;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities;
using Winhance.TestSupport;
using Xunit;
using static Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet.Utilities.WinGetProgressParser;

namespace Winhance.Infrastructure.Tests.Utilities;

public class WinGetProgressParserTests
{
    private static readonly ILocalizationService Localization = KeyEchoingLocalization();

    // Echoes the key and its arguments, so a test sees which string the parser asked for.
    private static ILocalizationService KeyEchoingLocalization()
    {
        var localization = new Mock<ILocalizationService>();
        localization.Setup(l => l.GetString(It.IsAny<string>())).Returns<string>(key => $"<{key}>");
        localization.Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns<string, object[]>((key, args) => $"<{key}:{string.Join("|", args)}>");
        return localization.Object;
    }

    private static string? TranslateLine(string? line) => WinGetProgressParser.TranslateLine(line, Localization);

    // The map's values are keys the parser builds from winget's output, which the static key gate cannot see.
    [Fact]
    public void ResourceKeyMap_EveryKeyItMapsTo_IsInEnglish()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoPaths.LocalizationDir(), "en.json")));
        var english = doc.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();

        WinGetProgressParser.ResourceKeyMap.Values
            .Where(key => key.Length > 0 && !english.Contains(key))
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("UninstallFlowStartingPackageUninstall", "<Progress_WinGetLine_Uninstalling>")]
    [InlineData("UninstallFlowUninstallSuccess", "<Progress_WinGetLine_UninstallSuccess>")]
    [InlineData("NoInstalledPackageFound", "<Progress_WinGetLine_NoInstalledPackage>")]
    public void TranslateLine_UninstallResourceKeys_ReadAsLocalizedText(string key, string expected)
    {
        TranslateLine(key).Should().Be(expected);
    }

    [Fact]
    public void ParseLine_UninstallFlowKeys_DrivePhases()
    {
        WinGetProgressParser.ParseLine("UninstallFlowStartingPackageUninstall")!.Phase.Should().Be(WinGetPhase.Uninstalling);
        WinGetProgressParser.ParseLine("UninstallFlowUninstallSuccess").Should().Be(new WinGetProgressInfo(WinGetPhase.Complete, 100));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void TranslateLine_NullOrWhitespace_ReturnsNull(string? input)
    {
        TranslateLine(input).Should().BeNull();
    }

    [Theory]
    [InlineData("InstallationDisclaimer1")]
    [InlineData("InstallationDisclaimer2")]
    [InlineData("PackageDependencies")]
    public void TranslateLine_SuppressedResourceKeys_ReturnsNull(string key)
    {
        TranslateLine(key).Should().BeNull();
    }

    [Theory]
    [InlineData("PackageRequiresDependencies", "<Progress_WinGetLine_InstallingDependencies>")]
    [InlineData("InstallerHashVerified", "<Progress_WinGetLine_HashVerified>")]
    [InlineData("ExtractingArchive", "<Progress_WinGetLine_ExtractingArchive>")]
    [InlineData("ExtractArchiveSucceeded", "<Progress_WinGetLine_ArchiveExtracted>")]
    [InlineData("InstallFlowStartingPackageInstall", "<Progress_WinGetLine_Installing>")]
    [InlineData("InstallFlowInstallSuccess", "<Progress_WinGetLine_InstallSuccess>")]
    [InlineData("SourceOpenFailedSuggestion", "<Progress_WinGetLine_SourceUnavailable>")]
    [InlineData("UnexpectedErrorExecutingCommand", "<Progress_WinGetLine_UnexpectedError>")]
    [InlineData("InstallingDependencies", "<Progress_WinGetLine_InstallingDependencies>")]
    public void TranslateLine_MappedResourceKeys_ReturnsTranslation(string key, string expected)
    {
        TranslateLine(key).Should().Be(expected);
    }

    [Fact]
    public void TranslateLine_ResourceKeyLookup_IsCaseInsensitive()
    {
        TranslateLine("installerhashverified").Should().Be("<Progress_WinGetLine_HashVerified>");
        TranslateLine("INSTALLERHASHVERIFIED").Should().Be("<Progress_WinGetLine_HashVerified>");
    }

    [Fact]
    public void TranslateLine_StepPrefix_PreservedWithTranslation()
    {
        var result = TranslateLine("(1/2) PackageRequiresDependencies");
        result.Should().Be("[1/2] <Progress_WinGetLine_InstallingDependencies>");
    }

    [Fact]
    public void TranslateLine_StepPrefix_SuppressedResourceKey_ReturnsNull()
    {
        var result = TranslateLine("(1/3) InstallationDisclaimer1");
        result.Should().BeNull();
    }

    [Fact]
    public void TranslateLine_StepPrefix_PlainText_PreservedWithBrackets()
    {
        var result = TranslateLine("(2/3) Some normal text");
        result.Should().Be("[2/3] Some normal text");
    }

    [Fact]
    public void TranslateLine_DownloadUrl_ShowsFilenameOnly()
    {
        var line = "Downloading https://example.com/packages/installer-v1.2.3.exe";
        var result = TranslateLine(line);
        result.Should().Be("<Progress_Downloading:installer-v1.2.3.exe>");
    }

    [Fact]
    public void TranslateLine_DownloadUrl_NoFilename_ReturnsFallback()
    {
        var line = "Downloading https://example.com/";
        var result = TranslateLine(line);
        result.Should().Be("<Progress_WinGetLine_Downloading>");
    }

    [Fact]
    public void TranslateLine_ReportIdentityFound_TranslatedToFriendlyFormat()
    {
        var line = "ReportIdentityFound Google Chrome [Google.Chrome] ShowVersion 120.0.1";
        var result = TranslateLine(line);
        result.Should().Be("<Progress_WinGetLine_Found:Google Chrome|120.0.1>");
    }

    [Fact]
    public void TranslateLine_HexErrorCode_Preserved()
    {
        var line = "0x8a15000f : Something went wrong";
        var result = TranslateLine(line);
        result.Should().Be("<Progress_WinGetLine_Error:Something went wrong>");
    }

    [Fact]
    public void TranslateLine_NormalText_PassedThrough()
    {
        var line = "Some random winget output text";
        var result = TranslateLine(line);
        result.Should().Be("Some random winget output text");
    }

    [Fact]
    public void TranslateLine_LeadingWhitespace_IsTrimmed()
    {
        var line = "   Some random winget output text   ";
        var result = TranslateLine(line);
        result.Should().Be("Some random winget output text");
    }

    [Fact]
    public void TranslateLine_DependencyDetailLine_IsTrimmedAndPassedThrough()
    {
        // Lines starting with "  - " get trimmed before the StartsWith check,
        // so they fall through to "return trimmed" (the trimmed value).
        var line = "  - Microsoft.VCRedist.2015+.x64";
        var result = TranslateLine(line);
        result.Should().Be("- Microsoft.VCRedist.2015+.x64");
    }

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
        var result = WinGetProgressParser.ParseLine("Progress: 45%");
        result.Should().NotBeNull();
        result!.Phase.Should().Be(WinGetPhase.Downloading);
        result.Percent.Should().Be(45);
    }

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

    [Fact]
    public void Forward_Line_ShowsTheTranslationAsARedrawingProgressLine()
    {
        var progress = new Mock<ITaskProgressService>();
        TaskProgressDetail? shown = null;
        progress.Setup(p => p.UpdateDetailedProgress(It.IsAny<TaskProgressDetail>()))
                .Callback<TaskProgressDetail>(detail => shown = detail);

        WinGetProgressParser.Forward("InstallFlowInstallSuccess", progress.Object, Localization, null);

        shown.Should().NotBeNull();
        shown!.TerminalOutput.Should().Be("<Progress_WinGetLine_InstallSuccess>");
        shown.IsProgressIndicator.Should().BeTrue();
    }

    // A progress line is decoration; whatever goes wrong showing it must not end the install.
    [Fact]
    public void Forward_ProgressServiceThrows_LogsAWarningInsteadOfThrowing()
    {
        var progress = new Mock<ITaskProgressService>();
        progress.Setup(p => p.UpdateDetailedProgress(It.IsAny<TaskProgressDetail>()))
                .Throws(new InvalidOperationException("dispatcher gone"));
        var log = new Mock<ILogService>();

        Action act = () => WinGetProgressParser.Forward("50%", progress.Object, Localization, log.Object);

        act.Should().NotThrow();
        log.Verify(l => l.LogWarning(It.Is<string>(m => m.Contains("dispatcher gone")), It.IsAny<string>()), Times.Once);
    }
}
