using System.Globalization;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.Services;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class AppUpdateMonitorTests
{
    private readonly Mock<IExternalAppsService> _externalAppsService = new();
    private readonly Mock<IWindowsAppsService> _windowsAppsService = new();
    private readonly Mock<IInstalledAppsService> _service = new();
    private readonly Mock<ITaskProgressService> _progress = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IDispatcherService> _dispatcher = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly Mock<IUserPreferencesService> _preferences = new();
    private readonly Mock<IVersionService> _version = new();
    private readonly List<AppUpdateNotice> _notices = [];
    private readonly Dictionary<string, object?> _saved = [];
    private readonly List<ItemDefinition> _windowsItems = [];

    public AppUpdateMonitorTests()
    {
        _localization.Setup(l => l.GetString(It.IsAny<string>())).Returns<string>(k => k);
        _localization.Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns<string, object[]>((key, args) => $"{key}: {string.Join(", ", args)}");
        _dispatcher.Setup(d => d.RunOnUIThreadWithContextAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(f => f());
        _dispatcher.Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(f => f());
        _externalAppsService.Setup(s => s.GetAppsAsync()).ReturnsAsync([]);
        _windowsAppsService.Setup(s => s.GetAppsAsync()).ReturnsAsync(() => _windowsItems.ToList());
        _progress.Setup(p => p.CreateDetailedProgress()).Returns(new Progress<TaskProgressDetail>());
        _progress.Setup(p => p.StartTask(It.IsAny<string>(), It.IsAny<bool>())).Returns(new CancellationTokenSource());
        _preferences.Setup(p => p.SetPreferenceAsync(It.IsAny<string>(), It.IsAny<It.IsAnyType>()))
            .Callback(new InvocationAction(invocation => _saved[(string)invocation.Arguments[0]] = invocation.Arguments[1]))
            .ReturnsAsync(OperationResult.Succeeded());
        _preferences.Setup(p => p.GetPreference(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string key, string fallback) => _saved.TryGetValue(key, out var v) ? v as string ?? fallback : fallback);
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.AppUpdateAction, It.IsAny<AppUpdateAction>()))
            .Returns((string _, AppUpdateAction fallback) => fallback);
        _service.Setup(s => s.UpdateAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));
    }

    private AppUpdateMonitor CreateSut(params InstalledApp[] apps)
    {
        _service.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => new InstalledAppsScan(apps, true, _windowsItems.ToDictionary(d => d.Id, _ => true)));
        var externalApps = new ExternalAppsViewModel(
            _externalAppsService.Object,
            _service.Object,
            new Mock<IAppInstallationService>().Object,
            new Mock<IStagedAppInstaller>().Object,
            new Mock<IInstallOptionsService>().Object,
            _progress.Object,
            _log.Object,
            new Mock<IDialogService>().Object,
            _localization.Object,
            _dispatcher.Object,
            new Mock<IThemeService>().Object);
        var windowsApps = new WindowsAppsViewModel(
            _windowsAppsService.Object,
            _service.Object,
            new Mock<IAppInstallationService>().Object,
            new Mock<IWindowsAppUninstallService>().Object,
            _progress.Object,
            _log.Object,
            new Mock<IDialogService>().Object,
            _localization.Object,
            _dispatcher.Object,
            new Mock<IThemeService>().Object);
        var sut = new AppUpdateMonitor(externalApps, windowsApps, _preferences.Object, _localization.Object, _version.Object, _log.Object);
        sut.NoticeRaised += (_, notice) => _notices.Add(notice);
        return sut;
    }

    private static InstalledApp WindowsApp(string catalogId, string version, string? availableVersion) =>
        new($"appx:{catalogId}", catalogId, InstalledAppKind.StoreApp)
        {
            CatalogId = catalogId,
            Version = version,
            AvailableVersion = availableVersion,
            UpdateVia = availableVersion is null ? PackageSources.None : PackageSources.WinGet,
        };

    private void WindowsCatalog(params string[] ids) =>
        _windowsItems.AddRange(ids.Select(id => new ItemDefinition { Id = id, Name = id, Description = id, AppxPackageName = [id] }));

    private static InstalledApp App(string name, string? availableVersion = null) =>
        new($"reg:machine:{name}", name, InstalledAppKind.Program)
        {
            WinGetId = name,
            UpdateVia = availableVersion is null ? PackageSources.None : PackageSources.WinGet,
            AvailableVersion = availableVersion,
        };

    [Theory]
    [InlineData(AppUpdateCheckFrequency.Off, null, false)]
    [InlineData(AppUpdateCheckFrequency.Daily, null, true)]
    [InlineData(AppUpdateCheckFrequency.Daily, 23, false)]
    [InlineData(AppUpdateCheckFrequency.Daily, 25, true)]
    [InlineData(AppUpdateCheckFrequency.Weekly, 100, false)]
    [InlineData(AppUpdateCheckFrequency.Weekly, 169, true)]
    public void IsDue_FrequencyAndLastCheck_IsTrueOnceItsIntervalHasPassed(AppUpdateCheckFrequency frequency, int? hoursAgo, bool due)
    {
        var now = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
        DateTime? last = hoursAgo is { } h ? now.AddHours(-h) : null;

        AppUpdateMonitor.IsDue(frequency, last, now).Should().Be(due);
    }

    [Fact]
    public void IsDue_WithSavedFrequencyAndLastCheck_ReadsBothFromPreferences()
    {
        var now = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.AppUpdateCheckFrequency, It.IsAny<AppUpdateCheckFrequency>()))
            .Returns(AppUpdateCheckFrequency.Daily);
        _saved[UserPreferenceKeys.LastAppUpdateCheck] = now.AddHours(-2).ToString("o", CultureInfo.InvariantCulture);

        AppUpdateMonitor.IsDue(_preferences.Object, now).Should().BeFalse();
        AppUpdateMonitor.IsDue(_preferences.Object, now.AddDays(1)).Should().BeTrue();
    }

    [Fact]
    public async Task CheckWinhanceAsync_WhenChecksAreOnOrOff_ChecksOnceADayAndNeverWhenOff()
    {
        var now = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
        var frequency = AppUpdateCheckFrequency.Weekly;
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.AppUpdateCheckFrequency, It.IsAny<AppUpdateCheckFrequency>()))
            .Returns(() => frequency);
        _version.Setup(v => v.CheckForUpdateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VersionInfo { Version = "v26.11.20", IsUpdateAvailable = true });

        AppUpdateMonitor.WinhanceCheckIsDue(_preferences.Object, now).Should().BeTrue();
        var (done, version) = await AppUpdateMonitor.CheckWinhanceAsync(_version.Object, _preferences.Object, now);

        done.Should().BeTrue();
        version.Should().Be("v26.11.20");
        AppUpdateMonitor.WinhanceCheckIsDue(_preferences.Object, now.AddHours(23)).Should().BeFalse();
        AppUpdateMonitor.WinhanceCheckIsDue(_preferences.Object, now.AddHours(25)).Should().BeTrue();
        frequency = AppUpdateCheckFrequency.Off;
        AppUpdateMonitor.WinhanceCheckIsDue(_preferences.Object, now.AddDays(5)).Should().BeFalse();
    }

    [Fact]
    public async Task CheckWinhanceAsync_WhenGitHubUnreachable_IsNotRecordedSoItRunsAgain()
    {
        _version.Setup(v => v.CheckForUpdateAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new VersionInfo());

        var (done, _) = await AppUpdateMonitor.CheckWinhanceAsync(_version.Object, _preferences.Object, DateTime.UtcNow);

        done.Should().BeFalse();
        _saved.Should().NotContainKey(UserPreferenceKeys.LastWinhanceUpdateCheck);
    }

    [Fact]
    public async Task CheckAsync_WhenFinished_ReportsHowManyUpdatesAreLeft()
    {
        var sut = CreateSut(App("Git", "2.46.0"), App("VLC", "3.0.21"));
        var outcomes = new List<AppUpdateOutcome>();
        sut.Finished += (_, outcome) => outcomes.Add(outcome);

        await sut.CheckAsync(manual: false);

        outcomes.Should().ContainSingle().Which.Should().Be(new AppUpdateOutcome(2, false));
    }

    [Fact]
    public async Task CheckAsync_WithWindowsAppUpdates_CountsThemWithTheOthers()
    {
        WindowsCatalog("terminal", "calc");
        var sut = CreateSut(App("Git", "2.46.0"), WindowsApp("terminal", "1.24.0", "1.25.0"), WindowsApp("calc", "11.0", null));
        var outcomes = new List<AppUpdateOutcome>();
        sut.Finished += (_, outcome) => outcomes.Add(outcome);

        await sut.CheckAsync(manual: false);

        outcomes.Should().ContainSingle().Which.Should().Be(new AppUpdateOutcome(2, false));
        _notices.Should().ContainSingle().Which.Message.Should().Be("Notify_UpdatesAvailable_Message: 2");
    }

    [Fact]
    public async Task CheckAsync_WithOnlyAWindowsAppUpdate_StillReportsIt()
    {
        WindowsCatalog("terminal");
        var sut = CreateSut(WindowsApp("terminal", "1.24.0", "1.25.0"));

        await sut.CheckAsync(manual: false);

        _notices.Should().ContainSingle().Which.Kind.Should().Be(AppUpdateNoticeKind.Available);
    }

    [Fact]
    public async Task CheckAsync_WithAutomaticInstalls_UpdatesWindowsAppsToo()
    {
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.AppUpdateAction, It.IsAny<AppUpdateAction>()))
            .Returns(AppUpdateAction.Install);
        WindowsCatalog("terminal");
        var windows = WindowsApp("terminal", "1.24.0", "1.25.0");
        var sut = CreateSut(App("Git", "2.46.0"), windows);

        await sut.CheckAsync(manual: false);

        _service.Verify(s => s.UpdateAsync(windows, It.IsAny<CancellationToken>()), Times.Once);
        _service.Verify(s => s.UpdateAsync(It.Is<InstalledApp>(a => a.Name == "Git"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckAsync_ScheduledCheckRunTwice_ReportsUpdatesOnce()
    {
        var sut = CreateSut(App("7-Zip"), App("Git", "2.46.0"));

        await sut.CheckAsync(manual: false);
        await sut.CheckAsync(manual: false);

        _notices.Should().ContainSingle().Which.Kind.Should().Be(AppUpdateNoticeKind.Available);
        _notices[0].Message.Should().Be("Notify_UpdatesAvailable_Message: 1");
        _saved.Should().ContainKey(UserPreferenceKeys.LastAppUpdateCheck);
    }

    [Fact]
    public async Task CheckAsync_ManualCheckWhenUpToDate_SaysEverythingIsUpToDate()
    {
        var sut = CreateSut(App("7-Zip"));

        await sut.CheckAsync(manual: false);
        _notices.Should().BeEmpty();

        await sut.CheckAsync(manual: true);
        _notices.Should().ContainSingle().Which.Kind.Should().Be(AppUpdateNoticeKind.UpToDate);
    }

    [Fact]
    public async Task CheckAsync_AutomaticInstallScheduledCheck_UpdatesEveryAppAndReportsIt()
    {
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.AppUpdateAction, It.IsAny<AppUpdateAction>()))
            .Returns(AppUpdateAction.Install);
        var sut = CreateSut(App("Git", "2.46.0"), App("VLC", "3.0.21"));
        _service.Setup(s => s.UpdateAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()))
            .Callback(() => _service.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>())).ReturnsAsync(new InstalledAppsScan([App("Git"), App("VLC")], true, new Dictionary<string, bool>())))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));

        await sut.CheckAsync(manual: false);

        _service.Verify(s => s.UpdateAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _notices.Should().ContainSingle().Which.Message.Should().Be("Notify_UpdatesInstalled_Message: 2");
        _saved.Should().ContainKey(UserPreferenceKeys.LastAppUpdateCheck);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CheckAsync_ScanFailed_StaysDueAndOnlyAManualCheckSaysSo(bool manual)
    {
        var sut = CreateSut(App("7-Zip"));
        _service.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("winget is broken"));

        await sut.CheckAsync(manual);

        _notices.Select(n => n.Kind).Should().Equal(manual ? [AppUpdateNoticeKind.CheckFailed] : []);
        _saved.Should().NotContainKey(UserPreferenceKeys.LastAppUpdateCheck);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CheckAsync_ScanCouldNotReadUpdates_StaysDueAndOnlyAManualCheckSaysSo(bool manual)
    {
        var sut = CreateSut();
        _service.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([App("7-Zip")], false, new Dictionary<string, bool>()));

        await sut.CheckAsync(manual);

        _notices.Select(n => n.Kind).Should().Equal(manual ? [AppUpdateNoticeKind.CheckFailed] : []);
        _saved.Should().NotContainKey(UserPreferenceKeys.LastAppUpdateCheck);
    }

    [Fact]
    public async Task CheckAsync_AutomaticInstallBehindAnotherTask_StaysDue()
    {
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.AppUpdateAction, It.IsAny<AppUpdateAction>()))
            .Returns(AppUpdateAction.Install);
        _progress.Setup(p => p.IsTaskRunning).Returns(true);
        var sut = CreateSut(App("Git", "2.46.0"));

        await sut.CheckAsync(manual: false);

        _service.Verify(s => s.UpdateAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()), Times.Never);
        _notices.Should().BeEmpty();
        _saved.Should().NotContainKey(UserPreferenceKeys.LastAppUpdateCheck);
    }

    [Fact]
    public async Task CheckAsync_ManualCheckDuringRunningCheck_WaitsForItAndReportsWhatItFound()
    {
        var sut = CreateSut(App("Git", "2.46.0"));
        await sut.CheckAsync(manual: false);
        _notices.Clear();
        _service.Invocations.Clear();
        var scan = new TaskCompletionSource<InstalledAppsScan>();
        _service.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>())).Returns(scan.Task);

        var scheduled = sut.CheckAsync(manual: false);
        var manual = sut.CheckAsync(manual: true);
        manual.IsCompleted.Should().BeFalse();
        scan.SetResult(new InstalledAppsScan([App("Git", "2.46.0")], true, new Dictionary<string, bool>()));
        await Task.WhenAll(scheduled, manual);

        _service.Verify(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()), Times.Once);
        _notices.Should().ContainSingle().Which.Kind.Should().Be(AppUpdateNoticeKind.Available);
        _notices[0].Message.Should().Be("Notify_UpdatesAvailable_Message: 1");
    }

    [Fact]
    public async Task CheckAsync_ManualCheckDuringRunningCheckThatFails_SaysItFailed()
    {
        var sut = CreateSut(App("Git", "2.46.0"));
        await sut.CheckAsync(manual: false);
        _notices.Clear();
        var scan = new TaskCompletionSource<InstalledAppsScan>();
        _service.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>())).Returns(scan.Task);

        var scheduled = sut.CheckAsync(manual: false);
        var manual = sut.CheckAsync(manual: true);
        scan.SetException(new InvalidOperationException("winget is broken"));
        await Task.WhenAll(scheduled, manual);

        _notices.Should().ContainSingle().Which.Kind.Should().Be(AppUpdateNoticeKind.CheckFailed);
    }

    [Fact]
    public async Task RefreshAsync_Always_ScansOnceFeedsTheTrayAndRaisesNoNotice()
    {
        WindowsCatalog("terminal");
        var sut = CreateSut(App("Git", "2.46.0"), WindowsApp("terminal", "1.24.0", "1.25.0"));
        var outcomes = new List<AppUpdateOutcome>();
        await sut.CheckAsync(manual: false);
        _saved.Remove(UserPreferenceKeys.LastAppUpdateCheck);
        _notices.Clear();
        _service.Invocations.Clear();
        sut.Finished += (_, outcome) => outcomes.Add(outcome);

        await sut.RefreshAsync();

        _service.Verify(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()), Times.Once);
        outcomes.Should().ContainSingle().Which.Should().Be(new AppUpdateOutcome(2, false));
        _notices.Should().BeEmpty();
        _saved.Should().NotContainKey(UserPreferenceKeys.LastAppUpdateCheck);
    }

    [Fact]
    public async Task RefreshAsync_WhenUserInstalledUpdateElsewhere_ClearsItOnBothTabs()
    {
        WindowsCatalog("terminal");
        var sut = CreateSut(App("Git", "2.46.0"), WindowsApp("terminal", "1.24.0", "1.25.0"));
        await sut.CheckAsync(manual: false);
        _service.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([App("Git"), WindowsApp("terminal", "1.25.0", null)], true, new Dictionary<string, bool>()));
        var outcomes = new List<AppUpdateOutcome>();
        sut.Finished += (_, outcome) => outcomes.Add(outcome);

        await sut.RefreshAsync();

        outcomes.Should().ContainSingle().Which.Pending.Should().Be(0);
    }

    [Fact]
    public async Task RefreshAsync_WhenFirstScanCouldNotReadUpdates_ScansAgain()
    {
        var sut = CreateSut(App("Git", "2.46.0"));
        await sut.CheckAsync(manual: false);
        var scans = 0;
        _service.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ItemDefinition> _, StatusRules rules, CancellationToken _) => rules == StatusRules.External && ++scans == 1
                ? new InstalledAppsScan([App("Git", "2.46.0")], false, new Dictionary<string, bool>())
                : new InstalledAppsScan([App("Git")], true, new Dictionary<string, bool>()));
        var outcomes = new List<AppUpdateOutcome>();
        sut.Finished += (_, outcome) => outcomes.Add(outcome);

        await sut.RefreshAsync();

        scans.Should().Be(2);
        outcomes.Should().ContainSingle().Which.Pending.Should().Be(0);
    }

    [Fact]
    public async Task RefreshAsync_WhenBothScansFail_LeavesTheTrayAlone()
    {
        var sut = CreateSut(App("Git", "2.46.0"));
        await sut.CheckAsync(manual: false);
        _notices.Clear();
        _service.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], false, new Dictionary<string, bool>()));
        var outcomes = new List<AppUpdateOutcome>();
        sut.Finished += (_, outcome) => outcomes.Add(outcome);

        await sut.RefreshAsync();

        outcomes.Should().BeEmpty();
        _notices.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshAsync_DuringARunningCheck_WaitsForItInsteadOfScanningAgain()
    {
        var sut = CreateSut(App("Git", "2.46.0"));
        await sut.CheckAsync(manual: false);
        _service.Invocations.Clear();
        var scan = new TaskCompletionSource<InstalledAppsScan>();
        _service.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>())).Returns(scan.Task);

        var scheduled = sut.CheckAsync(manual: false);
        var refresh = sut.RefreshAsync();
        refresh.IsCompleted.Should().BeFalse();
        scan.SetResult(new InstalledAppsScan([App("Git", "2.46.0")], true, new Dictionary<string, bool>()));
        await Task.WhenAll(scheduled, refresh);

        _service.Verify(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()), Times.Once);
    }
}
