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
    private readonly Mock<IInstalledAppsService> _service = new();
    private readonly Mock<ITaskProgressService> _progress = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IDispatcherService> _dispatcher = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly Mock<IUserPreferencesService> _preferences = new();
    private readonly List<AppUpdateNotice> _notices = [];
    private readonly Dictionary<string, object?> _saved = [];

    public AppUpdateMonitorTests()
    {
        _localization.Setup(l => l.GetString(It.IsAny<string>())).Returns<string>(k => k);
        _localization.Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns<string, object[]>((key, args) => $"{key}: {string.Join(", ", args)}");
        _dispatcher.Setup(d => d.RunOnUIThreadWithContextAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(f => f());
        _externalAppsService.Setup(s => s.GetAppsAsync()).ReturnsAsync([]);
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>());
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
        _service.Setup(s => s.GetInstalledAppsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new InstalledAppsScan(apps, true));
        var externalApps = new ExternalAppsViewModel(
            _externalAppsService.Object,
            _service.Object,
            new Mock<IAppInstallationService>().Object,
            new Mock<IStagedAppInstaller>().Object,
            _progress.Object,
            _log.Object,
            new Mock<IDialogService>().Object,
            _localization.Object,
            _dispatcher.Object,
            new Mock<IThemeService>().Object);
        var sut = new AppUpdateMonitor(externalApps, _preferences.Object, _localization.Object, _log.Object);
        sut.NoticeRaised += (_, notice) => _notices.Add(notice);
        return sut;
    }

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
    public void A_check_is_due_once_its_interval_has_passed(AppUpdateCheckFrequency frequency, int? hoursAgo, bool due)
    {
        var now = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
        DateTime? last = hoursAgo is { } h ? now.AddHours(-h) : null;

        AppUpdateMonitor.IsDue(frequency, last, now).Should().Be(due);
    }

    [Fact]
    public async Task A_scheduled_check_reports_updates_once()
    {
        var sut = CreateSut(App("7-Zip"), App("Git", "2.46.0"));

        await sut.CheckAsync(manual: false);
        await sut.CheckAsync(manual: false);

        _notices.Should().ContainSingle().Which.Kind.Should().Be(AppUpdateNoticeKind.Available);
        _notices[0].Message.Should().Be("Notify_UpdatesAvailable_Message: 1");
        _saved.Should().ContainKey(UserPreferenceKeys.LastAppUpdateCheck);
    }

    [Fact]
    public async Task A_manual_check_says_when_everything_is_up_to_date()
    {
        var sut = CreateSut(App("7-Zip"));

        await sut.CheckAsync(manual: false);
        _notices.Should().BeEmpty();

        await sut.CheckAsync(manual: true);
        _notices.Should().ContainSingle().Which.Kind.Should().Be(AppUpdateNoticeKind.UpToDate);
    }

    [Fact]
    public async Task With_automatic_installs_a_scheduled_check_updates_every_app_and_reports_it()
    {
        _preferences.Setup(p => p.GetPreference(UserPreferenceKeys.AppUpdateAction, It.IsAny<AppUpdateAction>()))
            .Returns(AppUpdateAction.Install);
        var sut = CreateSut(App("Git", "2.46.0"), App("VLC", "3.0.21"));
        _service.Setup(s => s.UpdateAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()))
            .Callback(() => _service.Setup(s => s.GetInstalledAppsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new InstalledAppsScan([App("Git"), App("VLC")], true)))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));

        await sut.CheckAsync(manual: false);

        _service.Verify(s => s.UpdateAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _notices.Should().ContainSingle().Which.Message.Should().Be("Notify_UpdatesInstalled_Message: 2");
        _saved.Should().ContainKey(UserPreferenceKeys.LastAppUpdateCheck);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_check_whose_scan_failed_stays_due_and_only_a_manual_one_says_so(bool manual)
    {
        var sut = CreateSut(App("7-Zip"));
        _service.Setup(s => s.GetInstalledAppsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("winget is broken"));

        await sut.CheckAsync(manual);

        _notices.Select(n => n.Kind).Should().Equal(manual ? [AppUpdateNoticeKind.CheckFailed] : []);
        _saved.Should().NotContainKey(UserPreferenceKeys.LastAppUpdateCheck);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_check_whose_scan_could_not_read_the_updates_stays_due_and_only_a_manual_one_says_so(bool manual)
    {
        var sut = CreateSut();
        _service.Setup(s => s.GetInstalledAppsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([App("7-Zip")], false));

        await sut.CheckAsync(manual);

        _notices.Select(n => n.Kind).Should().Equal(manual ? [AppUpdateNoticeKind.CheckFailed] : []);
        _saved.Should().NotContainKey(UserPreferenceKeys.LastAppUpdateCheck);
    }

    [Fact]
    public async Task Automatic_installs_that_cannot_start_behind_another_task_stay_due()
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
    public async Task A_manual_check_during_a_running_check_waits_for_it_and_reports_what_it_found()
    {
        var sut = CreateSut(App("Git", "2.46.0"));
        await sut.CheckAsync(manual: false);
        _notices.Clear();
        _service.Invocations.Clear();
        var scan = new TaskCompletionSource<InstalledAppsScan>();
        _service.Setup(s => s.GetInstalledAppsAsync(It.IsAny<CancellationToken>())).Returns(scan.Task);

        var scheduled = sut.CheckAsync(manual: false);
        var manual = sut.CheckAsync(manual: true);
        manual.IsCompleted.Should().BeFalse();
        scan.SetResult(new InstalledAppsScan([App("Git", "2.46.0")], true));
        await Task.WhenAll(scheduled, manual);

        _service.Verify(s => s.GetInstalledAppsAsync(It.IsAny<CancellationToken>()), Times.Once);
        _notices.Should().ContainSingle().Which.Kind.Should().Be(AppUpdateNoticeKind.Available);
        _notices[0].Message.Should().Be("Notify_UpdatesAvailable_Message: 1");
    }

    [Fact]
    public async Task A_manual_check_during_a_running_check_that_fails_says_it_failed()
    {
        var sut = CreateSut(App("Git", "2.46.0"));
        await sut.CheckAsync(manual: false);
        _notices.Clear();
        var scan = new TaskCompletionSource<InstalledAppsScan>();
        _service.Setup(s => s.GetInstalledAppsAsync(It.IsAny<CancellationToken>())).Returns(scan.Task);

        var scheduled = sut.CheckAsync(manual: false);
        var manual = sut.CheckAsync(manual: true);
        scan.SetException(new InvalidOperationException("winget is broken"));
        await Task.WhenAll(scheduled, manual);

        _notices.Should().ContainSingle().Which.Kind.Should().Be(AppUpdateNoticeKind.CheckFailed);
    }
}
