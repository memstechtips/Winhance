using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Infrastructure.Features.Optimize.Services;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.WimUtil.WimServices;

public class OfflineSettingsApplierTests
{
    private const string BuildKey = @"HKEY_LOCAL_MACHINE\WH_SOFTWARE_t\Microsoft\Windows NT\CurrentVersion";

    private readonly Mock<IOfflineHiveLoader> _hives = new();
    private readonly Mock<IWindowsRegistryService> _live = new();
    private readonly Mock<IRegistryHiveNative> _native = new();
    private readonly Mock<IScheduledTaskStateService> _tasks = new();
    private readonly FakeHiveSession _session = new();
    private readonly List<string> _written = [];
    private readonly List<string> _powerWrites = [];

    public OfflineSettingsApplierTests()
    {
        _hives.Setup(h => h.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(_session);
        _live.Setup(r => r.GetValue(BuildKey, "CurrentBuildNumber")).Returns("26100");
        _live.Setup(r => r.GetValue(BuildKey, "UBR")).Returns(1);
        _live.Setup(r => r.CreateKey(It.IsAny<string>())).Returns(true);
        _live.Setup(r => r.KeyExists(It.IsAny<string>())).Returns(true);
        _live
            .Setup(r => r.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>()))
            .Callback((string path, string _, object _, RegistryValueKind _) => _written.Add(path))
            .Returns(true);
        _native
            .Setup(n => n.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>()))
            .Callback((string path, string name, object value, RegistryValueKind _) => _powerWrites.Add($"{path}|{name}={value}"))
            .Returns(0);
    }

    private OfflineSettingsApplier Applier() => new(
        _hives.Object, _native.Object, _live.Object, Mock.Of<IOptionProviderRegistry>(), _tasks.Object, Mock.Of<IPowerCfgApplier>(),
        Mock.Of<IPowerPlanActivationService>(), Mock.Of<IWindowsThemeService>(), Mock.Of<IDnsSettingsService>(),
        Mock.Of<ISystemParametersService>(), Mock.Of<ILogService>());

    private OfflineStateWriter Writer() => new(
        new OfflineRegistry(_live.Object, _session, _native.Object, Mock.Of<ILogService>()), _tasks.Object, Mock.Of<IPowerCfgApplier>(),
        Mock.Of<IPowerPlanActivationService>(), Mock.Of<IWindowsThemeService>(), Mock.Of<IDnsSettingsService>(),
        Mock.Of<ISystemParametersService>(), Mock.Of<ILogService>());

    private static SelectionSet Chosen(params SettingChoice[] choices) =>
        SelectionSet.Empty with { Settings = choices };

    // A plain registry toggle in the machine hive, so the test follows the catalog rather than a hard-coded id.
    // Every state writes a value: a state that deletes one would need the live mock to accept the delete.
    private static Setting MachineToggle() => SettingCatalog.All.First(s =>
        s.Control == ControlKind.Toggle && !s.IsAnswerFileOnly && s.Detector is null
        && s.States.All(state => state.Effects.Count == 0 && state.Set.Values.All(v => !v.AcceptsAbsent))
        && s.Targets.Count > 0
        && s.Targets.All(t => t is RegTarget
        {
            ReadOnly: false, ByteIndex: null, BitMask: null, ListItem: null, CompositeStringKey: null, StringFlagMask: null,
            PerNetworkInterface: false, PerMonitor: false, ValueName: not null,
        } reg && reg.AppliesTo.Count == 0 && reg.Paths.All(p => p.StartsWith(@"HKEY_LOCAL_MACHINE\SOFTWARE", StringComparison.OrdinalIgnoreCase))));

    [Fact]
    public async Task ApplyAsync_WritesACatalogSettingThroughTheLoadedHive()
    {
        var setting = MachineToggle();

        var outcome = await Applier().ApplyAsync(@"D:\mount", Chosen(new SettingChoice(setting.Id, new ChoiceValue.Toggle(true))), null, CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();
        outcome.Applied.Should().Be(1);
        _written.Should().NotBeEmpty().And.OnlyContain(p => p.StartsWith(@"HKEY_LOCAL_MACHINE\WH_SOFTWARE_t\"));
        _session.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyAsync_NamesASettingTheCatalogDoesNotHave()
    {
        var outcome = await Applier().ApplyAsync(@"D:\mount", Chosen(new SettingChoice("no-such-setting", new ChoiceValue.Toggle(true))), null, CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Failed.Should().Equal("no-such-setting");
    }

    [Fact]
    public async Task ApplyAsync_OneSettingUnknown_StillCountsTheOnesThatApplied()
    {
        var outcome = await Applier().ApplyAsync(
            @"D:\mount",
            Chosen(new SettingChoice("no-such-setting", new ChoiceValue.Toggle(true)), new SettingChoice(MachineToggle().Id, new ChoiceValue.Toggle(true))),
            null,
            CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Failed.Should().Equal("no-such-setting");
        outcome.Applied.Should().Be(1);
    }

    [Fact]
    public async Task ApplyAsync_LeavesAWriteWithNoHiveInTheImageToSetupWithoutFailing()
    {
        var outcome = await Applier().ApplyAsync(@"D:\mount", Chosen(new SettingChoice("devices-numlock-at-startup", new ChoiceValue.Toggle(true))), null, CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();
        _written.Should().OnlyContain(p => p.StartsWith(@"HKEY_LOCAL_MACHINE\WH_NTUSER_t\"));
    }

    [Fact]
    public async Task ApplyAsync_ARefusedWriteDoesNotHideAnotherWriteOfTheSameSettingThatFailed()
    {
        _live
            .Setup(r => r.SetValue(It.Is<string>(p => p.StartsWith(@"HKEY_LOCAL_MACHINE\WH_NTUSER_t\")), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>()))
            .Returns(false);

        var outcome = await Applier().ApplyAsync(@"D:\mount", Chosen(new SettingChoice("devices-numlock-at-startup", new ChoiceValue.Toggle(true))), null, CancellationToken.None);

        outcome.Succeeded.Should().BeFalse();
        outcome.Failed.Should().Equal("devices-numlock-at-startup");
    }

    [Fact]
    public async Task ApplyAsync_UnloadsTheHivesWhenTheBuildCannotBeRead()
    {
        _live.Setup(r => r.GetValue(BuildKey, "CurrentBuildNumber")).Returns((object?)null);

        var act = () => Applier().ApplyAsync(@"D:\mount", SelectionSet.Empty, null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _session.Disposed.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyAsync_UnloadsTheHivesWhenCancelled()
    {
        var setting = MachineToggle();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => Applier().ApplyAsync(@"D:\mount", Chosen(new SettingChoice(setting.Id, new ChoiceValue.Toggle(true))), null, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _session.Disposed.Should().BeTrue();
    }

    [Fact]
    public void Execute_WritesTheRegistryThroughTheMappedPathAndIgnoresATaskOp()
    {
        var target = new RegTarget("Foo", [@"HKEY_CURRENT_USER\Software\Foo"], "Bar", RegistryValueKind.DWord);
        var plan = new ApplyPlan(
            [new RegistryWriteOp(target, target.Paths[0], 1), new TaskSetOp(new TaskTarget("Task", @"\Microsoft\Windows\Foo"), false)],
            []);

        var result = ApplyExecutor.Execute(plan, Writer());

        result.AllSucceeded.Should().BeTrue();
        _live.Verify(r => r.SetValue(@"HKEY_LOCAL_MACHINE\WH_NTUSER_t\Software\Foo", "Bar", 1, RegistryValueKind.DWord), Times.Once);
        _tasks.VerifyNoOtherCalls();
    }

    [Fact]
    public void Execute_ReportsAWriteToAnUnmappedRootAsFailedAndWritesNothing()
    {
        var target = new RegTarget("Foo", [@"HKEY_USERS\.DEFAULT\Foo"], "Bar", RegistryValueKind.DWord);

        var result = ApplyExecutor.Execute(new ApplyPlan([new RegistryWriteOp(target, target.Paths[0], 1)], []), Writer());

        result.Failed.Should().Be(1);
        _written.Should().BeEmpty();
    }

    [Fact]
    public void Execute_WritesAServiceStartTypeUnderControlSet001()
    {
        const string service = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Foo";
        var target = new RegTarget("Start", [service], "Start", RegistryValueKind.DWord);

        var result = ApplyExecutor.Execute(new ApplyPlan([new RegistryWriteOp(target, service, 4)], []), Writer());

        result.AllSucceeded.Should().BeTrue();
        _written.Should().Equal(@"HKEY_LOCAL_MACHINE\WH_SYSTEM_t\ControlSet001\Services\Foo");
    }

    [Fact]
    public void Execute_LeavesAServiceTheImageDoesNotHave()
    {
        const string service = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Foo";
        _live.Setup(r => r.KeyExists(It.IsAny<string>())).Returns(false);
        var target = new RegTarget("Start", [service], "Start", RegistryValueKind.DWord);

        var result = ApplyExecutor.Execute(new ApplyPlan([new RegistryWriteOp(target, service, 4)], []), Writer());

        result.AllSucceeded.Should().BeTrue();
        _written.Should().BeEmpty();
    }

    private const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private const string HighPerformance = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string Schemes = @"WH_SYSTEM_t\ControlSet001\Control\Power\User\PowerSchemes";
    private const string DisplaySubgroup = "7516b95f-f776-4464-8c53-06167f40cc99";
    private const string DisplayTimeout = "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e";

    private static readonly PowerCfgTarget DisplayTarget = new("Power", DisplaySubgroup, DisplayTimeout, PowerModeSupport.Separate);

    [Fact]
    public void Execute_WritesAPowerValueUnderTheImagesActiveScheme()
    {
        _live.Setup(r => r.GetValue(It.Is<string>(p => p.EndsWith(@"\PowerSchemes")), "ActivePowerScheme")).Returns(HighPerformance);

        var result = ApplyExecutor.Execute(
            new ApplyPlan([new PowerCfgSetOp(DisplayTarget, PowerContext.AC, 600), new PowerCfgSetOp(DisplayTarget, PowerContext.DC, 300)], []),
            Writer());

        result.AllSucceeded.Should().BeTrue();
        _powerWrites.Should().Equal(
            $@"{Schemes}\{HighPerformance}\{DisplaySubgroup}\{DisplayTimeout}|ACSettingIndex=600",
            $@"{Schemes}\{HighPerformance}\{DisplaySubgroup}\{DisplayTimeout}|DCSettingIndex=300");
        _native.Verify(n => n.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), RegistryValueKind.DWord), Times.Exactly(2));
    }

    [Fact]
    public void Execute_FallsBackToBalancedWhenTheImageNamesNoActiveScheme()
    {
        ApplyExecutor.Execute(new ApplyPlan([new PowerCfgSetOp(DisplayTarget, PowerContext.AC, 600)], []), Writer());

        _powerWrites.Should().ContainSingle().Which.Should().Contain($@"\{Balanced}\");
    }

    [Fact]
    public void ActivatePowerPlan_WritesAStockPlanAsTheActiveSchemeString()
    {
        Writer().ActivatePowerPlan(HighPerformance.ToUpperInvariant()).Should().BeTrue();

        _native.Verify(n => n.SetValue($@"{Schemes}", "ActivePowerScheme", HighPerformance, RegistryValueKind.String), Times.Once);
    }

    [Fact]
    public void ActivatePowerPlan_LeavesACustomPlanForSetupToCreate()
    {
        Writer().ActivatePowerPlan("57696e68-616e-6365-506f-776572000000").Should().BeTrue();

        _native.VerifyNoOtherCalls();
    }

    [Fact]
    public void Execute_WritesNoPowerValueWhenACustomPlanIsPicked()
    {
        var writer = Writer();
        writer.PickedPowerPlan = Guid.Parse("57696e68-616e-6365-506f-776572000000");

        var result = ApplyExecutor.Execute(new ApplyPlan([new PowerCfgSetOp(DisplayTarget, PowerContext.AC, 600)], []), writer);

        result.AllSucceeded.Should().BeTrue();
        _powerWrites.Should().BeEmpty();
    }

    [Fact]
    public void Execute_WritesIntoTheStockPlanThatWasPicked()
    {
        var writer = Writer();
        writer.PickedPowerPlan = Guid.Parse(HighPerformance);

        ApplyExecutor.Execute(new ApplyPlan([new PowerCfgSetOp(DisplayTarget, PowerContext.AC, 600)], []), writer);

        _powerWrites.Should().ContainSingle().Which.Should().Contain($@"\{HighPerformance}\");
    }

    [Fact]
    public async Task ApplyAsync_LeavesAPowerValueToSetupWhenWindowsRefusesTheWrite()
    {
        _native
            .Setup(n => n.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>()))
            .Returns(5);

        var outcome = await Applier().ApplyAsync(
            @"D:\mount",
            Chosen(new SettingChoice("power-display-timeout", new ChoiceValue.AcDcOption(0, 0))),
            null,
            CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();
        outcome.Failed.Should().BeEmpty();
        _native.Verify(n => n.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ApplyAsync_WritesAPickedStockPlanBeforeTheValuesThatFollowIt()
    {
        var outcome = await Applier().ApplyAsync(
            @"D:\mount",
            Chosen(
                new SettingChoice("power-display-timeout", new ChoiceValue.AcDcOption(0, 0)),
                new SettingChoice("power-plan-selection", new ChoiceValue.Keyed(HighPerformance, "High performance"))),
            null,
            CancellationToken.None);

        outcome.Succeeded.Should().BeTrue();
        _powerWrites.Should().Contain($"{Schemes}|ActivePowerScheme={HighPerformance}");
        _powerWrites.Where(w => w.Contains(DisplayTimeout)).Should().OnlyContain(w => w.Contains(HighPerformance));
    }
}
