using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.TestSupport;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class ProfileServiceTests
{
    private static readonly SettingsProfile Gaming = new("Gaming", "Gaming", ProfileKind.User, @"C:\p\Gaming.winhance");
    private static readonly string[] SettingsFocusLeavesAlone = ["pci-link-state-power-management", "processor-energy-performance-preference", "unrelated"];

    private readonly Mock<ILocalizationService> _localization = new();
    private readonly Mock<IUserPreferencesService> _preferences = new();
    private readonly Mock<IInteractiveUserService> _interactiveUser = new();
    private readonly Mock<ICatalogSettingsRegistry> _registry = new();
    private readonly Mock<ISettingSnapshotSource> _snapshot = new();
    private readonly Mock<IConfigLoadService> _configLoad = new();
    private readonly Mock<IConfigApplicationExecutionService> _execution = new();
    private readonly Mock<IConfigurationService> _configuration = new();
    private readonly Mock<IFileStore> _fileStore = new();
    private readonly Mock<ICatalogScopeProvider> _scopes = new();
    private readonly Mock<IConfigFileWriter> _configFiles = new();
    private readonly Mock<IFileSystemService> _fileSystem = new();
    private WinhanceConfigFile? _applied;
    private bool? _restartedExplorer;
    private bool? _broadcastRegional;
    private readonly Dictionary<string, SelectionSet> _written = [];

    public ProfileServiceTests()
    {
        _localization.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
        _localization.MirrorTryGetString();
        _fileSystem.Setup(f => f.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([1, 2, 3]);
        _interactiveUser.Setup(i => i.GetInteractiveUserFolderPath(It.IsAny<Environment.SpecialFolder>())).Returns(@"C:\Users\u\AppData\Local");
        _fileSystem.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => string.Join(@"\", parts));
        _fileStore.Setup(f => f.LoadAsync(It.IsAny<SelectionSet>())).ReturnsAsync((SelectionSet set) => set);
        _scopes.Setup(s => s.Current).Returns(CatalogScope.CurrentMachine);
        _configFiles.Setup(w => w.WriteAsync(It.IsAny<SelectionSet>(), It.IsAny<CatalogScope>(), It.IsAny<string>())).ReturnsAsync([4, 5, 6]);
        _registry.Setup(r => r.GetAll(It.IsAny<CatalogScope>()))
            .Returns(new Dictionary<string, IReadOnlyList<Setting>> { [FeatureIds.GamingPerformance] = [Toggle("a")] });
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.ActiveProfile, string.Empty)).Returns(string.Empty);
        foreach (var id in new[] { "a", "b", "c" })
            _registry.Setup(r => r.GetById(id, CatalogScope.CurrentMachine)).Returns(Toggle(id));
        GivenTheProfileHolds("a", "b", "c", "gone");
        _configLoad.Setup(c => c.ReadEmbeddedConfigurationAsync(false)).ReturnsAsync(() => Config("a", "b", "c", "gone"));
        var filter = RealConfigLoad();
        _configLoad.Setup(c => c.FilterConfigForCurrentSystem(It.IsAny<WinhanceConfigFile>())).Returns((WinhanceConfigFile c) => filter.FilterConfigForCurrentSystem(c));
        _execution
            .Setup(e => e.ApplySettingsQuietlyAsync(It.IsAny<WinhanceConfigFile>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Callback((WinhanceConfigFile c, string _, bool _, bool explorer, bool regional) =>
            {
                _applied = c;
                _restartedExplorer = explorer;
                _broadcastRegional = regional;
            })
            .ReturnsAsync([]);
    }

    private ProfileService CreateService() => new(
        Mock.Of<ILogService>(),
        _localization.Object,
        _preferences.Object,
        _fileSystem.Object,
        _interactiveUser.Object,
        _registry.Object,
        _snapshot.Object,
        _fileStore.Object,
        _scopes.Object,
        _configFiles.Object,
        _configLoad.Object,
        _execution.Object,
        _configuration.Object);

    // The real filter, so a null section reaches the plan the way it would from a real file.
    private ConfigLoadService RealConfigLoad() => new(
        Mock.Of<ILogService>(),
        Mock.Of<IDialogService>(),
        _localization.Object,
        Mock.Of<IWindowsVersionService>(),
        _registry.Object,
        Mock.Of<IConfigMigrationService>(),
        _interactiveUser.Object,
        _fileSystem.Object,
        Mock.Of<IMainWindowProvider>(),
        Mock.Of<IConfigImportState>());

    private void GivenTheProfileHolds(params string?[] ids) => GivenTheProfileParsesTo(() => Config(ids));

    // Matched by file name: a profile listed from disk carries the real profiles folder, not Gaming's fixture path.
    private void GivenTheProfileParsesTo(Func<WinhanceConfigFile?> config) =>
        _configLoad.Setup(c => c.ParseConfiguration(It.IsAny<byte[]>(), It.Is<string>(p => p.EndsWith(@"\Gaming.winhance", StringComparison.Ordinal)))).Returns(config);

    // GetProfiles lists the folder, so Find knows Gaming once it is saved.
    private void GivenGamingIsSaved()
    {
        _fileSystem.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        _fileSystem.Setup(f => f.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>())).Returns([Gaming.FilePath!]);
        _fileSystem.Setup(f => f.GetFileNameWithoutExtension(Gaming.FilePath!)).Returns("Gaming");
    }

    private void GivenGamingIsActive()
    {
        GivenGamingIsSaved();
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.ActiveProfile, string.Empty)).Returns("Gaming");
    }

    // The built-ins are written from the machine's reading; each file parses to one setting, "a", that reads differently.
    private void GivenTheMachineHas(params SettingChoice[] machine)
    {
        _snapshot.Setup(s => s.CaptureAsync(CatalogScope.CurrentMachine)).ReturnsAsync(machine);
        _registry.Setup(r => r.GetFeatureIdForSetting(It.IsAny<string>())).Returns(FeatureIds.GamingPerformance);
        _configFiles.Setup(w => w.WriteAsync(It.IsAny<SelectionSet>(), It.IsAny<CatalogScope>(), It.IsAny<string>()))
            .Callback((SelectionSet set, CatalogScope _, string path) => _written[path] = set)
            .ReturnsAsync([4, 5, 6]);
        _configLoad.Setup(c => c.ParseConfiguration(It.IsAny<byte[]>(), It.Is<string>(path => path.Contains(@"\Builtin\")))).Returns(() => Config("a"));
        GivenTheMachineReads(("a", SettingMatch.Differs));
    }

    private static bool IsBuiltinFile(string path, string name) => path.EndsWith($@"\Builtin\{name}.winhance", StringComparison.Ordinal);

    private SelectionSet WrittenTo(string name) => _written.Single(w => IsBuiltinFile(w.Key, name)).Value;

    private void VerifyAppliedWithoutThePolicyCleanup() =>
        _execution.Verify(e => e.ApplySettingsQuietlyAsync(It.IsAny<WinhanceConfigFile>(), It.IsAny<string>(), false, It.IsAny<bool>(), It.IsAny<bool>()), Times.Once);

    private static Setting Toggle(string id, RestartTarget? restart = null) => new()
    {
        Id = id, Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of(id) },
        Apply = new ApplyBehavior { Restart = restart },
        Targets = new Target[] { new RegTarget("V", [@"HKEY_CURRENT_USER\Software\T"], id, RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = LocKey.Common.Enabled, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(1) } },
            new SettingState { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(0) } },
        },
    };

    private static WinhanceConfigFile Config(params string?[] ids) => new()
    {
        Optimize = new()
        {
            IsIncluded = true,
            Features = new Dictionary<string, ConfigSection>
            {
                ["Gaming"] = new() { IsIncluded = true, Items = ids.Select(id => new ConfigurationItem { Id = id!, IsSelected = true }).ToList() },
            },
        },
    };

    private void GivenTheMachineReads(params (string Id, SettingMatch Match)[] before) =>
        GivenTheMachineReads(before, before.Select(b => (b.Id, SettingMatch.Matches)).ToArray());

    private void GivenTheMachineReads((string Id, SettingMatch Match)[] before, (string Id, SettingMatch Match)[] after) =>
        _snapshot.SetupSequence(s => s.CompareAsync(It.IsAny<IReadOnlyList<SettingChoice>>(), It.IsAny<IReadOnlyList<CarriedFile>>(), CatalogScope.CurrentMachine))
            .ReturnsAsync(before.ToDictionary(b => b.Id, b => b.Match))
            .ReturnsAsync(after.ToDictionary(a => a.Id, a => a.Match));

    private IEnumerable<string> AppliedIds() => _applied!.Optimize.Features.Values.SelectMany(f => f.Items).Select(i => i.Id);

    [Fact]
    public async Task SwitchAsync_SomeSettingsAlreadySet_SkipsOnlyThoseTheMachineReadsAsSet()
    {
        GivenTheMachineReads(("a", SettingMatch.Matches), ("b", SettingMatch.Differs), ("c", SettingMatch.Unknown));

        var result = await CreateService().SwitchAsync(Gaming);

        AppliedIds().Should().BeEquivalentTo("b", "c", "gone");
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SwitchAsync_SettingStillDiffersAfterApply_FailsTheSwitch()
    {
        GivenTheMachineReads(
            [("a", SettingMatch.Differs), ("b", SettingMatch.Differs), ("c", SettingMatch.Matches)],
            [("a", SettingMatch.Matches), ("b", SettingMatch.Differs)]);

        var result = await CreateService().SwitchAsync(Gaming);

        result!.Success.Should().BeFalse();
        result.NotApplied.Should().Equal("b");
        _preferences.Verify(p => p.SetPreferenceAsync(UserPreferenceKeys.ActiveProfileFailed, true), Times.Once);
    }

    [Fact]
    public async Task SwitchAsync_ProfileAlreadyInPlace_WritesNothing()
    {
        GivenTheMachineReads(("a", SettingMatch.Matches), ("b", SettingMatch.Matches), ("c", SettingMatch.Matches));
        GivenTheProfileHolds("a", "b", "c");

        var result = await CreateService().SwitchAsync(Gaming);

        result!.Success.Should().BeTrue();
        _execution.Verify(e => e.ApplySettingsQuietlyAsync(It.IsAny<WinhanceConfigFile>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [Theory]
    [InlineData("", false, 1)]
    [InlineData("Gaming", false, 0)]
    [InlineData("", true, 0)]
    public async Task SwitchAsync_ActiveProfileOrReapply_BacksUpOnlyWhenSwitchingToAnotherProfile(string active, bool reapply, int backups)
    {
        GivenTheMachineReads(("a", SettingMatch.Differs), ("b", SettingMatch.Matches), ("c", SettingMatch.Matches));
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.ActiveProfile, string.Empty)).Returns(active);
        var service = CreateService();

        if (reapply)
            await service.ReapplyAsync(Gaming);
        else
            await service.SwitchAsync(Gaming);

        _configuration.Verify(c => c.CreateUserBackupConfigAsync(), Times.Exactly(backups));
    }

    [Theory]
    [InlineData("intl", false, true)]
    [InlineData("Explorer", true, false)]
    [InlineData(null, false, false)]
    public async Task SwitchAsync_SettingRestartKind_RestartsExplorerAndBroadcastsOnlyWhenNeeded(string? restart, bool explorer, bool regional)
    {
        _registry.Setup(r => r.GetById("a", CatalogScope.CurrentMachine)).Returns(Toggle("a", restart is null ? null : new RestartProcess(restart)));
        GivenTheMachineReads(("a", SettingMatch.Differs), ("b", SettingMatch.Matches), ("c", SettingMatch.Matches));

        await CreateService().SwitchAsync(Gaming);

        _restartedExplorer.Should().Be(explorer);
        _broadcastRegional.Should().Be(regional);
    }

    [Theory]
    [InlineData(false, SettingMatch.Unknown, SettingMatch.Unknown, true)]
    [InlineData(true, SettingMatch.Unknown, SettingMatch.Unknown, false)]
    [InlineData(true, SettingMatch.Unknown, SettingMatch.Matches, true)]
    [InlineData(true, SettingMatch.Differs, SettingMatch.Matches, true)]
    public async Task ReapplyAsync_SettingBeforeAndAfter_AsksForRestartOnlyForASettingItSawChange(bool reapply, SettingMatch before, SettingMatch after, bool restart)
    {
        _registry.Setup(r => r.GetById("a", CatalogScope.CurrentMachine)).Returns(Toggle("a") with { Apply = new ApplyBehavior { RequiresReboot = true } });
        GivenTheMachineReads(
            [("a", before), ("b", SettingMatch.Matches), ("c", SettingMatch.Matches)],
            [("a", after)]);
        var service = CreateService();

        var result = reapply ? await service.ReapplyAsync(Gaming) : await service.SwitchAsync(Gaming);

        result!.NeedsRestart.Should().HaveCount(restart ? 1 : 0);
    }

    [Fact]
    public async Task ExportAsync_SavedProfile_WritesANewFileWithItsBytes()
    {
        var exported = await CreateService().ExportAsync(Gaming, @"D:\out\Gaming.winhance");

        exported.Should().BeTrue();
        _fileSystem.Verify(f => f.WriteAllBytesAsync(@"D:\out\Gaming.winhance", It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 1, 2, 3 })), It.IsAny<CancellationToken>()), Times.Once);
        _fileSystem.Verify(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task SwitchAsync_ProfileWithNullSection_SucceedsWithoutThrowing()
    {
        GivenTheProfileParsesTo(() => new WinhanceConfigFile { Optimize = null! });
        GivenTheMachineReads();

        var result = await CreateService().SwitchAsync(Gaming);

        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SwitchAsync_ProfileItemWithNoId_SkipsItInsteadOfFailing()
    {
        GivenTheProfileHolds("a", null);
        GivenTheMachineReads(("a", SettingMatch.Differs));

        var result = await CreateService().SwitchAsync(Gaming);

        result!.Success.Should().BeTrue();
        AppliedIds().Should().Equal("a");
    }

    [Fact]
    public async Task DriftAsync_ProfileWithNullSection_IsEmpty()
    {
        GivenGamingIsActive();
        GivenTheProfileParsesTo(() => new WinhanceConfigFile { Optimize = null! });

        (await CreateService().DriftAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DriftAsync_Always_FiltersTheProfileToThisSystem()
    {
        GivenGamingIsActive();
        GivenTheMachineReads(("a", SettingMatch.Matches), ("b", SettingMatch.Matches), ("c", SettingMatch.Matches));

        await CreateService().DriftAsync();

        _configLoad.Verify(c => c.FilterConfigForCurrentSystem(It.IsAny<WinhanceConfigFile>()), Times.Once);
    }

    [Fact]
    public async Task DriftAsync_CheckThrows_ReportsNone()
    {
        GivenGamingIsActive();
        _snapshot.Setup(s => s.CompareAsync(It.IsAny<IReadOnlyList<SettingChoice>>(), It.IsAny<IReadOnlyList<CarriedFile>>(), CatalogScope.CurrentMachine)).ThrowsAsync(new InvalidOperationException("boom"));

        (await CreateService().DriftAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DriftAsync_TranslationMissing_NamesTheSettingByItsSettingName()
    {
        var name = TestKeys.Of("b").Value;
        _localization.MissingKey(name);
        GivenGamingIsActive();
        GivenTheMachineReads(("a", SettingMatch.Matches), ("b", SettingMatch.Differs), ("c", SettingMatch.Matches));

        (await CreateService().DriftAsync()).Should().Equal(name);
    }

    [Fact]
    public async Task SwitchAsync_ProfileFailsToLoad_DoesNotBecomeTheActiveProfile()
    {
        GivenTheProfileParsesTo(() => null);

        var result = await CreateService().SwitchAsync(Gaming);

        result!.Success.Should().BeFalse();
        _preferences.Verify(p => p.SetPreferenceAsync(UserPreferenceKeys.ActiveProfile, It.IsAny<string>()), Times.Never);
        _preferences.Verify(p => p.SetPreferenceAsync(UserPreferenceKeys.ActiveProfileFailed, true), Times.Once);
    }

    [Fact]
    public async Task SwitchAsync_WhenApplyThrows_DoesNotMakeTheProfileActive()
    {
        GivenTheMachineReads(("a", SettingMatch.Differs), ("b", SettingMatch.Differs), ("c", SettingMatch.Differs));
        _execution
            .Setup(e => e.ApplySettingsQuietlyAsync(It.IsAny<WinhanceConfigFile>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var result = await CreateService().SwitchAsync(Gaming);

        result!.Success.Should().BeFalse();
        _preferences.Verify(p => p.SetPreferenceAsync(UserPreferenceKeys.ActiveProfile, It.IsAny<string>()), Times.Never);
        _preferences.Verify(p => p.SetPreferenceAsync(UserPreferenceKeys.ActiveProfileFailed, true), Times.Once);
    }

    // The localization mock returns the key, so this user profile has the built-in Focus's display name.
    [Fact]
    public async Task DriftAsync_WithUserProfileNamedLikeABuiltIn_ChecksTheUserProfile()
    {
        const string name = "Profiles_Builtin_Focus";
        _fileSystem.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        _fileSystem.Setup(f => f.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>())).Returns([$@"C:\p\{name}.winhance"]);
        _fileSystem.Setup(f => f.GetFileNameWithoutExtension(It.IsAny<string>())).Returns(name);
        _configLoad.Setup(c => c.ParseConfiguration(It.IsAny<byte[]>(), It.IsAny<string>())).Returns(() => Config("a"));
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.ActiveProfile, string.Empty)).Returns(name);
        GivenTheMachineReads(("a", SettingMatch.Matches));

        await CreateService().DriftAsync();

        _configLoad.Verify(c => c.ParseConfiguration(It.IsAny<byte[]>(), It.IsAny<string>()), Times.Once);
        _snapshot.Verify(s => s.CaptureAsync(It.IsAny<CatalogScope>()), Times.Never);
    }

    [Theory]
    [InlineData("CON", false)]
    [InlineData("nul.txt", false)]
    [InlineData("com1", false)]
    [InlineData("LPT9", false)]
    [InlineData("aux.x", false)]
    [InlineData("Gaming2", true)]
    [InlineData("COM10", true)]
    [InlineData("Console", true)]
    public void IsNameAvailable_WindowsDeviceName_IsNotAvailable(string name, bool available) =>
        CreateService().IsNameAvailable(name).Should().Be(available);

    [Fact]
    public async Task ReapplyAsync_ProfileFileUnreadable_FailsWithoutApplyingIt()
    {
        _fileSystem.Setup(f => f.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("locked"));

        var result = await CreateService().ReapplyAsync(Gaming);

        result!.Success.Should().BeFalse();
        result.NotApplied.Should().Equal("Profiles_Error_Load");
        _execution.Verify(e => e.ApplySettingsQuietlyAsync(It.IsAny<WinhanceConfigFile>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task DriftAsync_SettingsReadingUnknownOrMatching_NamesOnlyThosePositivelyDifferent()
    {
        GivenGamingIsActive();
        GivenTheMachineReads(("a", SettingMatch.Matches), ("b", SettingMatch.Differs), ("c", SettingMatch.Unknown));

        (await CreateService().DriftAsync()).Should().Equal("b");
    }

    [Fact]
    public async Task SaveAsync_ProfileFromBuilder_KeepsOnlyThisPcsSettings()
    {
        _registry.Setup(r => r.GetFeatureIdForSetting("a")).Returns(FeatureIds.GamingPerformance);
        _registry.Setup(r => r.GetFeatureIdForSetting("answer")).Returns(FeatureIds.Autounattend);
        SelectionSet? saved = null;
        _fileStore.Setup(f => f.LoadAsync(It.IsAny<SelectionSet>()))
            .Callback((SelectionSet set) => saved = set)
            .ReturnsAsync((SelectionSet set) => set);

        await CreateService().SaveAsync("Gaming", [new("a", new ChoiceValue.Toggle(true)), new("answer", new ChoiceValue.Toggle(true))]);

        saved!.Settings.Select(c => c.SettingId).Should().Equal("a");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetProfiles_Always_ListsBuiltInsFirstAndBeforeWinhanceOnlyOnceItsFileExists(bool beforeWinhance)
    {
        GivenGamingIsSaved();
        _fileSystem.Setup(f => f.FileExists(It.Is<string>(p => IsBuiltinFile(p, "before-winhance")))).Returns(beforeWinhance);

        var profiles = CreateService().GetProfiles();

        string[] expected = beforeWinhance
            ? [ProfileService.FocusId, ProfileService.EverydayId, ProfileService.BeforeWinhanceId, "Gaming"]
            : [ProfileService.FocusId, ProfileService.EverydayId, "Gaming"];
        profiles.Select(p => p.Id).Should().Equal(expected);
        profiles[0].Description.Should().Be("Profiles_Builtin_Focus_Description");
        profiles[^1].Description.Should().BeNull();
    }

    private static SettingsProfile Builtin(ProfileService service, string id) => service.FindById(id)!;

    private void VerifyEverydaySnapshots(int times)
    {
        _configFiles.Verify(w => w.WriteAsync(It.IsAny<SelectionSet>(), It.IsAny<CatalogScope>(), It.Is<string>(p => IsBuiltinFile(p, "everyday"))), Times.Exactly(times));
    }

    // Only a switch into Focus from another profile keeps Everyday; Focus to Focus would snapshot Focus's own values.
    [Theory]
    [InlineData("", false, 1)]
    [InlineData("Gaming", false, 1)]
    [InlineData(ProfileService.FocusId, false, 0)]
    [InlineData(ProfileService.FocusId, true, 0)]
    [InlineData("Gaming", true, 0)]
    public async Task SwitchAsync_IntoFocus_SnapshotsEverydayOnlyWhenComingFromAnotherProfile(string active, bool reapply, int snapshots)
    {
        GivenTheMachineHas(new SettingChoice("gaming-game-mode", new ChoiceValue.Toggle(false)));
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.ActiveProfile, string.Empty)).Returns(active);
        var service = CreateService();
        var focus = Builtin(service, ProfileService.FocusId);

        var result = reapply ? await service.ReapplyAsync(focus) : await service.SwitchAsync(focus);

        result!.Success.Should().BeTrue();
        VerifyEverydaySnapshots(snapshots);
    }

    [Fact]
    public async Task SwitchAsync_AfterAFailedSwitchWithEverydaySnapshotOnDisk_KeepsTheSnapshot()
    {
        GivenTheMachineHas(new SettingChoice("gaming-game-mode", new ChoiceValue.Toggle(false)));
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.ActiveProfile, string.Empty)).Returns(ProfileService.EverydayId);
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.ActiveProfileFailed, false)).Returns(true);
        _fileSystem.Setup(f => f.FileExists(It.Is<string>(p => IsBuiltinFile(p, "everyday")))).Returns(true);
        var service = CreateService();

        await service.SwitchAsync(Builtin(service, ProfileService.FocusId));

        VerifyEverydaySnapshots(0);
    }

    [Fact]
    public async Task SwitchAsync_AfterAFailedSwitchWithNoEverydaySnapshotOnDisk_SnapshotsEveryday()
    {
        GivenTheMachineHas(new SettingChoice("gaming-game-mode", new ChoiceValue.Toggle(false)));
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.ActiveProfile, string.Empty)).Returns(ProfileService.EverydayId);
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.ActiveProfileFailed, false)).Returns(true);
        var service = CreateService();

        await service.SwitchAsync(Builtin(service, ProfileService.FocusId));

        VerifyEverydaySnapshots(1);
    }

    [Fact]
    public async Task SwitchAsync_IntoFocus_WritesFocusWithMachineDcAndKeepsOnlyChangedSettingsAsEveryday()
    {
        GivenTheMachineHas(
            new SettingChoice("gaming-game-mode", new ChoiceValue.Toggle(false)),
            new SettingChoice("processor-min-state", new ChoiceValue.AcDcNumber(5, 30)),
            new SettingChoice("usb-selective-suspend", new ChoiceValue.AcDcOption(1, 1)),
            new SettingChoice("unrelated", new ChoiceValue.Toggle(true)));
        var service = CreateService();

        await service.SwitchAsync(Builtin(service, ProfileService.FocusId));

        WrittenTo("everyday").Settings.Select(c => c.SettingId).Should().BeEquivalentTo("gaming-game-mode", "processor-min-state", "usb-selective-suspend");
        var focus = WrittenTo("focus").Settings.ToDictionary(c => c.SettingId, c => c.Value);
        focus["gaming-game-mode"].Should().Be(new ChoiceValue.Toggle(true));
        focus["processor-min-state"].Should().Be(new ChoiceValue.AcDcNumber(100, 30));
        focus["usb-selective-suspend"].Should().Be(new ChoiceValue.AcDcOption(0, 1));
        focus.Keys.Should().NotContain(SettingsFocusLeavesAlone);
        VerifyAppliedWithoutThePolicyCleanup();
    }

    [Fact]
    public async Task SwitchAsync_ToEverydayBeforeAnyFocusSwitch_UsesWindowsDefaultsForWhatFocusChanges()
    {
        GivenTheMachineHas(new SettingChoice("processor-min-state", new ChoiceValue.AcDcNumber(100, 30)));
        _registry.Setup(r => r.GetById(It.Is<string>(id => FocusProfile.Ids.Contains(id)), CatalogScope.CurrentMachine))
            .Returns((string id, CatalogScope _) => SettingCatalog.Find(id));
        _configLoad.Setup(c => c.ReadEmbeddedConfigurationAsync(true)).ReturnsAsync(new WinhanceConfigFile
        {
            Optimize = new()
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Gaming"] = new()
                    {
                        IsIncluded = true,
                        Items =
                        [
                            new ConfigurationItem { Id = "processor-min-state", PowerSettings = new() { ["ACValue"] = 5, ["DCValue"] = 5 } },
                            new ConfigurationItem { Id = "gaming-game-mode", IsSelected = false },
                            new ConfigurationItem { Id = "a", IsSelected = true },
                        ],
                    },
                },
            },
        });
        var service = CreateService();

        var result = await service.SwitchAsync(Builtin(service, ProfileService.EverydayId));

        result!.Success.Should().BeTrue();
        WrittenTo("everyday-defaults").Settings.Should().Equal(
            new SettingChoice("processor-min-state", new ChoiceValue.AcDcNumber(5, 30)),
            new SettingChoice("gaming-game-mode", new ChoiceValue.Toggle(false)));
        VerifyEverydaySnapshots(0);
        VerifyAppliedWithoutThePolicyCleanup();
    }

    private const string OldestBackup = @"C:\b\UserBackup_20250101_120000.winhance";

    private void GivenTwoBackups()
    {
        _fileSystem.Setup(f => f.GetFiles(It.Is<string>(p => p.EndsWith(@"\Backup", StringComparison.Ordinal)), It.IsAny<string>(), It.IsAny<SearchOption>()))
            .Returns([@"C:\b\UserBackup_20260101_120000.winhance", OldestBackup]);
        _configLoad.Setup(c => c.ParseConfiguration(It.IsAny<byte[]>(), OldestBackup)).Returns(() => Config("a"));
    }

    private void VerifyBeforeWinhanceNotWritten() =>
        _fileSystem.Verify(f => f.WriteAllBytesAsync(It.Is<string>(p => IsBuiltinFile(p, "before-winhance")), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);

    [Theory]
    [InlineData("", true)]
    [InlineData("builtin:recommended", false)]
    [InlineData("Gaming", false)]
    public async Task EnsureBeforeWinhanceAsync_WithBackups_BuildsFromOldestAndBecomesActiveOnlyWhenNoneChosen(string active, bool becomesActive)
    {
        GivenGamingIsSaved();
        GivenTwoBackups();
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.ActiveProfile, string.Empty)).Returns(active);
        byte[]? written = null;
        _fileSystem.Setup(f => f.WriteAllBytesAsync(It.Is<string>(p => IsBuiltinFile(p, "before-winhance")), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback((string _, byte[] bytes, CancellationToken _) => written = bytes)
            .Returns(Task.CompletedTask);

        await CreateService().EnsureBeforeWinhanceAsync();

        _fileSystem.Verify(f => f.ReadAllBytesAsync(OldestBackup, It.IsAny<CancellationToken>()), Times.Once);
        written.Should().NotBeNull();
        var times = becomesActive ? Times.Once() : Times.Never();
        _preferences.Verify(p => p.SetPreferenceAsync(UserPreferenceKeys.ActiveProfile, ProfileService.BeforeWinhanceId), times);
        _preferences.Verify(p => p.SetPreferenceAsync(UserPreferenceKeys.ActiveProfileFailed, false), times);
    }

    [Fact]
    public async Task EnsureBeforeWinhanceAsync_WhenAlreadyBuilt_DoesNotBuildAgain()
    {
        GivenGamingIsSaved();
        GivenTwoBackups();
        _fileSystem.Setup(f => f.FileExists(It.Is<string>(p => IsBuiltinFile(p, "before-winhance")))).Returns(true);

        await CreateService().EnsureBeforeWinhanceAsync();

        _fileSystem.Verify(f => f.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()), Times.Never);
        VerifyBeforeWinhanceNotWritten();
    }

    [Fact]
    public async Task EnsureBeforeWinhanceAsync_WithNoBackupYet_WaitsForTheNextLaunch()
    {
        var ensure = () => CreateService().EnsureBeforeWinhanceAsync();

        await ensure.Should().NotThrowAsync();
        VerifyBeforeWinhanceNotWritten();
        _preferences.Verify(p => p.SetPreferenceAsync(UserPreferenceKeys.ActiveProfile, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoadAsync_TwoLoadsOfFocusAtOnce_NeverWriteItsFileAtTheSameTime()
    {
        GivenTheMachineHas(new SettingChoice("gaming-game-mode", new ChoiceValue.Toggle(false)));
        var writing = 0;
        var overlapped = false;
        _configFiles.Setup(w => w.WriteAsync(It.IsAny<SelectionSet>(), It.IsAny<CatalogScope>(), It.IsAny<string>()))
            .Returns(async () =>
            {
                overlapped |= Interlocked.Increment(ref writing) > 1;
                await Task.Delay(50);
                Interlocked.Decrement(ref writing);
                return new byte[] { 4, 5, 6 };
            });
        var service = CreateService();
        var focus = Builtin(service, ProfileService.FocusId);

        await Task.WhenAll(Task.Run(() => service.LoadAsync(focus)), Task.Run(() => service.LoadAsync(focus)));

        overlapped.Should().BeFalse();
    }
}
