using FluentAssertions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class TrayStatusTests
{
    private static readonly VersionInfo Current = VersionInfo.FromTag("v26.11.01");

    private sealed class FakePreferences : IUserPreferencesService
    {
        public Dictionary<string, object> Values { get; } = [];

        public Task<Dictionary<string, object>> GetPreferencesAsync() => Task.FromResult(Values);

        public Task<OperationResult> SavePreferencesAsync(Dictionary<string, object> preferences) => Task.FromResult(OperationResult.Succeeded());

        public Task<T> GetPreferenceAsync<T>(string key, T defaultValue) => Task.FromResult(GetPreference(key, defaultValue));

        public Task<OperationResult> SetPreferenceAsync<T>(string key, T value)
        {
            Values[key] = value!;
            return Task.FromResult(OperationResult.Succeeded());
        }

        public T GetPreference<T>(string key, T defaultValue) => Values.TryGetValue(key, out var v) && v is T t ? t : defaultValue;
    }

    [Theory]
    [InlineData(false, null, 0, TrayBadge.None)]
    [InlineData(false, null, 2, TrayBadge.AppUpdates)]
    [InlineData(false, "v26.11.20", 2, TrayBadge.WinhanceUpdate)]
    [InlineData(true, "v26.11.20", 2, TrayBadge.Attention)]
    [InlineData(true, null, 0, TrayBadge.Attention)]
    public void Resolve_Always_PicksTheMostUrgentState(bool attention, string? version, int apps, TrayBadge expected) =>
        TrayStatus.Resolve(attention, version, apps).Should().Be(expected);

    [Fact]
    public async Task SaveAsync_ThenLoad_KeepsTheState()
    {
        var preferences = new FakePreferences();
        var status = new TrayStatus(3, "v26.11.20", true);

        await status.SaveAsync(preferences);

        TrayStatus.Load(preferences, Current).Should().Be(status);
    }

    [Fact]
    public async Task Load_WithSavedUpdateTheRunningVersionAlreadyHas_DropsIt()
    {
        var preferences = new FakePreferences();
        await new TrayStatus(0, "v26.11.01", false).SaveAsync(preferences);

        TrayStatus.Load(preferences, Current).WinhanceVersion.Should().BeNull();
    }

    [Fact]
    public async Task RecordWinhanceUpdateAsync_SameVersionTwice_AnnouncesItOnce()
    {
        var preferences = new FakePreferences();

        (await TrayStatus.RecordWinhanceUpdateAsync(preferences, "v26.11.20", Current)).Should().BeTrue();
        (await TrayStatus.RecordWinhanceUpdateAsync(preferences, "v26.11.20", Current)).Should().BeFalse();
        (await TrayStatus.RecordWinhanceUpdateAsync(preferences, "v26.12.01", Current)).Should().BeTrue();
        TrayStatus.Load(preferences, Current).WinhanceVersion.Should().Be("v26.12.01");
    }

    [Fact]
    public async Task RecordWinhanceUpdateAsync_WhenWinhanceIsCurrent_ClearsTheUpdateStateWithoutAnnouncing()
    {
        var preferences = new FakePreferences();
        await TrayStatus.RecordWinhanceUpdateAsync(preferences, "v26.11.20", Current);

        (await TrayStatus.RecordWinhanceUpdateAsync(preferences, null, Current)).Should().BeFalse();
        TrayStatus.Load(preferences, Current).WinhanceVersion.Should().BeNull();
    }

    [Theory]
    [InlineData("DarkNative", TrayMenuTheme.Dark)]
    [InlineData("LightNative", TrayMenuTheme.Light)]
    [InlineData("System", TrayMenuTheme.System)]
    [InlineData("", TrayMenuTheme.System)]
    public void MenuThemeFor_SavedPreference_MapsToTheMenuTheme(string preference, TrayMenuTheme expected) =>
        TrayStatus.MenuThemeFor(preference).Should().Be(expected);
}
