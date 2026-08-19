using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
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

    // Every parity setting at its "on"/first-option value, the way a .winhance export writes it.
    internal static WinhanceConfigFile OldConfig()
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
}
