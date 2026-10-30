using System.Security.Cryptography;
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
    private readonly Mock<IWindowsRegistryService> _windowsRegistry = new();
    private readonly Mock<IFileSystemService> _fileSystem = new();
    private WinhanceConfigFile? _applied;
    private bool? _restartedExplorer;
    private bool? _broadcastRegional;

    public ProfileServiceTests()
    {
        _localization.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
        _localization.MirrorTryGetString();
        _fileSystem.Setup(f => f.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([1, 2, 3]);
        _interactiveUser.Setup(i => i.GetInteractiveUserFolderPath(It.IsAny<Environment.SpecialFolder>())).Returns(@"C:\Users\u\AppData\Local");
        _interactiveUser.Setup(i => i.InteractiveUserSid).Returns(Sid);
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
        _configuration.Object,
        _windowsRegistry.Object);

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

    private void GivenTheProfileParsesTo(Func<WinhanceConfigFile?> config) =>
        _configLoad.Setup(c => c.ParseConfiguration(It.IsAny<byte[]>(), Gaming.FilePath!)).Returns(config);

    // GetProfiles lists the folder, so Find knows Gaming once it is saved.
    private void GivenGamingIsSaved()
    {
        _fileSystem.Setup(f => f.DirectoryExists(It.IsAny<string>())).Returns(true);
        _fileSystem.Setup(f => f.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>())).Returns([Gaming.FilePath!]);
        _fileSystem.Setup(f => f.GetFileNameWithoutExtension(Gaming.FilePath!)).Returns("Gaming");
    }

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
    public async Task A_switch_skips_only_the_settings_the_PC_already_reads_as_set()
    {
        GivenTheMachineReads(("a", SettingMatch.Matches), ("b", SettingMatch.Differs), ("c", SettingMatch.Unknown));

        var result = await CreateService().SwitchAsync(Gaming);

        AppliedIds().Should().BeEquivalentTo("b", "c", "gone");
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task A_setting_that_still_reads_differently_after_the_apply_fails_the_switch()
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
    public async Task A_profile_that_is_already_in_place_writes_nothing()
    {
        GivenTheMachineReads(("a", SettingMatch.Matches), ("b", SettingMatch.Matches), ("c", SettingMatch.Matches));
        GivenTheProfileHolds("a", "b", "c");

        var result = await CreateService().SwitchAsync(Gaming);

        result!.Success.Should().BeTrue();
        _execution.Verify(e => e.ApplySettingsQuietlyAsync(It.IsAny<WinhanceConfigFile>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    private static readonly SettingsProfile Recommended = new("builtin:recommended", "Recommended", ProfileKind.Recommended, null);

    [Theory]
    [InlineData("", false, 1)]
    [InlineData("Gaming", false, 0)]
    [InlineData("builtin:recommended", true, 0)]
    public async Task A_backup_is_made_only_when_switching_to_another_profile(string active, bool reapply, int backups)
    {
        GivenTheMachineReads(("a", SettingMatch.Differs), ("b", SettingMatch.Matches), ("c", SettingMatch.Matches));
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.ActiveProfile, string.Empty)).Returns(active);
        var service = CreateService();

        if (reapply)
            await service.ReapplyAsync(Recommended);
        else
            await service.SwitchAsync(Gaming);

        _configuration.Verify(c => c.CreateUserBackupConfigAsync(), Times.Exactly(backups));
    }

    [Theory]
    [InlineData("intl", false, true)]
    [InlineData("Explorer", true, false)]
    [InlineData(null, false, false)]
    public async Task A_switch_restarts_Explorer_and_broadcasts_only_for_the_settings_that_need_it(string? restart, bool explorer, bool regional)
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
    public async Task A_re_apply_asks_for_a_restart_only_for_a_setting_it_saw_change(bool reapply, SettingMatch before, SettingMatch after, bool restart)
    {
        _registry.Setup(r => r.GetById("a", CatalogScope.CurrentMachine)).Returns(Toggle("a") with { Apply = new ApplyBehavior { RequiresReboot = true } });
        GivenTheMachineReads(
            [("a", before), ("b", SettingMatch.Matches), ("c", SettingMatch.Matches)],
            [("a", after)]);
        var service = CreateService();

        var result = reapply ? await service.ReapplyAsync(Recommended) : await service.SwitchAsync(Recommended);

        result!.NeedsRestart.Should().HaveCount(restart ? 1 : 0);
    }

    [Fact]
    public async Task Exporting_a_saved_profile_writes_a_new_file_with_its_bytes()
    {
        var exported = await CreateService().ExportAsync(Gaming, @"D:\out\Gaming.winhance");

        exported.Should().BeTrue();
        _fileSystem.Verify(f => f.WriteAllBytesAsync(@"D:\out\Gaming.winhance", It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 1, 2, 3 })), It.IsAny<CancellationToken>()), Times.Once);
        _fileSystem.Verify(f => f.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task A_profile_whose_section_is_null_switches_without_throwing()
    {
        GivenTheProfileParsesTo(() => new WinhanceConfigFile { Optimize = null! });
        GivenTheMachineReads();

        var result = await CreateService().SwitchAsync(Gaming);

        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task A_profile_item_with_no_id_is_skipped_rather_than_failing_the_switch()
    {
        GivenTheProfileHolds("a", null);
        GivenTheMachineReads(("a", SettingMatch.Differs));

        var result = await CreateService().SwitchAsync(Gaming);

        result!.Success.Should().BeTrue();
        AppliedIds().Should().Equal("a");
    }

    [Fact]
    public async Task Drift_of_a_profile_with_a_null_section_is_empty()
    {
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.ActiveProfile, string.Empty)).Returns("builtin:recommended");
        _configLoad.Setup(c => c.ReadEmbeddedConfigurationAsync(false)).ReturnsAsync(new WinhanceConfigFile { Optimize = null! });

        (await CreateService().DriftAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Drift_filters_the_profile_to_this_system()
    {
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.ActiveProfile, string.Empty)).Returns("builtin:recommended");
        GivenTheMachineReads(("a", SettingMatch.Matches), ("b", SettingMatch.Matches), ("c", SettingMatch.Matches));

        await CreateService().DriftAsync();

        _configLoad.Verify(c => c.FilterConfigForCurrentSystem(It.IsAny<WinhanceConfigFile>()), Times.Once);
    }

    [Fact]
    public async Task Drift_reports_none_when_the_check_throws()
    {
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.ActiveProfile, string.Empty)).Returns("builtin:recommended");
        _snapshot.Setup(s => s.CompareAsync(It.IsAny<IReadOnlyList<SettingChoice>>(), It.IsAny<IReadOnlyList<CarriedFile>>(), CatalogScope.CurrentMachine)).ThrowsAsync(new InvalidOperationException("boom"));

        (await CreateService().DriftAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Drift_names_a_setting_by_its_setting_name_when_the_translation_is_missing()
    {
        var name = TestKeys.Of("b").Value;
        _localization.MissingKey(name);
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.ActiveProfile, string.Empty)).Returns("builtin:recommended");
        GivenTheMachineReads(("a", SettingMatch.Matches), ("b", SettingMatch.Differs), ("c", SettingMatch.Matches));

        (await CreateService().DriftAsync()).Should().Equal(name);
    }

    [Fact]
    public async Task A_profile_that_fails_to_load_does_not_become_the_active_profile()
    {
        GivenTheProfileParsesTo(() => null);

        var result = await CreateService().SwitchAsync(Gaming);

        result!.Success.Should().BeFalse();
        _preferences.Verify(p => p.SetPreferenceAsync(UserPreferenceKeys.ActiveProfile, It.IsAny<string>()), Times.Never);
        _preferences.Verify(p => p.SetPreferenceAsync(UserPreferenceKeys.ActiveProfileFailed, true), Times.Once);
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
    public void A_Windows_device_name_is_not_an_available_profile_name(string name, bool available) =>
        CreateService().IsNameAvailable(name).Should().Be(available);

    private const string Sid = "S-1-5-21-test";
    private const string TrustKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Winhance\ProfileHashes\" + Sid;

    private static string HashOf(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static string HashOfStub => HashOf([1, 2, 3]);

    // A registry that remembers, so a record, a delete and a later check see each other.
    private Dictionary<(string Key, string Name), object> GivenAWorkingRegistry()
    {
        var values = new Dictionary<(string Key, string Name), object>();
        _windowsRegistry.Setup(r => r.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>()))
            .Callback((string key, string name, object value, RegistryValueKind _) => values[(key, name)] = value)
            .Returns(true);
        _windowsRegistry.Setup(r => r.GetValue(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string key, string name) => values.GetValueOrDefault((key, name)));
        _windowsRegistry.Setup(r => r.DeleteValue(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string key, string name) => values.Remove((key, name)));
        return values;
    }

    private void VerifyNothingApplied() =>
        _execution.Verify(e => e.ApplySettingsQuietlyAsync(It.IsAny<WinhanceConfigFile>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);

    [Fact]
    public async Task A_switch_records_the_SHA256_of_the_profile_file_under_the_users_SID()
    {
        GivenTheMachineReads(("a", SettingMatch.Differs), ("b", SettingMatch.Matches), ("c", SettingMatch.Matches));

        await CreateService().SwitchAsync(Gaming);

        _windowsRegistry.Verify(r => r.SetValue(TrustKey, "Gaming", HashOfStub, RegistryValueKind.String), Times.Once);
        _windowsRegistry.Verify(r => r.SetValue(It.Is<string>(k => k != TrustKey), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Never);
    }

    [Fact]
    public async Task A_switch_reads_the_file_once_and_applies_the_bytes_it_hashed()
    {
        GivenTheMachineReads(("a", SettingMatch.Differs), ("b", SettingMatch.Matches), ("c", SettingMatch.Matches));

        await CreateService().SwitchAsync(Gaming);

        _fileSystem.Verify(f => f.ReadAllBytesAsync(Gaming.FilePath!, It.IsAny<CancellationToken>()), Times.Once);
        _configLoad.Verify(c => c.ParseConfiguration(It.Is<byte[]>(b => b.SequenceEqual(new byte[] { 1, 2, 3 })), Gaming.FilePath!), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("DEADBEEF")]
    public async Task The_sign_in_reapply_skips_a_profile_file_that_does_not_match_its_recorded_hash(string? recorded)
    {
        GivenTheMachineReads(("a", SettingMatch.Differs), ("b", SettingMatch.Matches), ("c", SettingMatch.Matches));
        _windowsRegistry.Setup(r => r.GetValue(TrustKey, "Gaming")).Returns(recorded);

        var result = await CreateService().ReapplyAsync(Gaming);

        result!.Success.Should().BeFalse();
        VerifyNothingApplied();
        _windowsRegistry.Verify(r => r.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Never);
        _preferences.Verify(p => p.SetPreferenceAsync(UserPreferenceKeys.ActiveProfileFailed, true), Times.Once);
    }

    [Fact]
    public async Task The_sign_in_reapply_applies_a_profile_file_that_matches_its_recorded_hash()
    {
        GivenTheMachineReads(("a", SettingMatch.Differs), ("b", SettingMatch.Matches), ("c", SettingMatch.Matches));
        _windowsRegistry.Setup(r => r.GetValue(TrustKey, "Gaming")).Returns(HashOfStub.ToLowerInvariant());

        var result = await CreateService().ReapplyAsync(Gaming);

        result!.Success.Should().BeTrue();
        _applied.Should().NotBeNull();
    }

    [Fact]
    public async Task The_sign_in_reapply_ignores_a_hash_recorded_for_another_user()
    {
        GivenTheMachineReads(("a", SettingMatch.Differs), ("b", SettingMatch.Matches), ("c", SettingMatch.Matches));
        _windowsRegistry.Setup(r => r.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Winhance\ProfileHashes\S-1-5-21-other", "Gaming")).Returns(HashOfStub);

        var result = await CreateService().ReapplyAsync(Gaming);

        result!.Success.Should().BeFalse();
        VerifyNothingApplied();
    }

    [Fact]
    public async Task The_sign_in_reapply_skips_a_profile_file_it_cannot_read()
    {
        _windowsRegistry.Setup(r => r.GetValue(TrustKey, "Gaming")).Returns(HashOfStub);
        _fileSystem.Setup(f => f.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("locked"));

        var result = await CreateService().ReapplyAsync(Gaming);

        result!.Success.Should().BeFalse();
        result.NotApplied.Should().Equal("Profiles_Error_Untrusted");
        VerifyNothingApplied();
    }

    [Fact]
    public async Task Deleting_a_profile_removes_its_hash_so_a_file_recreated_under_the_same_name_is_not_trusted()
    {
        var values = GivenAWorkingRegistry();
        GivenTheMachineReads(("a", SettingMatch.Differs), ("b", SettingMatch.Matches), ("c", SettingMatch.Matches));
        var service = CreateService();
        await service.SwitchAsync(Gaming);
        values.Should().ContainKey((TrustKey, "Gaming"));

        await service.DeleteAsync(Gaming);
        _applied = null;
        var result = await service.ReapplyAsync(Gaming);

        values.Should().NotContainKey((TrustKey, "Gaming"));
        result!.Success.Should().BeFalse();
        _applied.Should().BeNull();
    }

    [Fact]
    public async Task Saving_the_active_profile_records_the_hash_of_the_bytes_it_wrote()
    {
        GivenGamingIsSaved();
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.ActiveProfile, string.Empty)).Returns("Gaming");
        _registry.Setup(r => r.GetFeatureIdForSetting("a")).Returns(FeatureIds.GamingPerformance);
        byte[] written = [7, 8, 9];
        _configFiles.Setup(w => w.WriteAsync(It.IsAny<SelectionSet>(), CatalogScope.CurrentMachine, It.IsAny<string>())).ReturnsAsync(written);

        await CreateService().SaveAsync("Gaming", [new("a", new ChoiceValue.Toggle(true))]);

        _windowsRegistry.Verify(r => r.SetValue(TrustKey, "Gaming", HashOf(written), RegistryValueKind.String), Times.Once);
    }

    [Fact]
    public async Task Saving_a_profile_that_is_not_active_records_no_hash()
    {
        GivenGamingIsSaved();
        _registry.Setup(r => r.GetFeatureIdForSetting("a")).Returns(FeatureIds.GamingPerformance);

        await CreateService().SaveAsync("Gaming", [new("a", new ChoiceValue.Toggle(true))]);

        _configFiles.Verify(w => w.WriteAsync(It.IsAny<SelectionSet>(), It.IsAny<CatalogScope>(), It.IsAny<string>()), Times.Once);
        _windowsRegistry.Verify(r => r.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Never);
    }

    [Fact]
    public async Task Drift_names_only_the_settings_that_positively_read_differently()
    {
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.ActiveProfile, string.Empty)).Returns("builtin:recommended");
        GivenTheMachineReads(("a", SettingMatch.Matches), ("b", SettingMatch.Differs), ("c", SettingMatch.Unknown));

        (await CreateService().DriftAsync()).Should().Equal("b");
    }

    [Fact]
    public async Task A_profile_saved_from_Builder_keeps_only_this_PCs_settings()
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
}
