using FluentAssertions;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Xunit;

namespace Winhance.Core.Tests.Selections;

public class ConfigFileMapperTests
{
    private static readonly string[] HkcuT = [@"HKEY_CURRENT_USER\Software\T"];
    private static readonly string[] HkcuS = [@"HKEY_CURRENT_USER\Software\S"];
    private static readonly string[] Pkg = ["Pkg"];

    private static Setting Toggle(string id) => new()
    {
        Id = id, Display = new() { Name = id, Description = id },
        Targets = new Target[] { new RegTarget("V", HkcuT, "V", RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = "Enabled", Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(1) } },
            new SettingState { Label = "Disabled", Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(0) } },
        },
    };

    private static Setting Selection(string id) => new()
    {
        Id = id, Display = new() { Name = id, Description = id },
        Targets = new Target[] { new RegTarget("M", HkcuS, "Mode", RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = "A", Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Of(0) } },
            new SettingState { Label = "B", Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Of(1) } },
        },
    };

    private static Setting PowerCfgSelection(string id) => new()
    {
        Id = id, Display = new() { Name = id, Description = id },
        Targets = new Target[] { new PowerCfgTarget("P", "sub", "set", PowerModeSupport.Separate) },
        States = new[]
        {
            new SettingState { Label = "Off", Set = new Dictionary<string, StateValue> { ["P"] = StateValue.Of(0) } },
            new SettingState { Label = "On", Set = new Dictionary<string, StateValue> { ["P"] = StateValue.Of(1) } },
        },
    };

    private static Setting Slider(string id, PowerModeSupport mode) => new()
    {
        Id = id, Display = new() { Name = id, Description = id },
        Targets = new Target[] { new PowerCfgTarget("Q", "sub", "set", mode) },
        Numeric = new() { Min = 0, Max = 100, Units = "minutes" },
    };

    private static Setting PowerPlanSetting() => new()
    {
        Id = SettingIds.PowerPlanSelection, Display = new() { Name = "plan", Description = "plan" },
        OptionSource = new StubOptionSource(),
    };

    private sealed class StubOptionSource : IDynamicOptionSource
    {
        public IReadOnlyList<DynamicOption> EnumerateOptions(IDetectionContext context) => Array.Empty<DynamicOption>();
        public string? CurrentSelection(IDetectionContext context) => null;
    }

    public static IEnumerable<object[]> RoundTrips()
    {
        yield return new object[] { Toggle("t"), new ChoiceValue.Toggle(true) };
        yield return new object[] { Toggle("t"), new ChoiceValue.Toggle(false) };
        yield return new object[] { Selection("s"), new ChoiceValue.Option(1) };
        yield return new object[] { Selection("s"), new ChoiceValue.CustomValues(new Dictionary<string, object> { ["Mode"] = 7 }) };
        yield return new object[] { PowerCfgSelection("p"), new ChoiceValue.AcDcOption(1, 0) };
        yield return new object[] { Slider("n", PowerModeSupport.Both), new ChoiceValue.Number(600) };
        yield return new object[] { Slider("n", PowerModeSupport.Separate), new ChoiceValue.AcDcNumber(600, 300) };
        yield return new object[] { PowerPlanSetting(), new ChoiceValue.PowerPlan("381b4222-f694-41f0-9685-ff5bb260df2e", "Balanced") };
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
    public void WriteValue_PowerPlan_WritesGuidAndName_NoIndex()
    {
        var item = new ConfigurationItem { Id = SettingIds.PowerPlanSelection };
        ConfigFileMapper.WriteValue(item, PowerPlanSetting(), new ChoiceValue.PowerPlan("g", "n"));
        item.PowerPlanGuid.Should().Be("g");
        item.PowerPlanName.Should().Be("n");
        item.SelectedIndex.Should().BeNull();
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

    [Fact]
    public void InputTypeFor_MapsEveryControlKind()
    {
        ConfigFileMapper.InputTypeFor(Toggle("t")).Should().Be(InputType.Toggle);
        ConfigFileMapper.InputTypeFor(Selection("s")).Should().Be(InputType.Selection);
        ConfigFileMapper.InputTypeFor(PowerPlanSetting()).Should().Be(InputType.Selection);
        ConfigFileMapper.InputTypeFor(Slider("n", PowerModeSupport.Both)).Should().Be(InputType.NumericRange);
        ConfigFileMapper.InputTypeFor(new Setting { Id = "a", Display = new() { Name = "a", Description = "a" } }).Should().Be(InputType.Action);
    }

    [Fact]
    public void ToFile_GroupsByFeature_AndFromFile_Inverts()
    {
        var t = Toggle("t"); var s = Selection("s");
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            [FeatureIds.ExplorerCustomization] = new[] { t },
            [FeatureIds.Privacy] = new[] { s },
        };
        var set = new SelectionSet(
            new[] { new SettingChoice("t", new ChoiceValue.Toggle(true)), new SettingChoice("s", new ChoiceValue.Option(1)) },
            new[] { new AppChoice("app1", "App", Pkg, null, null, null) },
            new[] { new AppChoice("ext1", "Ext", null, null, null, "Vendor.Ext") },
            AutounattendChoices.None);

        var file = ConfigFileMapper.ToFile(set, byFeature);

        file.Customize.Features[FeatureIds.ExplorerCustomization].Items.Should().ContainSingle(i => i.Id == "t" && i.IsSelected == true && i.Name == "t");
        file.Optimize.Features[FeatureIds.Privacy].Items.Should().ContainSingle(i => i.Id == "s" && i.SelectedIndex == 1);
        file.WindowsApps.Items.Should().ContainSingle(i => i.Id == "app1" && i.AppxPackageName!.Single() == "Pkg" && i.IsSelected == true);
        file.ExternalApps.Items.Should().ContainSingle(i => i.Id == "ext1" && i.WinGetPackageId == "Vendor.Ext");
        file.Optimize.IsIncluded.Should().BeTrue();
        file.Customize.IsIncluded.Should().BeTrue();

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
