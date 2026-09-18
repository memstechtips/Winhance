using System.Text.Json;
using FluentAssertions;
using Microsoft.Win32;
using Winhance.Core.Features.Autounattend;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.TestSupport;
using Xunit;

using Winhance.Core.Features.Common.Localization;
namespace Winhance.Core.Tests.Selections;

public class ConfigFileMapperTests
{
    private static readonly string[] HkcuT = [@"HKEY_CURRENT_USER\Software\T"];
    private static readonly string[] HkcuS = [@"HKEY_CURRENT_USER\Software\S"];
    private static readonly string[] Pkg = ["Pkg"];

    private static Setting Toggle(string id) => new()
    {
        Id = id, Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of(id)},
        Targets = new Target[] { new RegTarget("V", HkcuT, "V", RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = LocKey.Common.Enabled, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(1) } },
            new SettingState { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(0) } },
        },
    };

    private static Setting CheckBox(string id) => new()
    {
        Id = id, Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of(id)},
        Targets = new Target[] { new RegTarget("V", HkcuT, "V", RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = LocKey.Common.Checked, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(1) } },
            new SettingState { Label = LocKey.Common.Unchecked, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(0) } },
        },
    };

    private static Setting Selection(string id) => new()
    {
        Id = id, Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of(id)},
        Targets = new Target[] { new RegTarget("M", HkcuS, "Mode", RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = TestKeys.Of("A"), Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Of(0) } },
            new SettingState { Label = TestKeys.Of("B"), Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Of(1) } },
        },
    };

    private static Setting PowerCfgSelection(string id) => new()
    {
        Id = id, Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of(id)},
        Targets = new Target[] { new PowerCfgTarget("P", "sub", "set", PowerModeSupport.Separate) },
        States = new[]
        {
            new SettingState { Label = TestKeys.Of("Off"), Set = new Dictionary<string, StateValue> { ["P"] = StateValue.Of(0) } },
            new SettingState { Label = TestKeys.Of("On"), Set = new Dictionary<string, StateValue> { ["P"] = StateValue.Of(1) } },
        },
    };

    private static Setting Slider(string id, PowerModeSupport mode) => new()
    {
        Id = id, Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of(id)},
        Targets = new Target[] { new PowerCfgTarget("Q", "sub", "set", mode) },
        Numeric = new() { Min = 0, Max = 100, Units = "minutes" },
    };

    private static Setting TextSetting(string id) => new()
    {
        Id = id, Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of(id)},
        Targets = new Target[] { new AutounattendElement("K", "specialize", "Microsoft-Windows-Shell-Setup", "ComputerName") },
        TextBox = new(new TextRule("^[A-Z]{3}$", UpperCase: true, TestKeys.Of("Three capital letters."))),
    };

    private static Setting ListSetting(string id) => new()
    {
        Id = id, Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of(id)},
        Targets = new Target[] { new AutounattendElement("K", "oobeSystem", "Microsoft-Windows-Shell-Setup", "UserAccounts/LocalAccounts") },
        List = new(
        [
            new Field("name", FieldKind.Text, TestKeys.Of("Name"), Rule: new TextRule("^.+$", UpperCase: false, TestKeys.Of("Anything."))),
            new Field("display-name", FieldKind.Text, TestKeys.Of("Display name")),
            new Field("group", FieldKind.Selection, TestKeys.Of("Group"), Options: [TestKeys.Of("Administrators"), TestKeys.Of("Users")], Default: "0"),
            new Field("password", FieldKind.Password, TestKeys.Of("Password")),
            new Field("obscure", FieldKind.CheckBox, TestKeys.Of("Obscure"), Default: "true"),
            new Field("auto-logon", FieldKind.CheckBox, TestKeys.Of("Sign in once"), OneRowOnly: true),
        ]),
    };

    private static ChoiceValue.ListRow Row(string name, string group = "0", string password = "", string obscure = "true", string autoLogon = "true") =>
        new(new Dictionary<string, string>
        {
            ["name"] = name,
            ["display-name"] = "Marco",
            ["group"] = group,
            ["password"] = password,
            ["obscure"] = obscure,
            ["auto-logon"] = autoLogon,
        });

    private const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";

    private static Setting PowerPlanSetting() => new()
    {
        Id = "power-plan-selection", Display = new() { Name = TestKeys.Of("plan"), Description = TestKeys.Of("plan") },
        Options = new(OptionSource.PowerPlans),
    };

    public static IEnumerable<object[]> RoundTrips()
    {
        yield return new object[] { Toggle("t"), new ChoiceValue.Toggle(true) };
        yield return new object[] { Toggle("t"), new ChoiceValue.Toggle(false) };
        yield return new object[] { CheckBox("c"), new ChoiceValue.CheckBox(true) };
        yield return new object[] { CheckBox("c"), new ChoiceValue.CheckBox(false) };
        yield return new object[] { Selection("s"), new ChoiceValue.Option(1) };
        yield return new object[] { Selection("s"), new ChoiceValue.CustomValues(new Dictionary<string, object> { ["Mode"] = 7 }) };
        yield return new object[] { PowerCfgSelection("p"), new ChoiceValue.AcDcOption(1, 0) };
        yield return new object[] { Slider("n", PowerModeSupport.Both), new ChoiceValue.Number(600) };
        yield return new object[] { Slider("n", PowerModeSupport.Separate), new ChoiceValue.AcDcNumber(600, 300) };
        yield return new object[] { PowerPlanSetting(), new ChoiceValue.Keyed("381b4222-f694-41f0-9685-ff5bb260df2e", "Balanced") };
        yield return new object[] { FakeOptionProvider.SettingFor(), new ChoiceValue.Keyed("beta", "Beta zone") };
        yield return new object[] { TextSetting("x"), new ChoiceValue.Text("ABC") };
        yield return new object[] { TextSetting("x"), new ChoiceValue.Text("") };
        yield return new object[] { ListSetting("a"), new ChoiceValue.List([Row("marco")]) };
        yield return new object[] { ListSetting("a"), new ChoiceValue.List([Row("marco"), Row("guest", group: "1", autoLogon: "false")]) };
        yield return new object[] { ListSetting("a"), new ChoiceValue.List([Row("marco", password: "hunter2")], SavePasswords: true) };
    }

    [Theory]
    [MemberData(nameof(RoundTrips))]
    public void WriteValue_ThenDecodeValue_RoundTripsEveryShape(Setting setting, ChoiceValue value)
    {
        var item = new ConfigurationItem { Id = setting.Id };

        ConfigFileMapper.WriteValue(item, setting, value);
        var decoded = ConfigFileMapper.DecodeValue(setting, item);

        if (value is ChoiceValue.CustomValues cv)
            decoded.Should().BeOfType<ChoiceValue.CustomValues>().Which.Values.Should().BeEquivalentTo(cv.Values);
        else
            decoded.Should().Be(value);
    }

    [Fact]
    public void WriteValue_Keyed_WritesTheKeyAndItsLabelAndNoIndex()
    {
        var item = new ConfigurationItem { Id = "fake-keyed" };

        ConfigFileMapper.WriteValue(item, FakeOptionProvider.SettingFor(), new ChoiceValue.Keyed("beta", "Beta zone"));

        item.SelectedKey.Should().Be("beta");
        item.SelectedKeyLabel.Should().Be("Beta zone");
        item.SelectedIndex.Should().BeNull();
        item.PowerPlanGuid.Should().BeNull();
    }

    [Fact]
    public void DecodeValue_Keyed_KeepsAKeyThisMachineDoesNotOffer()
    {
        // Resolved against this machine's list, the key would silently switch to one the user never chose.
        var item = new ConfigurationItem { Id = "fake-keyed", SelectedKey = "delta", SelectedKeyLabel = "Delta zone" };

        var decoded = ConfigFileMapper.DecodeValue(FakeOptionProvider.SettingFor(), item);

        decoded.Should().Be(new ChoiceValue.Keyed("delta", "Delta zone"));
    }

    [Fact]
    public void DecodeValue_Keyed_FallsBackToTheKeyWhenTheFileCarriesNoLabel()
    {
        var item = new ConfigurationItem { Id = "fake-keyed", SelectedKey = "beta" };

        ConfigFileMapper.DecodeValue(FakeOptionProvider.SettingFor(), item)
            .Should().Be(new ChoiceValue.Keyed("beta", "beta"));
    }

    [Fact]
    public void DecodeValue_Keyed_IgnoresAnIndexALegacyFileMightCarry()
    {
        var item = new ConfigurationItem { Id = "fake-keyed", SelectedIndex = 1 };

        ConfigFileMapper.DecodeValue(FakeOptionProvider.SettingFor(), item).Should().BeNull();
    }

    [Fact]
    public void WriteValue_Toggle_WritesIsSelectedOnly()
    {
        var item = new ConfigurationItem { Id = "t" };
        ConfigFileMapper.WriteValue(item, Toggle("t"), new ChoiceValue.Toggle(true));
        item.IsSelected.Should().BeTrue();
        item.SelectedIndex.Should().BeNull();
        item.PowerSettings.Should().BeNull();
        item.CustomStateValues.Should().BeNull();
    }

    [Fact]
    public void WriteValue_AcDcOption_UsesTheIndexKeys()
    {
        var item = new ConfigurationItem { Id = "p" };
        ConfigFileMapper.WriteValue(item, PowerCfgSelection("p"), new ChoiceValue.AcDcOption(1, 0));
        item.PowerSettings.Should().Equal(new Dictionary<string, object> { ["ACIndex"] = 1, ["DCIndex"] = 0 });
        item.SelectedIndex.Should().BeNull();
    }

    [Fact]
    public void WriteValue_AcDcNumber_UsesTheValueKeys()
    {
        var item = new ConfigurationItem { Id = "n" };
        ConfigFileMapper.WriteValue(item, Slider("n", PowerModeSupport.Separate), new ChoiceValue.AcDcNumber(600, 300));
        item.PowerSettings.Should().Equal(new Dictionary<string, object> { ["ACValue"] = 600, ["DCValue"] = 300 });
    }

    [Fact]
    public void WriteValue_PowerPlan_WritesTheKeyAndLabel()
    {
        var item = new ConfigurationItem { Id = "power-plan-selection" };
        ConfigFileMapper.WriteValue(item, PowerPlanSetting(), new ChoiceValue.Keyed("g", "n"));
        item.SelectedKey.Should().Be("g");
        item.SelectedKeyLabel.Should().Be("n");
        item.SelectedIndex.Should().BeNull();
    }

    [Fact]
    public void WriteValue_PowerPlan_KeepsTheLabelTheUserChoseItBy()
    {
        var item = new ConfigurationItem { Id = "power-plan-selection" };

        ConfigFileMapper.WriteValue(item, PowerPlanSetting(), new ChoiceValue.Keyed(BalancedGuid, "Ausbalanciert"));

        item.SelectedKeyLabel.Should().Be("Ausbalanciert");
    }

    [Fact]
    public void WriteValue_PowerPlan_AlsoWritesTheSpellingTheReleasedAppReads()
    {
        var item = new ConfigurationItem { Id = "power-plan-selection" };

        ConfigFileMapper.WriteValue(item, PowerPlanSetting(), new ChoiceValue.Keyed(BalancedGuid, "Ausbalanciert"));

        item.PowerPlanGuid.Should().Be(BalancedGuid);
        item.PowerPlanName.Should().Be("Balanced");
    }

    [Fact]
    public void DecodeValue_ReadsALegacyPowerPlanEntry()
    {
        const string guid = "381b4222-f694-41f0-9685-ff5bb260df2e";

        var withName = new ConfigurationItem { Id = "power-plan-selection", PowerPlanGuid = guid, PowerPlanName = "Balanced" };
        ConfigFileMapper.DecodeValue(PowerPlanSetting(), withName)
            .Should().Be(new ChoiceValue.Keyed(guid, "Balanced"));

        var guidOnly = new ConfigurationItem { Id = "power-plan-selection", PowerPlanGuid = guid };
        ConfigFileMapper.DecodeValue(PowerPlanSetting(), guidOnly)
            .Should().Be(new ChoiceValue.Keyed(guid, guid));
    }

    [Fact]
    public void DecodeValue_LegacyToggleEraSelection_ReturnsNull()
    {
        var item = new ConfigurationItem { Id = "s", InputType = InputType.Toggle, IsSelected = false };
        ConfigFileMapper.DecodeValue(Selection("s"), item).Should().BeNull();
    }

    [Fact]
    public void DecodeValue_JsonElementNumbers_AreUnwrapped()
    {
        var acdc = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>("{\"ACIndex\":1,\"DCIndex\":0}")!;
        var item = new ConfigurationItem { Id = "p", InputType = InputType.Selection, PowerSettings = acdc };
        ConfigFileMapper.DecodeValue(PowerCfgSelection("p"), item).Should().Be(new ChoiceValue.AcDcOption(1, 0));
    }

    [Fact]
    public void DecodeValue_CustomValues_UnwrapJsonElementsToPlainNumbersAndStrings()
    {
        var custom = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>("{\"Mode\":7,\"Name\":\"x\",\"On\":true}")!;
        var item = new ConfigurationItem { Id = "s", InputType = InputType.Selection, CustomStateValues = custom };
        var decoded = ConfigFileMapper.DecodeValue(Selection("s"), item).Should().BeOfType<ChoiceValue.CustomValues>().Which;
        decoded.Values["Mode"].Should().Be(7);
        decoded.Values["Name"].Should().Be("x");
        decoded.Values["On"].Should().Be(true);
    }

    [Fact]
    public void DecodeValue_Slider_NullDcValue_FallsBackToAc()
    {
        var item = new ConfigurationItem { Id = "n", InputType = InputType.NumericRange, PowerSettings = new Dictionary<string, object> { ["ACValue"] = 600, ["DCValue"] = null! } };
        ConfigFileMapper.DecodeValue(Slider("n", PowerModeSupport.Separate), item).Should().Be(new ChoiceValue.AcDcNumber(600, 600));
    }

    // A .winhance file is a shared preset, so a typed password travels only when the card was told to save it.
    [Fact]
    public void A_list_round_trip_drops_the_password()
    {
        var setting = ListSetting("a");
        var item = new ConfigurationItem { Id = "a" };

        ConfigFileMapper.WriteValue(item, setting, new ChoiceValue.List([Row("marco", password: "hunter2", obscure: "false")]));

        var written = item.Rows.Should().ContainSingle().Which;
        written["name"].Should().Be("marco");
        written["display-name"].Should().Be("Marco");
        written["group"].Should().Be("0");
        written["auto-logon"].Should().Be("true");
        written["obscure"].Should().Be("false");
        written.Should().NotContainKey("password");
        JsonSerializer.Serialize(item, ConfigFileConstants.JsonOptions).Should().NotContain("hunter2");

        ConfigFileMapper.DecodeValue(setting, item)
            .Should().Be(new ChoiceValue.List([Row("marco", obscure: "false")]));
    }

    [Fact]
    public void A_list_round_trip_keeps_an_obscured_password()
    {
        var setting = ListSetting("a");
        var item = new ConfigurationItem { Id = "a" };
        var typed = new ChoiceValue.List([Row("marco", password: "hunter2")], SavePasswords: true);

        ConfigFileMapper.WriteValue(item, setting, typed);

        var json = JsonSerializer.Serialize(item, ConfigFileConstants.JsonOptions);
        json.Should().NotContain("hunter2");
        json.Should().Contain(AccountPasswords.Obscure("hunter2", "Password"));

        ConfigFileMapper.DecodeValue(setting, item).Should().Be(typed);
    }

    [Fact]
    public void An_empty_password_is_never_obscured()
    {
        var item = new ConfigurationItem { Id = "a" };

        ConfigFileMapper.WriteValue(item, ListSetting("a"), new ChoiceValue.List([Row("marco")], SavePasswords: true));

        item.Rows.Should().ContainSingle().Which.Should().NotContainKey("password");

        ConfigFileMapper.DecodeValue(ListSetting("a"), item)
            .Should().Be(new ChoiceValue.List([Row("marco")], SavePasswords: true));
    }

    [Fact]
    public void A_password_this_release_cannot_read_decodes_as_empty()
    {
        var item = new ConfigurationItem
        {
            Id = "a",
            InputType = InputType.List,
            Rows = [new Dictionary<string, string> { ["name"] = "marco", ["password"] = "hunter2" }],
        };

        var decoded = ConfigFileMapper.DecodeValue(ListSetting("a"), item).Should().BeOfType<ChoiceValue.List>().Which;

        decoded.Rows.Single().Values["password"].Should().BeEmpty();
        decoded.SavePasswords.Should().BeFalse();
    }

    [Fact]
    public void A_row_with_no_password_field_at_all_decodes_as_empty()
    {
        var item = new ConfigurationItem
        {
            Id = "a",
            InputType = InputType.List,
            Rows = [new Dictionary<string, string> { ["name"] = "marco" }],
        };

        ConfigFileMapper.DecodeValue(ListSetting("a"), item)
            .Should().BeOfType<ChoiceValue.List>().Which.Rows.Single().Values["password"].Should().BeEmpty();
    }

    [Fact]
    public void ToFile_CarriesAFileBesideTheChoiceThatNamesIt()
    {
        var setting = Toggle("t");
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [FeatureIds.Privacy] = new[] { setting } };
        var set = new SelectionSet(
            [new SettingChoice("t", new ChoiceValue.Toggle(true))],
            [],
            [])
        {
            Files = [new CarriedFile("t", @"C:\Windows\Web\Wallpaper\Winhance\lake.jpg", "QUJD")],
        };

        var file = ConfigFileMapper.ToFile(set, byFeature);

        var item = file.Optimize.Features[FeatureIds.Privacy].Items.Single();
        item.File!.Destination.Should().Be(@"C:\Windows\Web\Wallpaper\Winhance\lake.jpg");
        item.File.Base64.Should().Be("QUJD");

        ConfigFileMapper.FromFile(file, byFeature).Files.Should().Equal(set.Files);
    }

    [Fact]
    public void InputTypeFor_MapsEveryControlKind()
    {
        ConfigFileMapper.InputTypeFor(Toggle("t")).Should().Be(InputType.Toggle);
        ConfigFileMapper.InputTypeFor(CheckBox("c")).Should().Be(InputType.CheckBox);
        ConfigFileMapper.InputTypeFor(Selection("s")).Should().Be(InputType.Selection);
        ConfigFileMapper.InputTypeFor(PowerPlanSetting()).Should().Be(InputType.Selection);
        ConfigFileMapper.InputTypeFor(Slider("n", PowerModeSupport.Both)).Should().Be(InputType.NumericRange);
        ConfigFileMapper.InputTypeFor(TextSetting("x")).Should().Be(InputType.TextBox);
        ConfigFileMapper.InputTypeFor(ListSetting("a")).Should().Be(InputType.List);
        ConfigFileMapper.InputTypeFor(new Setting { Id = "a", Display = new() { Name = TestKeys.Of("a"), Description = TestKeys.Of("a") } }).Should().Be(InputType.Action);
    }

    [Fact]
    public void ToFile_GroupsByFeature_AndFromFile_Inverts()
    {
        var t = Toggle("t"); var s = Selection("s"); var w = Toggle("w");
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            [FeatureIds.ExplorerCustomization] = new[] { t },
            [FeatureIds.Privacy] = new[] { s },
            [FeatureIds.Autounattend] = new[] { w },
        };
        var set = new SelectionSet(
            new[]
            {
                new SettingChoice("t", new ChoiceValue.Toggle(true)),
                new SettingChoice("s", new ChoiceValue.Option(1)),
                new SettingChoice("w", new ChoiceValue.Toggle(true)),
            },
            new[] { new AppChoice("app1", "App", Pkg, null, null, null) },
            new[] { new AppChoice("ext1", "Ext", null, null, null, "Vendor.Ext") });

        var file = ConfigFileMapper.ToFile(set, byFeature);

        file.Customize.Features[FeatureIds.ExplorerCustomization].Items.Should().ContainSingle(i => i.Id == "t" && i.IsSelected == true && i.Name == "t");
        file.Optimize.Features[FeatureIds.Privacy].Items.Should().ContainSingle(i => i.Id == "s" && i.SelectedIndex == 1);
        file.Autounattend.Features[FeatureIds.Autounattend].Items.Should().ContainSingle(i => i.Id == "w");
        file.WindowsApps.Items.Should().ContainSingle(i => i.Id == "app1" && i.AppxPackageName!.Single() == "Pkg" && i.IsSelected == true);
        file.ExternalApps.Items.Should().ContainSingle(i => i.Id == "ext1" && i.WinGetPackageId == "Vendor.Ext");
        file.Optimize.IsIncluded.Should().BeTrue();
        file.Customize.IsIncluded.Should().BeTrue();
        file.Autounattend.IsIncluded.Should().BeTrue();

        var back = ConfigFileMapper.FromFile(file, byFeature);
        back.Settings.Should().BeEquivalentTo(set.Settings);
        back.WindowsApps.Should().BeEquivalentTo(set.WindowsApps);
        back.ExternalApps.Should().BeEquivalentTo(set.ExternalApps);
    }

    [Fact]
    public void ToFile_SettingWithoutAChoice_IsLeftOut_AndEmptyGroupIsNotIncluded()
    {
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [FeatureIds.Privacy] = new[] { Selection("s") } };
        var file = ConfigFileMapper.ToFile(SelectionSet.Empty, byFeature);
        file.Optimize.Features.Should().BeEmpty();
        file.Optimize.IsIncluded.Should().BeFalse();
    }
}
