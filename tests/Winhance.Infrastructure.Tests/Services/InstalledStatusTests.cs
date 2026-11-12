using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class InstalledStatusTests
{
    private static readonly string[] TestAppId = ["TestApp.Id"];
    private static readonly string[] NonExistentDetectionPath = [@"C:\NonExistent\Path\That\Does\Not\Exist"];

    private readonly Mock<ILogService> _log = new();

    private static ScanSources Sources(
        IEnumerable<UninstallEntry>? entries = null,
        IEnumerable<string>? appx = null,
        IEnumerable<string>? winGet = null,
        IEnumerable<string>? choco = null,
        InstalledWindowsComponents? components = null) => new(
            entries?.ToList() ?? [],
            (appx ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase),
            winGet?.ToHashSet(StringComparer.OrdinalIgnoreCase),
            (choco ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase),
            components);

    private static UninstallEntry Entry(string keyName, string displayName = "", bool system = false) =>
        new(keyName, false, displayName, "uninstall.exe") { IsSystemComponent = system };

    private static ItemDefinition External(string id, string name, string[]? winGetIds = null, string? msStoreId = null, string? chocoId = null, string[]? paths = null) => new()
    {
        Id = id,
        Name = name,
        Description = id,
        WinGetPackageId = winGetIds,
        MsStoreId = msStoreId,
        ChocoPackageId = chocoId,
        DetectionPaths = paths,
    };

    private static ItemDefinition Appx(string id, string appxName) => new() { Id = id, Name = id, Description = id, AppxPackageName = [appxName] };

    private Dictionary<string, bool> ExternalStatus(ScanSources sources, params ItemDefinition[] definitions) => InstalledStatus.External(definitions, sources, _log.Object);

    private Dictionary<string, bool> WindowsStatus(ScanSources sources, params ItemDefinition[] definitions) => InstalledStatus.Windows(definitions, sources, _log.Object);

    [Fact]
    public void External_NoDefinitions_ReturnsAnEmptyCaseInsensitiveDictionary()
    {
        var result = ExternalStatus(Sources());

        result.Should().BeEmpty();
        result.Comparer.Should().Be(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void External_WinGetIdMatches_IsInstalledViaWinGet()
    {
        var app = External("ext1", "Test App", TestAppId);

        ExternalStatus(Sources(winGet: ["TestApp.Id"]), app)["ext1"].Should().BeTrue();
        app.DetectedVia.Should().Be(DetectionSource.WinGet);
    }

    [Fact]
    public void External_WinGetListsTheStoreId_IsInstalled()
    {
        var app = External("ext1", "Test App", msStoreId: "9NBLGGH4NNS1");

        ExternalStatus(Sources(winGet: ["9NBLGGH4NNS1"]), app)["ext1"].Should().BeTrue();
    }

    [Fact]
    public void External_WinGetCouldNotBeRead_FallsBackToChocolatey()
    {
        var app = External("ext1", "Test App", TestAppId, chocoId: "testapp");

        ExternalStatus(Sources(winGet: null, choco: ["testapp"]), app)["ext1"].Should().BeTrue();
        app.DetectedVia.Should().Be(DetectionSource.Chocolatey);
    }

    [Fact]
    public void External_NothingFindsIt_IsNotInstalled()
    {
        ExternalStatus(Sources(winGet: ["Other.Id"]), External("ext1", "Test App", TestAppId))["ext1"].Should().BeFalse();
    }

    [Fact]
    public void External_WinGetMatchWithAFingerprintTheRegistryLacks_IsAFalsePositive()
    {
        var app = External("ext1", "AutoHotkey", TestAppId) with { RegistryDisplayName = "AutoHotkey {version}" };

        ExternalStatus(Sources(winGet: ["TestApp.Id"], entries: [Entry("Other", "Something Else")]), app)["ext1"].Should().BeFalse();
    }

    [Fact]
    public void External_WinGetMatchWithAFingerprintTheRegistryHas_IsInstalled()
    {
        var app = External("ext1", "AutoHotkey", TestAppId) with { RegistryDisplayName = "AutoHotkey {version}" };

        ExternalStatus(Sources(winGet: ["TestApp.Id"], entries: [Entry("AutoHotkey", "AutoHotkey 2.0.19")]), app)["ext1"].Should().BeTrue();
    }

    [Fact]
    public void External_AppxPackage_IsInstalledViaAppX()
    {
        var app = Appx("ext-store", "Contoso.App");

        ExternalStatus(Sources(appx: ["Contoso.App"]), app)["ext-store"].Should().BeTrue();
        app.DetectedVia.Should().Be(DetectionSource.AppX);
    }

    [Fact]
    public void External_AppxPackageMissing_IsNotInstalled()
    {
        ExternalStatus(Sources(appx: ["Other.App"]), Appx("ext-store", "Contoso.App"))["ext-store"].Should().BeFalse();
    }

    [Fact]
    public void External_WinGetWinsOverAppx()
    {
        var app = External("ext1", "Test App", TestAppId) with { AppxPackageName = ["Contoso.App"] };

        ExternalStatus(Sources(winGet: ["TestApp.Id"], appx: ["Contoso.App"]), app);

        app.DetectedVia.Should().Be(DetectionSource.WinGet);
    }

    [Fact]
    public void External_NameEqualsAVisibleUninstallKey_IsInstalledViaRegistry()
    {
        var app = External("ext-contoso", "Contoso Tool");

        ExternalStatus(Sources(entries: [Entry("Contoso Tool")]), app)["ext-contoso"].Should().BeTrue();
        app.DetectedVia.Should().Be(DetectionSource.Registry);
    }

    [Fact]
    public void External_NameEqualsAHiddenUninstallKey_IsNotInstalled()
    {
        ExternalStatus(Sources(entries: [Entry("Contoso Tool", "Contoso Tool", system: true)]), External("ext-contoso", "Contoso Tool"))["ext-contoso"].Should().BeFalse();
    }

    [Fact]
    public void External_NameEqualsADisplayNameUnderAnotherKey_IsInstalled()
    {
        ExternalStatus(Sources(entries: [Entry("{A1B2}", "Contoso Tool")]), External("ext-contoso", "Contoso Tool"))["ext-contoso"].Should().BeTrue();
    }

    [Fact]
    public void External_ExistingDetectionPath_IsInstalledViaFileSystem()
    {
        var app = External("ext-portable", "Portable App", paths: [Path.GetTempPath()]);

        ExternalStatus(Sources(), app)["ext-portable"].Should().BeTrue();
        app.DetectedVia.Should().Be(DetectionSource.FileSystem);
    }

    [Fact]
    public void External_MissingDetectionPath_IsNotInstalled()
    {
        ExternalStatus(Sources(), External("ext-portable", "Portable App", paths: NonExistentDetectionPath))["ext-portable"].Should().BeFalse();
    }

    [Fact]
    public void Windows_AppxPackage_IsInstalled()
    {
        WindowsStatus(Sources(appx: ["Microsoft.TestApp1"]), Appx("app1", "Microsoft.TestApp1"))["app1"].Should().BeTrue();
    }

    [Fact]
    public void Windows_AppFoundOnlyByItsUninstallKey_IsInstalled()
    {
        var app = Appx("windows-app-note", "Winhance.Test.NotInstalled") with { RegistrySubKeyName = "OneNoteFreeRetail - {locale}" };

        WindowsStatus(Sources(entries: [Entry("OneNoteFreeRetail - en-us", system: true)]), app)["windows-app-note"].Should().BeTrue();
    }

    [Fact]
    public void Windows_AppWithOnlyAnUninstallKey_IsInstalled()
    {
        var app = new ItemDefinition
        {
            Id = "windows-app-edge-webview",
            Name = "Microsoft Edge WebView2 and EdgeUpdate",
            Description = "",
            RegistrySubKeyName = "Microsoft EdgeWebView",
        };

        WindowsStatus(Sources(entries: [Entry("Microsoft EdgeWebView", "Microsoft Edge WebView2 Runtime")]), app)["windows-app-edge-webview"].Should().BeTrue();
    }

    [Fact]
    public void Windows_CapabilitiesAndFeatures_ComeFromTheComponentsTheScanRead()
    {
        var capability = new ItemDefinition { Id = "cap", Name = "Cap", Description = "", CapabilityName = "Browser.InternetExplorer" };
        var feature = new ItemDefinition { Id = "feat", Name = "Feature", Description = "", OptionalFeatureName = "Recall" };
        var missing = new ItemDefinition { Id = "gone", Name = "Gone", Description = "", OptionalFeatureName = "Gone" };
        var components = new InstalledWindowsComponents(["Browser.InternetExplorer~~~~0.0.1.0"], ["Recall"]);

        var result = WindowsStatus(Sources(components: components), capability, feature, missing);

        result["cap"].Should().BeTrue();
        result["feat"].Should().BeTrue();
        result["gone"].Should().BeFalse();
    }

    [Fact]
    public void Windows_WhenDismGaveNothing_EveryCapabilityAndFeatureIsNotInstalled()
    {
        var feature = new ItemDefinition { Id = "feat", Name = "Feature", Description = "", OptionalFeatureName = "Recall" };

        WindowsStatus(Sources(components: new InstalledWindowsComponents(null, null)), feature)["feat"].Should().BeFalse();
    }

    [Theory]
    [InlineData("MediaMonkey 5", "MediaMonkey {version}", true)]
    [InlineData("K-Lite Mega Codec Pack 19.5.0", "K-Lite Mega Codec Pack {version}", true)]
    [InlineData("ONLYOFFICE 9.3.0 (x64)", "ONLYOFFICE {version} ({arch})", true)]
    [InlineData("PuTTY release 0.83 (64-bit)", "PuTTY release {version} ({arch})", true)]
    [InlineData("InputLeap 3.0.2-release", "InputLeap {version}-release", true)]
    [InlineData("Zoom Workplace (64-bit)", "Zoom Workplace ({arch})", true)]
    [InlineData("O365ProPlusRetail - en-us", "O365ProPlusRetail - {locale}", true)]
    [InlineData("Mozilla Thunderbird (x64 en-US)", "Mozilla Thunderbird ({arch} {locale})", true)]
    [InlineData("Krita (x64) 5.2.16 (git 7d9aefc)", "Krita ({arch}) {version}", true)]
    [InlineData("Microsoft EdgeWebView2 Runtime", "Microsoft EdgeWebView{version}", true)]
    [InlineData("Trillian Machine-Wide Installer", "Trillian Machine-Wide Installer", true)]
    [InlineData("Microsoft Visual C++ 2008 Redistributable - x64 9.0.30729.6161", "Microsoft Visual C++ 2008 Redistributable - x64 {version}", true)]
    [InlineData("Microsoft .NET Core Runtime - 3.1.32 (x64)", "Microsoft .NET Runtime - 3.1.{version} ({arch})|Microsoft .NET Core Runtime - 3.1.{version} ({arch})", true)]
    [InlineData("Microsoft .NET Runtime - 3.1.32 (x64)", "Microsoft .NET Runtime - 3.1.{version} ({arch})|Microsoft .NET Core Runtime - 3.1.{version} ({arch})", true)]
    [InlineData("Microsoft Visual C++ v14 Redistributable (x64) - 14.51.36247", "Microsoft Visual C++ 2015-2022 Redistributable (x64) - {version}|Microsoft Visual C++ v14 Redistributable (x64) - {version}", true)]
    [InlineData("Microsoft Visual C++ 2015-2022 Redistributable (x64) - 14.38.33135", "Microsoft Visual C++ 2015-2022 Redistributable (x64) - {version}|Microsoft Visual C++ v14 Redistributable (x64) - {version}", true)]
    [InlineData("Microsoft Visual Studio Code", "Microsoft Visual Studio Code|Microsoft Visual Studio Code (User)", true)]
    [InlineData("Microsoft Visual Studio Code (User)", "Microsoft Visual Studio Code|Microsoft Visual Studio Code (User)", true)]
    [InlineData("Microsoft Visual Studio Code - Insiders", "Microsoft Visual Studio Code|Microsoft Visual Studio Code (User)", false)]
    [InlineData("VLC media player", "VLC media player", true)]
    [InlineData("MediaMonkey 5", "VLC media player", false)]
    [InlineData("GIMP 3.0.8-2", "Audacity {version}", false)]
    [InlineData("Microsoft .NET Runtime - 5.0.17 (x64)", "Microsoft .NET Runtime - 3.1.{version} ({arch})|Microsoft .NET Core Runtime - 3.1.{version} ({arch})", false)]
    [InlineData("Microsoft Visual C++ 2013 Redistributable (x64) - 12.0.30501", "Microsoft Visual C++ 2015-2022 Redistributable (x64) - {version}|Microsoft Visual C++ v14 Redistributable (x64) - {version}", false)]
    [InlineData("", "Foo {version}", false)]
    public void MatchesPattern_ReturnsExpected(string input, string pattern, bool expected)
    {
        InstalledStatus.MatchesPattern(input, pattern).Should().Be(expected);
    }
}
