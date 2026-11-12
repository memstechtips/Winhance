using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.WimUtil.WimServices;

public class OfflineAppRemoverTests
{
    private const string Mount = @"D:\iso\WinhanceMount";
    private const string Software = @"HKEY_LOCAL_MACHINE\WH_SOFTWARE";
    private const string SystemHive = @"HKEY_LOCAL_MACHINE\WH_SYSTEM";
    private const string DefaultUser = @"HKEY_LOCAL_MACHINE\WH_DEFAULT";
    private const string Weather = "Microsoft.BingWeather_4.53.0.0_neutral_~_8wekyb3d8bbwe";

    private static readonly string Marker = Mount + "\\" + ScriptPaths.UnattendAppsRemovedMarker[3..];

    private readonly Mock<IDismProcessRunner> _dism = new();
    private readonly Mock<IFileSystemService> _files = new();
    private readonly Mock<IPowerShellRunner> _powerShell = new();
    private readonly Mock<IOfflineHiveLoader> _hives = new();
    private readonly Mock<IOfflineHiveSession> _session = new();
    private readonly Mock<ILogService> _log = new();
    private readonly List<string> _dismCalls = [];
    private readonly List<string> _scripts = [];
    private readonly List<string> _arguments = [];
    private readonly List<string> _deletedFolders = [];
    private readonly Dictionary<string, string> _written = [];
    private readonly Dictionary<string, string> _outputs = [];
    private Func<string, int> _exit = _ => 0;

    public OfflineAppRemoverTests()
    {
        _files.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => string.Join("\\", parts));
        _files.Setup(f => f.GetDirectoryName(It.IsAny<string>())).Returns((string p) => p[..p.LastIndexOf('\\')]);
        _files.Setup(f => f.GetTempPath()).Returns(@"C:\Temp");
        _files.Setup(f => f.DeleteDirectory(It.IsAny<string>(), true)).Callback((string p, bool _) => _deletedFolders.Add(p));
        _files.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string path, string text, CancellationToken _) => _written[path] = text)
            .Returns(Task.CompletedTask);
        _powerShell
            .Setup(r => r.RunScriptFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .Returns((string path, string args, IProgress<TaskProgressDetail>? _, CancellationToken _) =>
            {
                _scripts.Add(path);
                _arguments.Add(args);
                return Task.FromResult(string.Empty);
            });
        _session.SetupGet(s => s.SoftwareRoot).Returns(Software);
        _session.SetupGet(s => s.SystemRoot).Returns(SystemHive);
        _session.SetupGet(s => s.DefaultUserRoot).Returns(DefaultUser);
        _session.Setup(s => s.DisposeAsync()).Returns(ValueTask.CompletedTask);
        _hives.Setup(h => h.LoadAsync(Mount, It.IsAny<CancellationToken>())).ReturnsAsync(_session.Object);
        _dism
            .Setup(r => r.RunProcessWithProgressAsync("dism.exe", It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string args, IProgress<TaskProgressDetail>? _, CancellationToken _) =>
            {
                _dismCalls.Add(args);
                var output = _outputs.FirstOrDefault(o => args.Contains(o.Key)).Value ?? string.Empty;
                return Task.FromResult((_exit(args), output));
            });
    }

    private OfflineAppRemover Create() => new(_dism.Object, _files.Object, _powerShell.Object, _hives.Object, _log.Object);

    private static AppChoice Appx(string id, params string[] packages) => new(id, id, packages, null, null, null);

    private static AppChoice Capability(string id, string name) => new(id, id, null, name, null, null);

    private static AppChoice Feature(string id, string name) => new(id, id, null, null, name, null);

    private static SelectionSet Apps(params AppChoice[] apps) => new([], apps, []);

    private Task<Winhance.Core.Features.WimUtil.Models.OfflineStepOutcome> RunAsync(params AppChoice[] apps) =>
        Create().RemoveAsync(Mount, Apps(apps), null, CancellationToken.None);

    private static string ProvisionedListing(params string[] entries) =>
        "Image Version: 10.0.26100.1\r\n\r\nPackages listing:\r\n\r\n" + string.Join("\r\n", entries.Select(e =>
            $"DisplayName : {e[..e.IndexOf('_')]}\r\nVersion : 1.0\r\nArchitecture : neutral\r\nResourceId : ~\r\nPackageName : {e}\r\n"));

    [Fact]
    public async Task RemoveAsync_ProvisionedPackageNameMatchesExactly_RemovesOnlyThatPackage()
    {
        _outputs["/Get-ProvisionedAppxPackages"] = ProvisionedListing(
            Weather, "Microsoft.BingWeatherPlus_1.0.0.0_neutral_~_8wekyb3d8bbwe", "Microsoft.Other_1.0.0.0_neutral_~_8wekyb3d8bbwe");

        var outcome = await RunAsync(Appx("windows-app-weather", "Microsoft.BingWeather"));

        outcome.Succeeded.Should().BeTrue();
        outcome.Applied.Should().Be(1);
        _dismCalls.Should().Contain($"/Image:\"{Mount}\" /Remove-ProvisionedAppxPackage /PackageName:{Weather}");
        _dismCalls.Count(c => c.Contains("/Remove-ProvisionedAppxPackage")).Should().Be(1);
    }

    [Fact]
    public async Task RemoveAsync_ManyApps_ListsProvisionedPackagesOnce()
    {
        _outputs["/Get-ProvisionedAppxPackages"] = ProvisionedListing(Weather);

        await RunAsync(Appx("a", "Microsoft.BingWeather"), Appx("b", "Microsoft.Missing"));

        _dismCalls.Count(c => c.Contains("/Get-ProvisionedAppxPackages")).Should().Be(1);
    }

    [Fact]
    public async Task RemoveAsync_Capabilities_RemovesOnlyInstalledOnesByPrefix()
    {
        _outputs["/Get-Capabilities"] =
            "Capability Identity : Browser.InternetExplorer~~~~0.0.11.0\r\nState : Installed\r\n\r\n" +
            "Capability Identity : Browser.Legacy~~~~0.0.1.0\r\nState : Not Present\r\n";

        var outcome = await RunAsync(Capability("ie", "Browser.InternetExplorer"), Capability("legacy", "Browser.Legacy"));

        outcome.Succeeded.Should().BeTrue();
        _dismCalls.Should().Contain($"/Image:\"{Mount}\" /Remove-Capability /CapabilityName:Browser.InternetExplorer~~~~0.0.11.0");
        _dismCalls.Count(c => c.Contains("/Remove-Capability")).Should().Be(1);
    }

    [Fact]
    public async Task RemoveAsync_OptionalFeature_DisablesItWithoutRemovingIt()
    {
        _outputs["/Get-Features"] = "Feature Name : MediaPlayback\r\nState : Enabled\r\n";

        await RunAsync(Feature("wmp", "MediaPlayback"));

        _dismCalls.Should().Contain($"/Image:\"{Mount}\" /Disable-Feature /FeatureName:MediaPlayback");
        _dismCalls.Should().NotContain(c => c.Contains("/Remove"));
    }

    [Fact]
    public async Task RemoveAsync_FeatureTheEditionLacks_IsSkippedAndTheStepSucceeds()
    {
        _outputs["/Get-Features"] =
            "Feature Name : MediaPlayback\r\nState : Disabled\r\n\r\nFeature Name : Printing-XPSServices-Features\r\nState : Enabled\r\n";

        var outcome = await RunAsync(Feature("wmp", "MediaPlayback"), Feature("ghost", "NotInThisEdition"));

        outcome.Succeeded.Should().BeTrue();
        _dismCalls.Should().NotContain(c => c.Contains("/Disable-Feature"));
    }

    [Fact]
    public async Task RemoveAsync_ListingCalls_AskDismForEnglishOutput()
    {
        _outputs["/Get-Capabilities"] = "Capability Identity : Browser.InternetExplorer~~~~0.0.11.0\r\nState : Installed\r\n";

        await RunAsync(Capability("ie", "Browser.InternetExplorer"));

        _dismCalls.Should().Contain($"/Image:\"{Mount}\" /English /Get-Capabilities");
    }

    [Fact]
    public async Task RemoveAsync_Edge_RunsItsOwnScriptAgainstTheImageAndRemovesThePackage()
    {
        _outputs["/Get-ProvisionedAppxPackages"] = ProvisionedListing("Microsoft.MicrosoftEdge.Stable_130.0.0.0_neutral__8wekyb3d8bbwe");

        var outcome = await RunAsync(Appx("windows-app-edge", "Microsoft.MicrosoftEdge.Stable"));

        outcome.Succeeded.Should().BeTrue();
        _arguments.Should().ContainSingle().Which.Should().Be(
            $"-Image \"{Mount}\" -SoftwareRoot \"{Software}\" -SystemRoot \"{SystemHive}\\ControlSet001\" -UserRoot \"{DefaultUser}\"");
        _written[_scripts[0]].Should().Contain("[string]$Image = ''");
        _dismCalls.Should().Contain(c => c.Contains("/Remove-ProvisionedAppxPackage"));
    }

    [Fact]
    public async Task RemoveAsync_OneNote_RemovesTheProvisionedPackageAndRunsNoScript()
    {
        _outputs["/Get-ProvisionedAppxPackages"] = ProvisionedListing("Microsoft.Office.OneNote_16.0.0.0_neutral_~_8wekyb3d8bbwe");

        var outcome = await RunAsync(Appx("windows-app-onenote", "Microsoft.Office.OneNote"));

        outcome.Succeeded.Should().BeTrue();
        _dismCalls.Should().Contain(c => c.Contains("/Remove-ProvisionedAppxPackage"));
        _scripts.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_OneDrive_RunsItsOwnScriptAgainstTheImage()
    {
        var outcome = await RunAsync(Appx("windows-app-onedrive", "Microsoft.OneDriveSync"));

        outcome.Succeeded.Should().BeTrue();
        _arguments.Should().ContainSingle().Which.Should().Be(
            $"-Image \"{Mount}\" -SoftwareRoot \"{Software}\" -UserRoot \"{DefaultUser}\"");
        _written[_scripts[0]].Should().Contain("[string]$Image = ''");
    }

    [Fact]
    public async Task RemoveAsync_GameBarPackages_RunTheBloatScriptAgainstTheImage()
    {
        _outputs["/Get-ProvisionedAppxPackages"] = ProvisionedListing("Microsoft.XboxGamingOverlay_7.0.0.0_neutral_~_8wekyb3d8bbwe");

        await RunAsync(Appx("windows-app-xbox-overlay", "Microsoft.XboxGamingOverlay"));

        _arguments.Should().ContainSingle().Which.Should().Be($"-Image \"{Mount}\" -SoftwareRoot \"{Software}\"");
        _written[_scripts[0]].Should().Contain("ms-gamebar");
    }

    [Fact]
    public async Task RemoveAsync_Scripts_RunFromATempFileDeletedAfterwards()
    {
        var outcome = await RunAsync(Appx("windows-app-edge", "Microsoft.MicrosoftEdge.Stable"), Appx("windows-app-onedrive", "Microsoft.OneDriveSync"));

        outcome.Succeeded.Should().BeTrue();
        _scripts.Should().HaveCount(2).And.OnlyContain(path => path.StartsWith(@"C:\Temp\Winhance_Image_") && path.EndsWith(".ps1"));
        var folder = _scripts[0][.._scripts[0].LastIndexOf('\\')];
        _deletedFolders.Should().Equal(folder);
        _session.Verify(s => s.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_ScriptFails_LeavesTheAppForSetupAndWritesNoMarker()
    {
        _powerShell
            .Setup(r => r.RunScriptFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("exit 1"));

        var outcome = await RunAsync(Appx("windows-app-edge", "Microsoft.MicrosoftEdge.Stable"));

        outcome.Succeeded.Should().BeFalse();
        outcome.Failed.Should().Contain("Microsoft Edge");
        _deletedFolders.Should().ContainSingle();
        _session.Verify(s => s.DisposeAsync(), Times.Once);
        _files.Verify(f => f.WriteAllText(Marker, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RemoveAsync_HiveCannotLoad_LeavesTheAppForSetup()
    {
        _hives.Setup(h => h.LoadAsync(Mount, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("locked"));

        var outcome = await RunAsync(Appx("windows-app-onedrive", "Microsoft.OneDriveSync"));

        outcome.Succeeded.Should().BeFalse();
        outcome.Failed.Should().Contain("registry");
        _scripts.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_RegularApps_NeverLoadsTheHivesOrRunsScripts()
    {
        _outputs["/Get-ProvisionedAppxPackages"] = ProvisionedListing(Weather);

        await RunAsync(Appx("windows-app-weather", "Microsoft.BingWeather"));

        _hives.Verify(h => h.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _scripts.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveAsync_EverythingRemoved_WritesTheMarkerOnlyThen()
    {
        _outputs["/Get-ProvisionedAppxPackages"] = ProvisionedListing(Weather);

        var ok = await RunAsync(Appx("windows-app-weather", "Microsoft.BingWeather"));
        ok.Succeeded.Should().BeTrue();
        _files.Verify(f => f.WriteAllText(Marker, It.IsAny<string>()), Times.Once);

        _files.Invocations.Clear();
        _exit = args => args.Contains("/Remove-ProvisionedAppxPackage") ? 1 : 0;
        var failed = await RunAsync(Appx("windows-app-weather", "Microsoft.BingWeather"));

        failed.Succeeded.Should().BeFalse();
        failed.Failed.Should().Contain(Weather);
        failed.Applied.Should().Be(0);
        _files.Verify(f => f.WriteAllText(Marker, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RemoveAsync_ListingFails_MarksEveryPackageForSetup()
    {
        _exit = args => args.Contains("/Get-ProvisionedAppxPackages") ? 1 : 0;

        var outcome = await RunAsync(Appx("windows-app-weather", "Microsoft.BingWeather"));

        outcome.Succeeded.Should().BeFalse();
        outcome.Failed.Should().Contain("Microsoft.BingWeather");
    }

    [Theory]
    [InlineData("Microsoft.BingWeather_4.53.0.0_neutral_~_8wekyb3d8bbwe", "Microsoft.BingWeather", true)]
    [InlineData("Microsoft.BingWeatherPlus_4.53.0.0_neutral_~_8wekyb3d8bbwe", "Microsoft.BingWeather", false)]
    [InlineData("Microsoft.BingWeather", "Microsoft.BingWeather", false)]
    [InlineData("microsoft.bingweather_4.53.0.0_neutral_~_8wekyb3d8bbwe", "Microsoft.BingWeather", true)]
    public void PackageNamesMatchTheBareNameOnly(string full, string name, bool expected) =>
        DismListParser.IsPackageOf(full, name).Should().Be(expected);
}
