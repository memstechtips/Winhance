using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools;

public class AutounattendWriterParityTests
{
    private readonly Mock<IPowerSettingsQueryService> _power = new();
    private readonly Mock<IHardwareDetectionService> _hardware = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IPowerShellRunner> _ps = new();
    private readonly Mock<IWindowsVersionService> _version = new();

    public AutounattendWriterParityTests()
    {
        _version.Setup(v => v.GetWindowsBuildNumber()).Returns(ParityCatalog.Build.Build);
        _version.Setup(v => v.GetWindowsBuildRevision()).Returns(ParityCatalog.Build.Revision);
        _hardware.Setup(h => h.HasBattery()).Returns(true);
        _power.Setup(p => p.GetActivePowerPlanAsync()).ReturnsAsync(new PowerPlan { Guid = "381b4222-f694-41f0-9685-ff5bb260df2e", Name = "Balanced" });
        _power.Setup(p => p.GetAllPowerSettingsACDCAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>());
        _ps.Setup(p => p.ValidateScriptSyntaxAsync(It.IsAny<string>(), default)).Returns(Task.CompletedTask);
    }

    private AutounattendScriptBuilder OldBuilder() =>
        new(_power.Object, _hardware.Object, _log.Object, _ps.Object, _version.Object);

    // The two powercfg settings only matter to the power section: the old path read their values from the live
    // machine (mocked empty here), the new one from the choices, so the feature-section comparison leaves them out
    // and the power section has its own facts.
    private static readonly string[] PowerCfgIds = ["parity-powercfg-selection", "parity-slider"];

    // Every parity setting at its "on"/first-option value, the way a .winhance export writes it.
    internal static WinhanceConfigFile OldConfig(bool withPowerCfg = true)
    {
        var items = new List<ConfigurationItem>
        {
            new() { Id = "parity-toggle-hklm", InputType = InputType.Toggle, IsSelected = true },
            new() { Id = "parity-toggle-hkcu-delete", InputType = InputType.Toggle, IsSelected = false },
            new() { Id = "parity-key-exists", InputType = InputType.Toggle, IsSelected = true },
            new() { Id = "parity-bit", InputType = InputType.Toggle, IsSelected = true },
            new() { Id = "parity-byte", InputType = InputType.Toggle, IsSelected = true },
            new() { Id = "parity-per-subkey", InputType = InputType.Toggle, IsSelected = true },
            new() { Id = "parity-scripts", InputType = InputType.Toggle, IsSelected = true },
            new() { Id = "parity-regcontent", InputType = InputType.Toggle, IsSelected = true },
            new() { Id = "parity-task", InputType = InputType.Toggle, IsSelected = false },
            new() { Id = "parity-selection", InputType = InputType.Selection, SelectedIndex = 2 },
            new() { Id = "parity-powercfg-selection", InputType = InputType.Selection, PowerSettings = new Dictionary<string, object> { ["ACIndex"] = 1, ["DCIndex"] = 0 } },
            new() { Id = "parity-slider", InputType = InputType.NumericRange, PowerSettings = new Dictionary<string, object> { ["ACValue"] = 600, ["DCValue"] = 300 } },
            new() { Id = "parity-composite", InputType = InputType.Toggle, IsSelected = true },
            new() { Id = "parity-string-flag", InputType = InputType.Toggle, IsSelected = true },
            new() { Id = "parity-lock", InputType = InputType.Toggle, IsSelected = true },
            new() { Id = "parity-resetset", InputType = InputType.Toggle, IsSelected = true },
            new() { Id = "parity-action", InputType = InputType.Action, IsSelected = true },
        };
        if (!withPowerCfg) items.RemoveAll(i => PowerCfgIds.Contains(i.Id));
        var section = new ConfigSection { IsIncluded = true, Items = items };
        return new WinhanceConfigFile
        {
            Customize = new FeatureGroupSection { IsIncluded = true, Features = new Dictionary<string, ConfigSection> { [ParityCatalog.FeatureId] = section } },
        };
    }

    [Fact]
    public async Task OldBuilder_RendersTheParityCatalog()
    {
        var script = await OldBuilder().BuildWinhancementsScriptAsync(OldConfig(), ParityCatalog.ByFeature);

        script.Should().Contain("Set-RegistryValue -Path 'HKLM:\\SOFTWARE\\Parity' -Name 'V' -Type 'DWord' -Value 1");
        script.Should().Contain("Remove-RegistryValue -Path 'HKCU:\\Software\\Parity' -Name 'V'");
        script.Should().Contain("Set-BinaryBit -Path 'HKCU:\\Control Panel\\Desktop' -Name 'UserPreferencesMask' -ByteIndex 4 -BitMask 0x20 -SetBit $True");
        script.Should().Contain("Get-ChildItem -Path 'HKLM:\\SYSTEM\\Parity\\Interfaces' -ErrorAction SilentlyContinue | ForEach-Object {");
        script.Should().Contain("Write-Host 'system side'");
        script.Should().Contain("Write-Host 'user side'");
        script.Should().Contain("reg import");
        script.Should().Contain("schtasks /Change");
    }

    private static SelectionSet NewSet(bool withPowerCfg = true)
    {
        var choices = new List<SettingChoice>
        {
            new("parity-toggle-hklm", new ChoiceValue.Toggle(true)),
            new("parity-toggle-hkcu-delete", new ChoiceValue.Toggle(false)),
            new("parity-key-exists", new ChoiceValue.Toggle(true)),
            new("parity-bit", new ChoiceValue.Toggle(true)),
            new("parity-byte", new ChoiceValue.Toggle(true)),
            new("parity-per-subkey", new ChoiceValue.Toggle(true)),
            new("parity-scripts", new ChoiceValue.Toggle(true)),
            new("parity-regcontent", new ChoiceValue.Toggle(true)),
            new("parity-task", new ChoiceValue.Toggle(false)),
            new("parity-selection", new ChoiceValue.Option(2)),
            new("parity-powercfg-selection", new ChoiceValue.AcDcOption(1, 0)),
            new("parity-slider", new ChoiceValue.AcDcNumber(600, 300)),
            new("parity-composite", new ChoiceValue.Toggle(true)),
            new("parity-string-flag", new ChoiceValue.Toggle(true)),
            new("parity-lock", new ChoiceValue.Toggle(true)),
            new("parity-resetset", new ChoiceValue.Toggle(true)),
            new("parity-action", new ChoiceValue.Toggle(true)),
        };
        if (!withPowerCfg) choices.RemoveAll(c => PowerCfgIds.Contains(c.SettingId));
        return new SelectionSet(choices, Array.Empty<AppChoice>(), Array.Empty<AppChoice>(), AutounattendChoices.None);
    }

    // Ids whose rendering is INTENTIONALLY different (spec 3.5.4 plus the two found while building the parity):
    // composite / string-flag / lock get the helpers the old emitter lacked; a WindowsDefault-roled state's ResetSet is
    // honoured; a setting that applies via a .reg import no longer also writes its detect-only targets; a key-existence
    // state writes New-RegistryKey where the old emitter wrote Remove-RegistryKey. Every other line must be identical.
    private static readonly string[] IntentionalDifferences =
        ["parity-composite", "parity-string-flag", "parity-lock", "parity-resetset", "parity-regcontent", "parity-key-exists"];

    // Every emitted line for a setting carries its description (-Description '...', Write-Log "..."), so stripping
    // by description removes exactly that setting's lines from both sides.
    private static string StripLinesMentioning(string script, IEnumerable<string> descriptions)
    {
        var needles = descriptions.ToList();
        return string.Join("\n", script.Split('\n').Where(l => !needles.Any(d => l.Contains(d, StringComparison.Ordinal))));
    }

    [Fact]
    public async Task NewBuilder_MatchesOldBuilder_ExceptTheIntentionalDifferences()
    {
        var oldScript = await OldBuilder().BuildWinhancementsScriptAsync(OldConfig(withPowerCfg: false), ParityCatalog.ByFeature);
        var newScript = await OldBuilder().BuildAsync(NewSet(withPowerCfg: false), ParityCatalog.ByFeature);

        var names = IntentionalDifferences.Select(id => ParityCatalog.Settings.Single(s => s.Id == id).Display.Description).ToList();
        StripLinesMentioning(newScript, names).Should().Be(StripLinesMentioning(oldScript, names));
    }

    [Fact]
    public async Task NewBuilder_PowerRowsComeFromChoices_NotTheMachine()
    {
        var script = await OldBuilder().BuildAsync(NewSet(), ParityCatalog.ByFeature);

        script.Should().Contain("G=\"0853a681-27c8-4100-a2fd-82013e970683\"; AC=1; DC=0;");
        script.Should().Contain("G=\"3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e\"; AC=600; DC=300;");
        _power.Verify(p => p.GetAllPowerSettingsACDCAsync(It.IsAny<string>()), Times.Never);
        _power.Verify(p => p.GetActivePowerPlanAsync(), Times.Never);
    }

    [Fact]
    public async Task NewBuilder_RendersTheShapesTheOldOneCouldNot()
    {
        var script = await OldBuilder().BuildAsync(NewSet(), ParityCatalog.ByFeature);

        script.Should().Contain("Set-RegistryCompositeValue -Path 'HKCU:\\Software\\Microsoft\\DirectX\\UserGpuPreferences' -Name 'DirectXUserGlobalSettings' -Key 'SwapEffectUpgradeEnable' -SubValue '1'");
        script.Should().Contain("Set-RegistryStringFlag -Path 'HKCU:\\Control Panel\\Accessibility\\MouseKeys' -Name 'Flags' -FlagMask 4 -AbsentBase 62 -Set $True");
        script.Should().Contain("Unlock-RegistryKey -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\ParitySvc'");
        script.Should().Contain("Lock-RegistryKey -Path 'HKLM:\\SYSTEM\\CurrentControlSet\\Services\\ParitySvc'");
        script.Should().Contain("Remove-RegistryValue -Path 'HKCU:\\Software\\ParityReset' -Name 'E'");
        script.Should().Contain("New-RegistryKey -Path 'HKCU:\\Software\\Classes\\CLSID\\{PARITY}'");
        script.Should().NotContain("Set-RegistryValue -Path 'HKCU:\\Software\\ParityReg'", "a .reg import is the apply; its detect-only targets are not written");
    }

    [Fact]
    public async Task NewBuilder_WithNoPowerChoices_EmitsNoPowerSection()
    {
        var script = await OldBuilder().BuildAsync(NewSet(withPowerCfg: false), ParityCatalog.ByFeature);

        script.Should().NotContain("# POWER PLAN & POWERCFG SETTINGS");
    }
}
