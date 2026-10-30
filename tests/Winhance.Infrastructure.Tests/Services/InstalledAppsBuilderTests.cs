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
        IReadOnlyDictionary<string, string>? chocoOutdated = null) =>
        InstalledAppsBuilder.Build(programs ?? [], store ?? [], winGet ?? [], choco ?? NoChoco, chocoOutdated ?? new Dictionary<string, string>(), catalog ?? []);

    [Fact]
    public void Winget_matches_a_program_by_its_uninstall_key()
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
    public void A_store_app_winget_has_an_update_for_updates_from_the_store_source()
    {
        var apps = Build(
            store: [Store("Microsoft.WindowsTerminal", "Terminal")],
            winGet: [new WinGetInstalledPackage("9N0DX20HK701", "Terminal", "1.0.0.0", "msstore", "1.1.0.0") { PackageFamilyNames = ["Microsoft.WindowsTerminal_8wekyb3d8bbwe"] }]);

        apps.Single().UpdateVia.Should().Be(PackageSources.MsStore);
        apps.Single().MsStoreId.Should().Be("9N0DX20HK701");
    }

    [Fact]
    public void An_app_without_a_newer_version_has_no_update()
    {
        var apps = Build(
            programs: [Program("7-Zip", "7-Zip 24.09 (x64)")],
            winGet: [new WinGetInstalledPackage("7zip.7zip", "7-Zip", "24.09", "winget", null) { ProductCodes = ["7-Zip"] }]);

        apps.Single().HasUpdate.Should().BeFalse();
        apps.Single().AvailableVersion.Should().BeNull();
    }

    [Fact]
    public void An_app_chocolatey_installed_updates_through_chocolatey_even_when_winget_has_it_too()
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
    public void An_app_chocolatey_installed_and_has_current_takes_no_update_from_winget()
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
    public void A_store_app_chocolatey_installed_takes_no_update_from_the_store()
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
    public void An_app_chocolatey_knows_but_did_not_install_still_updates_through_winget()
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
    public void A_program_winget_only_knows_locally_has_no_winget_id()
    {
        var apps = Build(
            programs: [Program("{55F0F698}", "VMware Tools")],
            winGet: [new WinGetInstalledPackage(@"ARP\Machine\X64\{55F0F698}", "VMware Tools", "12.4", null, null) { ProductCodes = ["{55F0F698}"] }]);

        apps.Single().WinGetId.Should().BeNull();
    }

    [Fact]
    public void The_catalog_supplies_ids_for_an_app_winget_did_not_match()
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
    public void Frameworks_resource_packages_and_os_internals_are_left_out()
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
    public void A_store_app_named_like_a_program_shows_once_as_the_program()
    {
        var apps = Build(
            programs: [Program("Microsoft Edge", "Microsoft Edge")],
            store: [Store("Microsoft.MicrosoftEdge.Stable", "Microsoft Edge")]);

        apps.Should().ContainSingle().Which.Kind.Should().Be(InstalledAppKind.Program);
    }

    [Fact]
    public void A_store_app_takes_its_store_id_from_the_windows_apps_catalog()
    {
        var apps = Build(
            store: [Store("Microsoft.WindowsCalculator", "Calculator")],
            catalog: [new ItemDefinition { Id = "calc", Name = "Calculator", Description = "", AppxPackageName = ["Microsoft.WindowsCalculator"], MsStoreId = "9WZDNCRFHVN5" }]);

        apps.Single().MsStoreId.Should().Be("9WZDNCRFHVN5");
    }

    [Fact]
    public void Apps_are_sorted_by_name()
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
    public void Reads_the_details_programs_and_features_shows()
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
    public void System_components_updates_and_entries_without_an_uninstaller_are_skipped()
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
    public void ReadAll_returns_the_skipped_keys_too_and_says_why_they_are_hidden()
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
    public void Reads_32_bit_and_per_user_programs_too()
    {
        Key(UninstallRegistryReader.Machine32, "Old", Values("Old app"));
        Key(UninstallRegistryReader.User, "Mine", Values("My app"));

        var entries = UninstallRegistryReader.Read(_registry.Object);

        entries.Select(e => (e.KeyName, e.PerUser)).Should().Equal(("Old", false), ("Mine", true));
    }

    [Fact]
    public void A_bad_install_date_is_left_empty()
    {
        Key(UninstallRegistryReader.Machine, "App", Values("App", ("InstallDate", "2026-10-02")));

        UninstallRegistryReader.Read(_registry.Object).Single().InstalledOn.Should().BeNull();
    }
}
