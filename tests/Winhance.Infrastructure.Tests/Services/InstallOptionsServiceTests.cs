using System.Text.Json;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class InstallOptionsServiceTests
{
    private static readonly InstallOptions Arm64 = new() { Architecture = InstallArchitecture.Arm64 };

    private readonly Mock<IUserPreferencesService> _preferences = new();
    private InstallOptionsStore? _saved;

    public InstallOptionsServiceTests()
    {
        _preferences
            .Setup(p => p.SetPreferenceAsync(UserPreferenceKeys.AppInstallOptions, It.IsAny<InstallOptionsStore>()))
            .Callback<string, InstallOptionsStore>((_, store) => _saved = store)
            .ReturnsAsync(OperationResult.Succeeded());
    }

    private InstallOptionsService CreateSut() => new(_preferences.Object);

    private void ArrangeSavedStore(InstallOptionsStore store) => _preferences
        .Setup(p => p.GetPreference<InstallOptionsStore?>(UserPreferenceKeys.AppInstallOptions, null))
        .Returns(store);

    [Fact]
    public void Defaults_NothingSaved_LetsTheInstallerDecide()
    {
        CreateSut().Defaults.Should().Be(InstallOptions.Default);
    }

    [Fact]
    public void Defaults_NothingSavedAndTheOldAllUsersSwitchOn_InstallsForEveryone()
    {
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.InstallAppsForAllUsers, false)).Returns(true);

        CreateSut().Defaults.Scope.Should().Be(InstallScope.Machine);
    }

    [Fact]
    public void Defaults_SavedStore_WinsOverTheOldAllUsersSwitch()
    {
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.InstallAppsForAllUsers, false)).Returns(true);
        ArrangeSavedStore(new InstallOptionsStore { Defaults = Arm64 });

        CreateSut().Defaults.Should().Be(Arm64);
    }

    [Fact]
    public void Store_IsReadOnceAndKept()
    {
        var sut = CreateSut();

        _ = sut.Defaults;
        _ = sut.ForApp("winget-VideoLAN.VLC");
        _ = sut.EffectiveFor("winget-VideoLAN.VLC");

        _preferences.Verify(p => p.GetPreference<InstallOptionsStore?>(UserPreferenceKeys.AppInstallOptions, null), Times.Once);
    }

    [Fact]
    public async Task EffectiveFor_AppWithItsOwnOptions_ReplacesTheDefaults()
    {
        var sut = CreateSut();
        await sut.SetDefaultsAsync(new InstallOptions { Scope = InstallScope.Machine, Interactive = true });

        await sut.SetForAppAsync("winget-VideoLAN.VLC", Arm64);

        sut.EffectiveFor("winget-VideoLAN.VLC").Should().Be(Arm64);
        sut.EffectiveFor("winget-Git.Git").Should().Be(sut.Defaults);
    }

    [Fact]
    public async Task SetForAppAsync_Null_FollowsTheDefaultsAgain()
    {
        var sut = CreateSut();
        await sut.SetForAppAsync("winget-VideoLAN.VLC", Arm64);

        await sut.SetForAppAsync("winget-VideoLAN.VLC", null);

        sut.ForApp("winget-VideoLAN.VLC").Should().BeNull();
        _saved!.Apps.Should().BeEmpty();
    }

    [Fact]
    public async Task SetForAppAsync_WritesTheWholeStoreBack()
    {
        var defaults = new InstallOptions { Scope = InstallScope.User };
        var sut = CreateSut();
        await sut.SetDefaultsAsync(defaults);

        await sut.SetForAppAsync("winget-VideoLAN.VLC", Arm64);

        var saved = _saved!;
        saved.Defaults.Should().Be(defaults);
        saved.Apps.Should().ContainKey("winget-VideoLAN.VLC").WhoseValue.Should().Be(Arm64);
    }

    [Fact]
    public void Store_SurvivesTheJsonRoundTripThePreferencesFileMakes()
    {
        var store = new InstallOptionsStore
        {
            Defaults = new InstallOptions { Scope = InstallScope.Machine, Interactive = true },
            Apps = new Dictionary<string, InstallOptions>(StringComparer.Ordinal) { ["winget-VideoLAN.VLC"] = Arm64 },
        };
        var file = JsonSerializer.Serialize(new Dictionary<string, object> { [UserPreferenceKeys.AppInstallOptions] = store });

        var read = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(file)![UserPreferenceKeys.AppInstallOptions]
            .Deserialize<InstallOptionsStore>()!;

        read.Defaults.Should().Be(store.Defaults);
        read.Apps.Should().ContainKey("winget-VideoLAN.VLC").WhoseValue.Should().Be(Arm64);
    }

    [Fact]
    public async Task Session_StandsInForTheSavedOptions_AndChangesStayOutOfThem()
    {
        var saved = new InstallOptionsStore { Defaults = Arm64 };
        ArrangeSavedStore(saved);
        var sut = CreateSut();
        var carried = new InstallOptionsStore { Defaults = InstallOptions.Default with { Scope = InstallScope.User } };

        sut.BeginSession(carried);
        await sut.SetForAppAsync("winget-VideoLAN.VLC", Arm64);

        sut.Defaults.Should().Be(carried.Defaults);
        sut.EffectiveFor("winget-VideoLAN.VLC").Should().Be(Arm64);
        _preferences.Verify(p => p.SetPreferenceAsync(UserPreferenceKeys.AppInstallOptions, It.IsAny<InstallOptionsStore>()), Times.Never);

        sut.EndSession();

        sut.Current.Should().BeSameAs(saved);
    }

    [Fact]
    public void BeginSession_WhileOneIsOpen_ThrowsAndKeepsTheOpenOne()
    {
        var sut = CreateSut();
        var outer = new InstallOptionsStore { Defaults = Arm64 };
        sut.BeginSession(outer);

        var nest = () => sut.BeginSession(new InstallOptionsStore());

        nest.Should().Throw<InvalidOperationException>();
        sut.Current.Should().BeSameAs(outer);
    }

    [Fact]
    public void BeginSession_AfterTheLastOneEnded_Opens()
    {
        var sut = CreateSut();
        sut.BeginSession(new InstallOptionsStore());
        sut.EndSession();
        var next = new InstallOptionsStore { Defaults = Arm64 };

        sut.BeginSession(next);

        sut.Current.Should().BeSameAs(next);
    }
}
