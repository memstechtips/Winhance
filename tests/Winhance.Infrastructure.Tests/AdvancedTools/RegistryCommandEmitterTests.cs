using System.Text;
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Helpers;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools;

public class RegistryCommandEmitterTests
{
    private readonly Mock<ILogService> _logService = new();
    private readonly RegistryCommandEmitter _sut;

    public RegistryCommandEmitterTests()
    {
        _sut = new RegistryCommandEmitter(_logService.Object);
    }

    // ---------------------------------------------------------------
    // AppendSelectionCommands (Slice 7e-6: takes the paired catalog Setting; renamed from
    // AppendSelectionCommandsFiltered)
    // ---------------------------------------------------------------

    [Fact]
    public void AppendSelectionCommands_PowerPlanSelection_SkipsEntirely()
    {
        var sb = new StringBuilder();
        var setting = SettingCatalog.Find("power-plan-selection")!;
        var configItem = new ConfigurationItem
        {
            Id = "power-plan-selection",
            InputType = InputType.Selection,
            SelectedIndex = 0
        };

        _sut.AppendSelectionCommands(sb, setting, configItem, isHkcu: false);

        sb.ToString().Trim().Should().BeEmpty();
    }

    [Fact]
    public void AppendSelectionCommands_WithCustomStateValues_AppliesValues()
    {
        var sb = new StringBuilder();
        // Slice 7e-6: the caller passes the REAL catalog selection itself (registry DWORD target "Start";
        // custom value 3 is arbitrary - the emit path has no lock handling).
        var setting = SettingCatalog.Find("gaming-touch-keyboard-service")!;
        var configItem = new ConfigurationItem
        {
            Id = "gaming-touch-keyboard-service",
            InputType = InputType.Selection,
            CustomStateValues = new Dictionary<string, object> { { "Start", 3 } }
        };

        _sut.AppendSelectionCommands(sb, setting, configItem, isHkcu: false);

        sb.ToString().Should().Contain("Set-RegistryValue");
        sb.ToString().Should().Contain("3");
    }

    [Fact]
    public void AppendSelectionCommands_NoValueMappingsOrCustomState_LogsWarning()
    {
        var sb = new StringBuilder();
        // A synthetic catalog Setting (fake id, no states) - the no-CustomStateValues / no-SelectedIndex
        // shape must warn and emit nothing regardless of the setting's write surface.
        var setting = new Setting
        {
            Id = "test-selection",
            Display = new Display { Name = "Test Selection", Description = "Test Selection" }
        };
        var configItem = new ConfigurationItem
        {
            Id = "test-selection",
            InputType = InputType.Selection
        };

        _sut.AppendSelectionCommands(sb, setting, configItem, isHkcu: false);

        _logService.Verify(l => l.Log(
            LogLevel.Warning,
            It.Is<string>(s => s.Contains("test-selection")),
            null), Times.Once);
    }

    // ---------------------------------------------------------------
    // ApplyResolvedValues - ApplyPerMonitor wraps in ForEach
    // ---------------------------------------------------------------

    [Fact]
    public void AppendSelectionCommands_ApplyPerMonitor_WrapsInForEachObject()
    {
        var sb = new StringBuilder();
        // The REAL PerMonitor catalog selection (gaming-auto-color-management, RegTarget PerMonitor=true) -
        // the per-subkey wrap comes from ApplyResolvedValuesFromCatalog's rt.PerMonitor handling.
        var setting = SettingCatalog.Find("gaming-auto-color-management")!;
        var configItem = new ConfigurationItem
        {
            Id = "gaming-auto-color-management",
            InputType = InputType.Selection,
            CustomStateValues = new Dictionary<string, object> { { "AutoColorManagementEnabled", 1 } }
        };

        _sut.AppendSelectionCommands(sb, setting, configItem, isHkcu: false);

        var output = sb.ToString();
        output.Should().Contain("Get-ChildItem");
        output.Should().Contain("ForEach-Object");
        output.Should().Contain("$_.PSPath");
    }

    // ---------------------------------------------------------------
    // AppendRegContentCommandsFromCatalog - mixed-hive rejection
    // ---------------------------------------------------------------

    [Fact]
    public void AppendRegContentCommandsFromCatalog_MixedHiveContent_Throws()
    {
        // Direct emitter-level coverage of the mixed-hive guard (7e-4b): the builder-level routing test
        // was deleted because no paired catalog content can mix hives and unpaired ids now skip at the
        // section gate - but the guard itself stays load-bearing for future authored content, and this
        // method accepts caller-constructed Settings, so the throw is directly testable here.
        var sb = new StringBuilder();
        var mixed = new Winhance.Core.Features.Common.Catalog.Setting
        {
            Id = "synthetic-mixed-regcontent",
            Display = new Winhance.Core.Features.Common.Catalog.Display { Name = "Mixed", Description = "Mixed hives" },
            States = new[]
            {
                new Winhance.Core.Features.Common.Catalog.SettingState
                {
                    Label = "Enabled",
                    Effects = new Winhance.Core.Features.Common.Catalog.Effect[]
                    {
                        new Winhance.Core.Features.Common.Catalog.RegContentEffect(
                            "Windows Registry Editor Version 5.00\r\n\r\n[HKEY_CURRENT_USER\\Software\\Test]\r\n\"A\"=dword:00000001\r\n\r\n[HKEY_LOCAL_MACHINE\\Software\\Test]\r\n\"B\"=dword:00000001\r\n"),
                    },
                },
                new Winhance.Core.Features.Common.Catalog.SettingState { Label = "Disabled" },
            },
        };

        var act = () => _sut.AppendRegContentCommandsFromCatalog(sb, mixed, isEnabled: true, isHkcuPass: false);

        act.Should().Throw<InvalidOperationException>().WithMessage("*mixes HKEY_CURRENT_USER and system-hive*");
    }
}
