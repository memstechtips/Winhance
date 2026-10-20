using Winhance.Core.Features.Common.Localization;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class SettingSnapshotSourceTests
{
    private readonly Mock<ICatalogSettingsRegistry> _registry = new();
    private readonly Mock<ICatalogSettingStateProvider> _states = new();
    private readonly Mock<ILogService> _log = new();

    private SettingSnapshotSource Sut() => new(_registry.Object, _states.Object, _log.Object);

    private void Arrange(Setting setting, SettingStateResult state)
    {
        _registry.Setup(r => r.InitializeAsync()).Returns(Task.CompletedTask);
        _registry.Setup(r => r.GetAll(It.IsAny<CatalogScope>()))
            .Returns(new Dictionary<string, IReadOnlyList<Setting>> { [FeatureIds.ExplorerCustomization] = new[] { setting } });
        _states.Setup(s => s.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult> { [setting.Id] = state });
    }

    [Fact]
    public async Task Toggle_MapsIsEnabled()
    {
        Arrange(ParityFixtures.Toggle("t"), new SettingStateResult { Success = true, IsEnabled = true });
        var choices = await Sut().CaptureAsync(CatalogScope.CurrentMachine);
        choices.Should().ContainSingle().Which.Should().Be(new SettingChoice("t", new ChoiceValue.Toggle(true)));
    }

    [Fact]
    public async Task CheckBox_MapsIsEnabledAsACheckBox()
    {
        Arrange(ParityFixtures.CheckBox("c"), new SettingStateResult { Success = true, IsEnabled = true });
        var choices = await Sut().CaptureAsync(CatalogScope.CurrentMachine);
        choices.Should().ContainSingle().Which.Should().Be(new SettingChoice("c", new ChoiceValue.CheckBox(true)));
    }

    [Fact]
    public async Task Text_RecordsWhatWasTyped()
    {
        Arrange(ParityFixtures.TextSetting("x"), new SettingStateResult { Success = true, CurrentValue = "PC-01" });
        var choices = await Sut().CaptureAsync(CatalogScope.CurrentMachine);
        choices.Should().ContainSingle().Which.Should().Be(new SettingChoice("x", new ChoiceValue.Text("PC-01")));
    }

    [Fact]
    public async Task Text_EmptyBox_IsStillAnAnswer()
    {
        Arrange(ParityFixtures.TextSetting("x"), new SettingStateResult { Success = true, CurrentValue = "" });
        var choices = await Sut().CaptureAsync(CatalogScope.CurrentMachine);
        choices.Should().ContainSingle().Which.Should().Be(new SettingChoice("x", new ChoiceValue.Text("")));
    }

    [Fact]
    public async Task Text_WithNothingRead_IsOmitted()
    {
        Arrange(ParityFixtures.TextSetting("x"), new SettingStateResult { Success = true });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Should().BeEmpty();
    }

    private static readonly ChoiceValue.List OneAccount = new(
    [
        new ChoiceValue.ListRow(new Dictionary<string, string>
        {
            ["name"] = "marco",
            ["display-name"] = "Marco",
            ["group"] = "0",
            ["password"] = "hunter2",
            ["obscure"] = "true",
            ["auto-logon"] = "true",
        }),
    ]);

    [Fact]
    public async Task List_RecordsTheRowsThatWereBuilt()
    {
        Arrange(ParityFixtures.ListSetting("a"), new SettingStateResult { Success = true, CurrentValue = OneAccount });
        var choices = await Sut().CaptureAsync(CatalogScope.CurrentMachine);
        choices.Should().ContainSingle().Which.Should().Be(new SettingChoice("a", OneAccount));
    }

    [Fact]
    public async Task List_WithNothingRead_IsAnEmptyList()
    {
        Arrange(ParityFixtures.ListSetting("a"), new SettingStateResult { Success = true });
        var choices = await Sut().CaptureAsync(CatalogScope.CurrentMachine);
        choices.Should().ContainSingle().Which.Should()
            .Be(new SettingChoice("a", new ChoiceValue.List([])));
    }

    // A reading the app admits it could not make is not intent. Recording it would write an explicit "off"
    // into the file and disable the setting on the next machine.
    [Fact]
    public async Task Toggle_DetectionFailed_IsOmitted()
    {
        Arrange(ParityFixtures.Toggle("t"), new SettingStateResult { Success = false, IsEnabled = false });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Should().BeEmpty();
    }

    [Fact]
    public async Task Toggle_Undetermined_IsOmitted()
    {
        Arrange(ParityFixtures.Toggle("t"), new SettingStateResult { Success = true, IsEnabled = false, Outcome = SettingDetectionOutcome.Undetermined });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Should().BeEmpty();
    }

    [Fact]
    public async Task Toggle_Malformed_IsStillRecorded()
    {
        Arrange(ParityFixtures.Toggle("t"), new SettingStateResult { Success = true, IsEnabled = true, Outcome = SettingDetectionOutcome.Malformed });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value.Should().Be(new ChoiceValue.Toggle(true));
    }

    [Fact]
    public async Task Action_MapsIsEnabledAsAToggle()
    {
        var action = new Setting { Id = "a", Display = new() { Name = TestKeys.Of("a"), Description = TestKeys.Of("a") } };
        Arrange(action, new SettingStateResult { Success = true, IsEnabled = false });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value.Should().Be(new ChoiceValue.Toggle(false));
    }

    [Fact]
    public async Task Selection_AtOption_MapsIndex()
    {
        Arrange(ParityFixtures.Selection("s"), new SettingStateResult { Success = true, CurrentValue = 1 });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value.Should().Be(new ChoiceValue.Option(1));
    }

    [Fact]
    public async Task Selection_NonIntCurrentValue_MapsIndexZero()
    {
        Arrange(ParityFixtures.Selection("s"), new SettingStateResult { Success = true, CurrentValue = null });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value.Should().Be(new ChoiceValue.Option(0));
    }

    [Fact]
    public async Task Selection_AtCustom_MapsReadingsKeyedByValueName()
    {
        Arrange(ParityFixtures.Selection("s"), new SettingStateResult
        {
            Success = true, CurrentValue = ComboBoxConstants.CustomStateIndex,
            Readings = new Dictionary<string, object?> { ["Mode"] = 7, ["Unrelated"] = 1 },
        });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value
            .Should().BeOfType<ChoiceValue.CustomValues>().Which.Values.Should().Equal(new Dictionary<string, object> { ["Mode"] = 7 });
    }

    [Fact]
    public async Task Selection_AtCustom_WithNoReadings_IsOmitted()
    {
        Arrange(ParityFixtures.Selection("s"), new SettingStateResult { Success = true, CurrentValue = ComboBoxConstants.CustomStateIndex });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Should().BeEmpty();
    }

    // gaming-dns-server has no RegTarget, so the ValueName pass finds nothing. Without the reconstructor
    // fallback the machine's own DNS servers never reach the saved file.
    [Fact]
    public async Task Selection_AtCustom_ScriptOnlySetting_CarriesTheMachinesOwnValues()
    {
        Arrange(ScriptOnlyDnsSelection("dns"), new SettingStateResult
        {
            Success = true, CurrentValue = ComboBoxConstants.CustomStateIndex,
            DnsServers = TwoDnsServers,
        });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value
            .Should().BeOfType<ChoiceValue.CustomValues>().Which.Values
            .Should().Equal(new Dictionary<string, object> { ["primary"] = "10.0.0.5", ["secondary"] = "10.0.0.6" });
    }

    // DetectedIndex says only "this matched no option". Carrying it would give the system-tray setting a
    // Custom choice whose script promotes every icon, which is a different state from the one detected.
    [Fact]
    public async Task Selection_AtCustom_WithOnlyADetectionArtifact_IsOmitted()
    {
        Arrange(ScriptOnlyDnsSelection("dns"), new SettingStateResult
        {
            Success = true, CurrentValue = ComboBoxConstants.CustomStateIndex,
        });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Should().BeEmpty();
    }

    private static readonly string[] TwoDnsServers = ["10.0.0.5", "10.0.0.6"];

    private static Setting ScriptOnlyDnsSelection(string id) => new()
    {
        Id = id, Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of(id)},
        States = new[]
        {
            new SettingState { Label = TestKeys.Of("Automatic") },
            new SettingState { Label = TestKeys.Of("Cloudflare") },
        },
        CustomStateScripts = new[] { new ScriptEffect("Set-DnsClientServerAddress -ServerAddresses @('{{primary}}','{{secondary}}')", RunContext.System) },
        Detector = new DnsServerDetector(TestKeys.Of("Automatic"), new Dictionary<string, LocKey> { ["1.1.1.1"] = TestKeys.Of("Cloudflare") }),
    };

    [Fact]
    public async Task PowerCfgSelection_Separate_MapsAcDcIndicesFromPayloads()
    {
        Arrange(ParityFixtures.PowerCfgSelection("p"), new SettingStateResult { Success = true, CurrentValue = 1, AcValue = 900, DcValue = 300 });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value.Should().Be(new ChoiceValue.AcDcOption(1, 0));
    }

    [Fact]
    public async Task PowerCfgSelection_Separate_NoDc_UsesTheAcOptionForBoth()
    {
        // Recording option 0 for the unreadable half would author whatever option 0 happens to be - "Never"
        // on a TimeIntervals-backed setting, which is not what the machine was doing.
        Arrange(ParityFixtures.PowerCfgSelection("p"), new SettingStateResult { Success = true, CurrentValue = 1, AcValue = 900 });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value.Should().Be(new ChoiceValue.AcDcOption(1, 1));
    }

    [Fact]
    public async Task PowerCfgSelection_Separate_WithoutAcDcReadings_MapsIndex()
    {
        Arrange(ParityFixtures.PowerCfgSelection("p"), new SettingStateResult { Success = true, CurrentValue = 1 });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value.Should().Be(new ChoiceValue.Option(1));
    }

    [Fact]
    public async Task Slider_Separate_MapsRawSystemValues()
    {
        Arrange(ParityFixtures.Slider("n", PowerModeSupport.Separate), new SettingStateResult { Success = true, CurrentValue = 600, AcValue = 600, DcValue = 300 });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value.Should().Be(new ChoiceValue.AcDcNumber(600, 300));
    }

    [Fact]
    public async Task Slider_Separate_NoDc_UsesAcForBoth()
    {
        Arrange(ParityFixtures.Slider("n", PowerModeSupport.Separate), new SettingStateResult { Success = true, CurrentValue = 600, AcValue = 600 });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value.Should().Be(new ChoiceValue.AcDcNumber(600, 600));
    }

    [Fact]
    public async Task Slider_Both_MapsCurrentValue()
    {
        Arrange(ParityFixtures.Slider("n", PowerModeSupport.Both), new SettingStateResult { Success = true, CurrentValue = 600 });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value.Should().Be(new ChoiceValue.Number(600));
    }

    [Fact]
    public async Task Slider_NoCurrentValue_IsOmitted()
    {
        Arrange(ParityFixtures.Slider("n", PowerModeSupport.Separate), new SettingStateResult { Success = true });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Should().BeEmpty();
    }

    [Fact]
    public async Task PowerPlan_MapsGuidAndName()
    {
        Arrange(ParityFixtures.PowerPlanSetting(), new SettingStateResult
        {
            Success = true,
            CurrentValue = 0,
            DynamicSelection = "guid-1",
            DynamicOptions = [new DynamicOption("Balanced", "guid-1")],
        });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value.Should().Be(new ChoiceValue.Keyed("guid-1", "Balanced"));
    }

    [Fact]
    public async Task PowerPlan_WithoutActivePlan_IsOmitted()
    {
        Arrange(ParityFixtures.PowerPlanSetting(), new SettingStateResult { Success = true, CurrentValue = 0 });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Should().BeEmpty();
    }

    // A third-party plan - an OEM's, or one the user built - is where "carry the machine's actual values" bites:
    // its GUID is none of Winhance's own, and the autounattend recreates the plan from the GUID and the name.
    [Fact]
    public async Task PowerPlan_ThirdPartyGuidAndName_SurviveTheFileRoundTrip()
    {
        const string guid = "9c5e7fda-e8bf-4d6b-8d2f-0a1b2c3d4e5f";
        var setting = ParityFixtures.PowerPlanSetting();
        Arrange(setting, new SettingStateResult
        {
            Success = true,
            CurrentValue = 0,
            DynamicSelection = guid,
            DynamicOptions = [new DynamicOption("MSI Gaming Mode", guid)],
        });
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [FeatureIds.ExplorerCustomization] = new[] { setting } };

        var captured = await Sut().CaptureAsync(CatalogScope.CurrentMachine);
        var set = new SelectionSet(captured, Array.Empty<AppChoice>(), Array.Empty<AppChoice>());
        var restored = ConfigFileMapper.FromFile(ConfigFileMapper.ToFile(set, byFeature), byFeature);

        restored.Settings.Should().ContainSingle().Which.Value
            .Should().Be(new ChoiceValue.Keyed(guid, "MSI Gaming Mode"));
    }

    [Fact]
    public async Task SettingWithNoState_IsOmitted()
    {
        _registry.Setup(r => r.InitializeAsync()).Returns(Task.CompletedTask);
        _registry.Setup(r => r.GetAll(It.IsAny<CatalogScope>()))
            .Returns(new Dictionary<string, IReadOnlyList<Setting>> { [FeatureIds.ExplorerCustomization] = new[] { ParityFixtures.Toggle("t") } });
        _states.Setup(s => s.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>())).ReturnsAsync(new Dictionary<string, SettingStateResult>());
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Should().BeEmpty();
    }

    [Fact]
    public async Task FeatureOutsideTheThreeGroups_IsSkipped()
    {
        _registry.Setup(r => r.InitializeAsync()).Returns(Task.CompletedTask);
        _registry.Setup(r => r.GetAll(It.IsAny<CatalogScope>()))
            .Returns(new Dictionary<string, IReadOnlyList<Setting>> { ["SomethingElse"] = new[] { ParityFixtures.Toggle("t") } });
        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Should().BeEmpty();
        _states.Verify(s => s.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()), Times.Never);
    }

    [Fact]
    public async Task AutounattendFeature_IsCaptured()
    {
        var setting = ParityFixtures.Toggle("w");
        _registry.Setup(r => r.InitializeAsync()).Returns(Task.CompletedTask);
        _registry.Setup(r => r.GetAll(It.IsAny<CatalogScope>()))
            .Returns(new Dictionary<string, IReadOnlyList<Setting>> { [FeatureIds.Autounattend] = new[] { setting } });
        _states.Setup(s => s.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["w"] = new SettingStateResult { Success = true, IsEnabled = true },
            });

        var choices = await Sut().CaptureAsync(CatalogScope.CurrentMachine);

        choices.Should().ContainSingle().Which.Should().Be(new SettingChoice("w", new ChoiceValue.Toggle(true)));
    }

    [Fact]
    public async Task Scope_IncludeOtherOsVersions_IsForwardedToTheRegistry()
    {
        Arrange(ParityFixtures.Toggle("t"), new SettingStateResult { Success = true, IsEnabled = false });
        await Sut().CaptureAsync(new CatalogScope(IncludeOtherOsVersions: true, IncludeOtherHardware: false));
        _registry.Verify(r => r.GetAll(new CatalogScope(true, false)), Times.Once);
    }

    [Fact]
    public async Task A_keyed_selection_is_snapshotted_as_its_key_and_the_label_the_machine_showed()
    {
        var setting = FakeOptionProvider.SettingFor();
        Arrange(setting, FakeOptionProvider.StateOn("beta"));

        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Single().Value
            .Should().Be(new ChoiceValue.Keyed("beta", "Beta zone"));
    }

    [Fact]
    public async Task A_keyed_selection_the_machine_could_not_read_is_not_snapshotted()
    {
        var setting = FakeOptionProvider.SettingFor();
        Arrange(setting, FakeOptionProvider.StateOn(null));

        (await Sut().CaptureAsync(CatalogScope.CurrentMachine)).Should().BeEmpty();
    }
}
