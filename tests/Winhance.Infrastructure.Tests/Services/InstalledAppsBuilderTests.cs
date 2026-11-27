using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class InstalledAppsBuilderTests
{
    private static readonly HashSet<string> NoChoco = [];

    private static UninstallEntry Program(string key, string name) => new(key, false, name, $@"C:\{name}\uninstall.exe");

    private static StorePackage Store(string name, string displayName) =>
        new(name, $"{name}_8wekyb3d8bbwe", displayName, "1.0.0.0");

    private static List<InstalledApp> Build(
        IReadOnlyList<UninstallEntry>? programs = null,
        IReadOnlyList<StorePackage>? store = null,
        IReadOnlyList<WinGetInstalledPackage>? winGet = null,
        IReadOnlySet<string>? choco = null,
        IReadOnlyList<ItemDefinition>? catalog = null,
        IReadOnlyDictionary<string, string>? chocoOutdated = null,
        IReadOnlyDictionary<string, StoreCatalogUpdate>? storeUpdates = null,
        Action<string>? trace = null) =>
        InstalledAppsBuilder.Build(programs ?? [], store ?? [], winGet ?? [], choco ?? NoChoco, chocoOutdated ?? new Dictionary<string, string>(), catalog ?? [],
            storeUpdates ?? new Dictionary<string, StoreCatalogUpdate>(), trace);

    [Fact]
    public void Build_ProgramWithAMatchingUninstallKey_MatchesTheWinGetPackage()
    {
        var apps = Build(
            programs: [Program("7-Zip", "7-Zip 24.09 (x64)")],
            winGet: [new WinGetInstalledPackage("7zip.7zip", "7-Zip", "24.09", "winget", "25.01") { ProductCodes = ["7-Zip"] }]);

        var app = apps.Should().ContainSingle().Subject;
        app.WinGetId.Should().Be("7zip.7zip");
        app.AvailableVersion.Should().Be("25.01");
        app.UpdateVia.Should().Be(PackageSources.WinGet);
    }

    [Fact]
    public void Build_StoreAppWinGetOffersAnUpdateFor_UpdatesFromTheStoreSource()
    {
        var apps = Build(
            store: [Store("Microsoft.WindowsTerminal", "Terminal")],
            winGet: [new WinGetInstalledPackage("9N0DX20HK701", "Terminal", "1.0.0.0", "msstore", "1.1.0.0") { PackageFamilyNames = ["Microsoft.WindowsTerminal_8wekyb3d8bbwe"] }]);

        apps.Single().UpdateVia.Should().Be(PackageSources.MsStore);
        apps.Single().MsStoreId.Should().Be("9N0DX20HK701");
    }

    private static readonly ItemDefinition Calculator = new()
    {
        Id = "calculator", Name = "Calculator", Description = "",
        AppxPackageName = ["Microsoft.WindowsCalculator"], MsStoreId = "9WZDNCRFHVN5",
    };

    private const string CalculatorFamily = "Microsoft.WindowsCalculator_8wekyb3d8bbwe";

    [Fact]
    public void Build_StoreAppWithCatalogUpdate_UpdatesThroughTheStoreAndShowsTheBundleVersion()
    {
        var apps = Build(
            store: [new StorePackage("Microsoft.WindowsCalculator", CalculatorFamily, "Calculator", "11.2502.2.0")],
            catalog: [Calculator],
            storeUpdates: new Dictionary<string, StoreCatalogUpdate> { [CalculatorFamily] = new("2021.2502.2.0", "2021.2607.0.0") });

        var app = apps.Single();
        app.UpdateVia.Should().Be(PackageSources.MsStore);
        app.AvailableVersion.Should().Be("2021.2607.0.0");
        app.Version.Should().Be("2021.2502.2.0");
        app.MsStoreId.Should().Be("9WZDNCRFHVN5");
    }

    [Fact]
    public void Build_StoreAppWithNoCatalogUpdate_StaysUpToDate()
    {
        var apps = Build(
            store: [new StorePackage("Microsoft.WindowsCalculator", CalculatorFamily, "Calculator", "11.2607.0.0")],
            catalog: [Calculator]);

        var app = apps.Single();
        app.HasUpdate.Should().BeFalse();
        app.Version.Should().Be("11.2607.0.0");
    }

    [Fact]
    public void Build_WhenWinGetSourceHasUpdate_WinsOverTheStoreCatalog()
    {
        var apps = Build(
            store: [new StorePackage("Microsoft.WindowsTerminal", "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "Terminal", "1.24.11911.0")],
            winGet: [new WinGetInstalledPackage("Microsoft.WindowsTerminal", "Terminal", "1.24.11911.0", "winget", "1.25.2733.0") { PackageFamilyNames = ["Microsoft.WindowsTerminal_8wekyb3d8bbwe"] }],
            catalog: [new ItemDefinition
            {
                Id = "terminal", Name = "Terminal", Description = "",
                AppxPackageName = ["Microsoft.WindowsTerminal"], WinGetPackageId = ["Microsoft.WindowsTerminal"], MsStoreId = "9N0DX20HK701",
            }],
            storeUpdates: new Dictionary<string, StoreCatalogUpdate> { ["Microsoft.WindowsTerminal_8wekyb3d8bbwe"] = new("3001.24.11911.0", "3001.24.12741.0") });

        var app = apps.Single();
        app.UpdateVia.Should().Be(PackageSources.WinGet);
        app.AvailableVersion.Should().Be("1.25.2733.0");
        app.Version.Should().Be("1.24.11911.0");
    }

    private static readonly ItemDefinition Terminal = new()
    {
        Id = "terminal", Name = "Terminal", Description = "",
        AppxPackageName = ["Microsoft.WindowsTerminal"], WinGetPackageId = ["Microsoft.WindowsTerminal"], MsStoreId = "9N0DX20HK701",
    };

    private const string TerminalFamily = "Microsoft.WindowsTerminal_8wekyb3d8bbwe";

    [Fact]
    public void Build_StoreAppWithBundleNumberedOutsideTheStore_IgnoresTheStoreCatalog()
    {
        var apps = Build(
            store: [new StorePackage("Microsoft.WindowsTerminal", TerminalFamily, "Terminal", "1.25.2733.0")],
            winGet: [new WinGetInstalledPackage("Microsoft.WindowsTerminal", "Terminal", "1.25.2733.0", "winget", null) { PackageFamilyNames = [TerminalFamily] }],
            catalog: [Terminal],
            storeUpdates: new Dictionary<string, StoreCatalogUpdate> { [TerminalFamily] = new("2025.25.2733.0", "3001.25.2733.0") });

        var app = apps.Single();
        app.HasUpdate.Should().BeFalse();
        app.Version.Should().Be("1.25.2733.0");
    }

    [Fact]
    public void Build_StoreAppWinGetHasNoUpdateFor_TakesANewerStoreBundleNumberedTheSameWay()
    {
        var apps = Build(
            store: [new StorePackage("Microsoft.WindowsTerminal", TerminalFamily, "Terminal", "1.24.11911.0")],
            winGet: [new WinGetInstalledPackage("Microsoft.WindowsTerminal", "Terminal", "1.24.11911.0", "winget", null) { PackageFamilyNames = [TerminalFamily] }],
            catalog: [Terminal],
            storeUpdates: new Dictionary<string, StoreCatalogUpdate> { [TerminalFamily] = new("3001.24.11911.0", "3001.25.2733.0") });

        var app = apps.Single();
        app.UpdateVia.Should().Be(PackageSources.MsStore);
        app.AvailableVersion.Should().Be("3001.25.2733.0");
        app.MsStoreId.Should().Be("9N0DX20HK701");
    }

    [Fact]
    public void Build_StoreAppWithOffer_TracesWhatEverySourceReported()
    {
        var traced = new List<string>();
        Build(
            store: [new StorePackage("Microsoft.WindowsTerminal", TerminalFamily, "Terminal", "1.24.11911.0")],
            winGet: [new WinGetInstalledPackage("Microsoft.WindowsTerminal", "Terminal", "1.24.11911.0", "winget", "1.25.2733.0") { PackageFamilyNames = [TerminalFamily] }],
            catalog: [Terminal],
            storeUpdates: new Dictionary<string, StoreCatalogUpdate> { [TerminalFamily] = new("3001.24.11911.0", "3001.24.12741.0") },
            trace: traced.Add);

        traced.Should().ContainSingle().Which.Should().Be(
            "Terminal: AppX 1.24.11911.0, winget Microsoft.WindowsTerminal (winget) 1.24.11911.0 -> 1.25.2733.0, "
            + "Store bundle 3001.24.11911.0 -> 3001.24.12741.0, update via WinGet 1.25.2733.0");
    }

    [Fact]
    public void StoreCatalogApps_ListsInstalledStoreAppsTheCatalogGivesAStoreId()
    {
        var packages = new List<StorePackage>
        {
            new("Microsoft.WindowsCalculator", CalculatorFamily, "Calculator", "11.2502.2.0"),
            new("Microsoft.WindowsCalculator", CalculatorFamily, "Calculator", "11.2502.2.0"),
            new("Contoso.Player", "Contoso.Player_8wekyb3d8bbwe", "Player", "1.0.0.0"),
            new("Microsoft.VCLibs", "Microsoft.VCLibs_8wekyb3d8bbwe", "VCLibs", "14.0.0.0") { IsFramework = true },
        };

        var apps = InstalledAppsBuilder.StoreCatalogApps(packages, [Calculator]);

        apps.Should().Equal(new StoreCatalogApp("9WZDNCRFHVN5", CalculatorFamily));
    }

    private static WinGetInstalledPackage TeamsPackage() =>
        new("Microsoft.Teams", "Microsoft Teams", "26260.1717.5229.7049", "winget", "26276.513.5241.9675") { PackageFamilyNames = ["MSTeams_8wekyb3d8bbwe"] };

    [Fact]
    public void Build_StoreAppAtTheOfferedAppxVersion_HasNoUpdate()
    {
        var apps = Build(
            store: [new StorePackage("MSTeams", "MSTeams_8wekyb3d8bbwe", "Microsoft Teams", "26276.513.5241.9675")],
            winGet: [TeamsPackage()]);

        var app = apps.Single();
        app.HasUpdate.Should().BeFalse();
        app.AvailableVersion.Should().BeNull();
        app.Version.Should().Be("26276.513.5241.9675");
    }

    [Fact]
    public void Build_StoreAppOlderThanTheOfferedVersion_HasAnUpdate()
    {
        var apps = Build(
            store: [new StorePackage("MSTeams", "MSTeams_8wekyb3d8bbwe", "Microsoft Teams", "26260.1717.5229.7049")],
            winGet: [TeamsPackage()]);

        var app = apps.Single();
        app.UpdateVia.Should().Be(PackageSources.WinGet);
        app.AvailableVersion.Should().Be("26276.513.5241.9675");
    }

    [Fact]
    public void Build_StoreAppNewerThanWinGetReports_ShowsItsAppxVersionBesideTheNewerOffer()
    {
        var apps = Build(
            store: [new StorePackage("MSTeams", "MSTeams_8wekyb3d8bbwe", "Microsoft Teams", "26270.100.5229.7049")],
            winGet: [TeamsPackage()]);

        var app = apps.Single();
        app.UpdateVia.Should().Be(PackageSources.WinGet);
        app.Version.Should().Be("26270.100.5229.7049");
        app.AvailableVersion.Should().Be("26276.513.5241.9675");
    }

    [Fact]
    public void Build_StoreAppWithNoWinGetInstalledVersion_ComparesByItsAppxVersion()
    {
        var apps = Build(
            store: [new StorePackage("MSTeams", "MSTeams_8wekyb3d8bbwe", "Microsoft Teams", "26260.1717.5229.7049")],
            winGet: [TeamsPackage() with { Version = null }]);

        apps.Single().UpdateVia.Should().Be(PackageSources.WinGet);
        apps.Single().Version.Should().Be("26260.1717.5229.7049");
    }

    [Fact]
    public void Build_ProgramNumberedDifferentlyInItsUninstallKey_StillGetsTheWinGetUpdate()
    {
        var apps = Build(
            programs: [new UninstallEntry("dotnet-sdk", false, "Microsoft .NET SDK 10.0.100", "uninstall.exe") { Version = "99.0.0.0" }],
            winGet: [new WinGetInstalledPackage("Microsoft.DotNet.SDK.10", ".NET SDK", "10.0.100", "winget", "10.0.101") { ProductCodes = ["dotnet-sdk"] }]);

        apps.Single().UpdateVia.Should().Be(PackageSources.WinGet);
        apps.Single().AvailableVersion.Should().Be("10.0.101");
    }

    [Fact]
    public void Build_AppWithoutANewerVersion_HasNoUpdate()
    {
        var apps = Build(
            programs: [Program("7-Zip", "7-Zip 24.09 (x64)")],
            winGet: [new WinGetInstalledPackage("7zip.7zip", "7-Zip", "24.09", "winget", null) { ProductCodes = ["7-Zip"] }]);

        apps.Single().HasUpdate.Should().BeFalse();
        apps.Single().AvailableVersion.Should().BeNull();
    }

    [Fact]
    public void Build_WhenWinGetOffersALowerVersion_IsNeverAnUpdate()
    {
        var apps = Build(
            programs: [Program("7-Zip", "7-Zip 25.01 (x64)")],
            winGet: [new WinGetInstalledPackage("7zip.7zip", "7-Zip", "25.01", "winget", "24.09") { ProductCodes = ["7-Zip"] }]);

        apps.Single().HasUpdate.Should().BeFalse();
        apps.Single().AvailableVersion.Should().BeNull();
    }

    // The SDK's Uninstall key numbers it 10.4.226.50611, winget calls the same install 10.0.402.
    [Fact]
    public void Build_DotnetSdkUpdate_ComparesAndShowsWinGetVersionsNotTheUninstallKeyVersion()
    {
        var sdk = new UninstallEntry("{8A5D3C21}", false, "Microsoft .NET SDK 10.0.402 (x64)", @"C:\ProgramData\Package Cache\sdk.exe")
        {
            Version = "10.4.226.50611",
        };

        var apps = Build(
            programs: [sdk],
            winGet: [new WinGetInstalledPackage("Microsoft.DotNet.SDK.10", "Microsoft .NET SDK 10.0", "10.0.402", "winget", "10.0.403") { ProductCodes = ["{8A5D3C21}"] }]);

        var app = apps.Single();
        app.UpdateVia.Should().Be(PackageSources.WinGet);
        app.Version.Should().Be("10.0.402");
        app.AvailableVersion.Should().Be("10.0.403");
    }

    [Fact]
    public void Build_WhenWinGetOffersAnOlderSdk_HasNoUpdate()
    {
        var sdk = new UninstallEntry("{8A5D3C21}", false, "Microsoft .NET SDK 10.0.404 (x64)", @"C:\ProgramData\Package Cache\sdk.exe")
        {
            Version = "10.4.226.50611",
        };

        var apps = Build(
            programs: [sdk],
            winGet: [new WinGetInstalledPackage("Microsoft.DotNet.SDK.10", "Microsoft .NET SDK 10.0", "10.0.404", "winget", "10.0.403") { ProductCodes = ["{8A5D3C21}"] }]);

        var app = apps.Single();
        app.HasUpdate.Should().BeFalse();
        app.Version.Should().Be("10.4.226.50611");
    }

    [Fact]
    public void Build_AppChocolateyInstalled_UpdatesThroughChocolateyEvenWhenWinGetHasItToo()
    {
        var apps = Build(
            programs: [Program("Notepad++", "Notepad++ (64-bit x64)")],
            winGet: [new WinGetInstalledPackage("Notepad++.Notepad++", "Notepad++", "8.6", "winget", "8.7") { ProductCodes = ["Notepad++"] }],
            catalog: [new ItemDefinition
            {
                Id = "notepadplusplus", Name = "Notepad++", Description = "",
                RegistryDisplayName = "Notepad++ ({arch})", WinGetPackageId = ["Notepad++.Notepad++"], ChocoPackageId = "notepadplusplus",
            }],
            choco: new HashSet<string> { "notepadplusplus" },
            chocoOutdated: new Dictionary<string, string> { ["notepadplusplus"] = "8.7.1" });

        apps.Single().UpdateVia.Should().Be(PackageSources.Chocolatey);
        apps.Single().AvailableVersion.Should().Be("8.7.1");
    }

    [Fact]
    public void Build_AppChocolateyInstalledAndCurrent_TakesNoUpdateFromWinGet()
    {
        var apps = Build(
            programs: [Program("Notepad++", "Notepad++ (64-bit x64)")],
            winGet: [new WinGetInstalledPackage("Notepad++.Notepad++", "Notepad++", "8.6", "winget", "8.7") { ProductCodes = ["Notepad++"] }],
            catalog: [new ItemDefinition
            {
                Id = "notepadplusplus", Name = "Notepad++", Description = "",
                RegistryDisplayName = "Notepad++ ({arch})", WinGetPackageId = ["Notepad++.Notepad++"], ChocoPackageId = "notepadplusplus",
            }],
            choco: new HashSet<string> { "notepadplusplus" });

        var app = apps.Single();
        app.InstalledByChocolatey.Should().BeTrue();
        app.UpdateVia.Should().Be(PackageSources.None);
        app.AvailableVersion.Should().BeNull();
        app.HasUpdate.Should().BeFalse();
    }

    [Fact]
    public void Build_StoreAppChocolateyInstalled_TakesNoUpdateFromTheStore()
    {
        var apps = Build(
            store: [Store("Contoso.Player", "Contoso Player")],
            winGet: [new WinGetInstalledPackage("9NCONTOSO001", "Contoso Player", "1.0.0.0", "msstore", "1.1.0.0") { PackageFamilyNames = ["Contoso.Player_8wekyb3d8bbwe"] }],
            catalog: [new ItemDefinition
            {
                Id = "contoso-player", Name = "Contoso Player", Description = "",
                AppxPackageName = ["Contoso.Player"], ChocoPackageId = "contoso-player",
            }],
            choco: new HashSet<string> { "contoso-player" });

        apps.Single().UpdateVia.Should().Be(PackageSources.None);
    }

    [Fact]
    public void Build_AppChocolateyKnowsButDidNotInstall_StillUpdatesThroughWinGet()
    {
        var apps = Build(
            programs: [Program("Notepad++", "Notepad++ (64-bit x64)")],
            winGet: [new WinGetInstalledPackage("Notepad++.Notepad++", "Notepad++", "8.6", "winget", "8.7") { ProductCodes = ["Notepad++"] }],
            catalog: [new ItemDefinition
            {
                Id = "notepadplusplus", Name = "Notepad++", Description = "",
                RegistryDisplayName = "Notepad++ ({arch})", WinGetPackageId = ["Notepad++.Notepad++"], ChocoPackageId = "notepadplusplus",
            }]);

        apps.Single().UpdateVia.Should().Be(PackageSources.WinGet);
    }

    // An ARP\... id is winget only knowing the local install, not a source that can install it again.
    [Fact]
    public void Build_ProgramWinGetOnlyKnowsLocally_HasNoWinGetId()
    {
        var apps = Build(
            programs: [Program("{55F0F698}", "VMware Tools")],
            winGet: [new WinGetInstalledPackage(@"ARP\Machine\X64\{55F0F698}", "VMware Tools", "12.4", null, null) { ProductCodes = ["{55F0F698}"] }]);

        apps.Single().WinGetId.Should().BeNull();
    }

    [Fact]
    public void Build_AppWinGetDidNotMatch_TakesItsIdsFromTheCatalog()
    {
        var apps = Build(
            programs: [Program("Notepad++", "Notepad++ (64-bit x64)")],
            catalog: [new ItemDefinition
            {
                Id = "notepadplusplus", Name = "Notepad++", Description = "",
                RegistryDisplayName = "Notepad++ ({arch})", WinGetPackageId = ["Notepad++.Notepad++"], ChocoPackageId = "notepadplusplus",
            }],
            choco: new HashSet<string> { "notepadplusplus" });

        var app = apps.Single();
        app.CatalogId.Should().Be("notepadplusplus");
        app.WinGetId.Should().Be("Notepad++.Notepad++");
        app.ChocoId.Should().Be("notepadplusplus");
        app.InstalledByChocolatey.Should().BeTrue();
    }

    [Fact]
    public void Build_WithoutWinGet_MatchesACatalogAppByItsUninstallKeyName()
    {
        var apps = Build(
            programs: [Program("7-Zip", "7-Zip 24.09 (x64)")],
            catalog: [new ItemDefinition { Id = "7zip", Name = "7-Zip", Description = "", WinGetPackageId = ["7zip.7zip"] }]);

        var app = apps.Should().ContainSingle().Subject;
        app.CatalogId.Should().Be("7zip");
        app.WinGetId.Should().Be("7zip.7zip");
    }

    [Fact]
    public void Build_FrameworkResourceAndSystemPackages_AreLeftOut()
    {
        var apps = Build(store:
        [
            Store("Microsoft.WindowsCalculator", "Calculator"),
            Store("Microsoft.VCLibs.140.00", "Microsoft Visual C++ Runtime") with { IsFramework = true },
            Store("Microsoft.LanguageExperiencePackde-DE", "Deutsch") with { IsResource = true },
            Store("Microsoft.Windows.ShellExperienceHost", "Windows Shell Experience") with { IsSystem = true },
        ]);

        apps.Select(a => a.Name).Should().Equal("Calculator");
    }

    [Fact]
    public void Build_StoreAppNamedLikeAProgram_ShowsOnceAsTheProgram()
    {
        var apps = Build(
            programs: [Program("Microsoft Edge", "Microsoft Edge")],
            store: [Store("Microsoft.MicrosoftEdge.Stable", "Microsoft Edge")]);

        apps.Should().ContainSingle().Which.Kind.Should().Be(InstalledAppKind.Program);
    }

    [Fact]
    public void Build_StoreApp_TakesItsStoreIdFromTheWindowsAppsCatalog()
    {
        var apps = Build(
            store: [Store("Microsoft.WindowsCalculator", "Calculator")],
            catalog: [new ItemDefinition { Id = "calc", Name = "Calculator", Description = "", AppxPackageName = ["Microsoft.WindowsCalculator"], MsStoreId = "9WZDNCRFHVN5" }]);

        apps.Single().MsStoreId.Should().Be("9WZDNCRFHVN5");
    }

    [Fact]
    public void Build_SeveralApps_SortsThemByName()
    {
        var apps = Build(programs: [Program("b", "VLC media player"), Program("a", "7-Zip")], store: [Store("Microsoft.Paint", "Paint")]);

        apps.Select(a => a.Name).Should().Equal("7-Zip", "Paint", "VLC media player");
    }
}

public class UninstallRegistryReaderTests
{
    private readonly Mock<IWindowsRegistryService> _registry = new();
    private readonly Dictionary<string, List<string>> _keys = [];

    public UninstallRegistryReaderTests() => _registry.Setup(r => r.GetSubKeyNames(It.IsAny<string>())).Returns([]);

    private void Key(string root, string key, Dictionary<string, object> values)
    {
        _keys.TryAdd(root, []);
        _keys[root].Add(key);
        _registry.Setup(r => r.GetSubKeyNames(root)).Returns(() => [.. _keys[root]]);
        foreach (var (name, value) in values)
            _registry.Setup(r => r.GetValue($@"{root}\{key}", name)).Returns(value);
    }

    private static Dictionary<string, object> Values(string name, params (string Name, object Value)[] more)
    {
        var values = new Dictionary<string, object> { ["DisplayName"] = name, ["UninstallString"] = $@"C:\{name}\uninstall.exe" };
        foreach (var (key, value) in more)
            values[key] = value;
        return values;
    }

    [Fact]
    public void Read_ProgramsAndFeaturesEntry_ReadsTheDetailsItShows()
    {
        Key(UninstallRegistryReader.Machine, "7-Zip", Values("7-Zip 24.09", ("Publisher", "Igor Pavlov"), ("DisplayVersion", "24.09"),
            ("InstallDate", "20261002"), ("EstimatedSize", 5900)));

        var entry = UninstallRegistryReader.Read(_registry.Object).Should().ContainSingle().Subject;
        entry.Publisher.Should().Be("Igor Pavlov");
        entry.Version.Should().Be("24.09");
        entry.InstalledOn.Should().Be(new DateTime(2026, 10, 2));
        entry.SizeBytes.Should().Be(5900 * 1024L);
        entry.PerUser.Should().BeFalse();
    }

    [Fact]
    public void Read_SystemComponentsUpdatesAndEntriesWithoutAnUninstaller_AreSkipped()
    {
        Key(UninstallRegistryReader.Machine, "Shown", Values("Shown"));
        Key(UninstallRegistryReader.Machine, "Component", Values("Component", ("SystemComponent", 1)));
        Key(UninstallRegistryReader.Machine, "Patch", Values("Patch", ("ParentKeyName", "Office")));
        Key(UninstallRegistryReader.Machine, "Hotfix", Values("Hotfix", ("ReleaseType", "Security Update")));
        Key(UninstallRegistryReader.Machine, "NoUninstall", new Dictionary<string, object> { ["DisplayName"] = "NoUninstall" });
        Key(UninstallRegistryReader.Machine, "NoName", new Dictionary<string, object> { ["UninstallString"] = "x.exe" });

        UninstallRegistryReader.Read(_registry.Object).Select(e => e.KeyName).Should().Equal("Shown");
    }

    [Fact]
    public void ReadAll_SkippedKeys_AreReturnedWithTheReasonTheyAreHidden()
    {
        Key(UninstallRegistryReader.Machine, "Shown", Values("Shown"));
        Key(UninstallRegistryReader.Machine, "Component", Values("Component", ("SystemComponent", 1)));
        Key(UninstallRegistryReader.Machine, "Patch", Values("Patch", ("ParentKeyName", "Office")));
        Key(UninstallRegistryReader.Machine, "NoUninstall", new Dictionary<string, object> { ["DisplayName"] = "NoUninstall" });
        Key(UninstallRegistryReader.Machine, "Bare", []);

        var entries = UninstallRegistryReader.ReadAll(_registry.Object).ToList();

        entries.Select(e => (e.KeyName, e.IsListed, e.IsSystemComponent, e.IsUpdate)).Should().Equal(
            ("Shown", true, false, false),
            ("Component", false, true, false),
            ("Patch", false, false, true),
            ("NoUninstall", false, false, false),
            ("Bare", false, false, false));
        entries.Single(e => e.KeyName == "NoUninstall").UninstallString.Should().BeEmpty();
        entries.Single(e => e.KeyName == "Bare").DisplayName.Should().BeEmpty();
    }

    [Fact]
    public void Read_32BitAndPerUserPrograms_AreReadToo()
    {
        Key(UninstallRegistryReader.Machine32, "Old", Values("Old app"));
        Key(UninstallRegistryReader.User, "Mine", Values("My app"));

        var entries = UninstallRegistryReader.Read(_registry.Object);

        entries.Select(e => (e.KeyName, e.PerUser)).Should().Equal(("Old", false), ("Mine", true));
    }

    [Fact]
    public void Read_BadInstallDate_LeavesItEmpty()
    {
        Key(UninstallRegistryReader.Machine, "App", Values("App", ("InstallDate", "2026-10-02")));

        UninstallRegistryReader.Read(_registry.Object).Single().InstalledOn.Should().BeNull();
    }
}
