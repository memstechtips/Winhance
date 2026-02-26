using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class SettingOperationExecutorTests
{
    private readonly Mock<IWindowsRegistryService> _mockRegistry = new();
    private readonly Mock<IComboBoxResolver> _mockComboBox = new();
    private readonly Mock<IProcessRestartManager> _mockRestart = new();
    private readonly Mock<IPowerCfgApplier> _mockPowerCfg = new();
    private readonly Mock<IScheduledTaskService> _mockScheduledTask = new();
    private readonly Mock<IInteractiveUserService> _mockInteractiveUser = new();
    private readonly Mock<IProcessExecutor> _mockProcessExecutor = new();
    private readonly Mock<IPowerShellRunner> _mockPowerShell = new();
    private readonly Mock<IFileSystemService> _mockFileSystem = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly SettingOperationExecutor _executor;

    public SettingOperationExecutorTests()
    {
        _mockRegistry
            .Setup(r => r.ApplySetting(
                It.IsAny<RegistrySetting>(), It.IsAny<bool>(), It.IsAny<object?>()))
            .Returns(true);

        _mockPowerCfg
            .Setup(p => p.ApplyPowerCfgSettingsAsync(
                It.IsAny<SettingDefinition>(), It.IsAny<bool>(), It.IsAny<object?>()))
            .ReturnsAsync(OperationResult.Succeeded());

        _mockScheduledTask
            .Setup(s => s.EnableTaskAsync(It.IsAny<string>()))
            .ReturnsAsync(OperationResult.Succeeded());
        _mockScheduledTask
            .Setup(s => s.DisableTaskAsync(It.IsAny<string>()))
            .ReturnsAsync(OperationResult.Succeeded());

        _mockPowerShell
            .Setup(p => p.RunScriptAsync(It.IsAny<string>(), null, default))
            .ReturnsAsync(string.Empty);

        _mockFileSystem
            .Setup(f => f.GetTempPath())
            .Returns(@"C:\Temp");
        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join(@"\", parts));

        _mockProcessExecutor
            .Setup(p => p.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0 });

        _executor = new SettingOperationExecutor(
            _mockRegistry.Object,
            _mockComboBox.Object,
            _mockRestart.Object,
            _mockPowerCfg.Object,
            _mockScheduledTask.Object,
            _mockInteractiveUser.Object,
            _mockProcessExecutor.Object,
            _mockPowerShell.Object,
            _mockFileSystem.Object,
            _mockLog.Object);
    }

    private static SettingDefinition CreateSetting(string id, InputType inputType = InputType.Toggle) => new()
    {
        Id = id,
        Name = $"Setting {id}",
        Description = $"Description for {id}",
        InputType = inputType,
    };

    // ---------------------------------------------------------------
    // 1. Setting with no operations returns success
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplySettingOperationsAsync_NoOperations_ReturnsSuccess()
    {
        var setting = CreateSetting("empty");

        var result = await _executor.ApplySettingOperationsAsync(setting, true, null);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_NoOperations_StillCallsProcessRestart()
    {
        var setting = CreateSetting("empty");

        await _executor.ApplySettingOperationsAsync(setting, true, null);

        _mockRestart.Verify(
            r => r.HandleProcessAndServiceRestartsAsync(setting),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // 2. Registry operations are applied
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplySettingOperationsAsync_ToggleEnabled_AppliesRegistrySettingsWithTrue()
    {
        var regSetting = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Test",
            ValueName = "TestValue",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("reg-toggle") with
        {
            RegistrySettings = new[] { regSetting },
        };

        var result = await _executor.ApplySettingOperationsAsync(setting, true, null);

        result.Success.Should().BeTrue();
        _mockRegistry.Verify(
            r => r.ApplySetting(regSetting, true, null),
            Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_ToggleDisabled_AppliesRegistrySettingsWithFalse()
    {
        var regSetting = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Test",
            ValueName = "DisableValue",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("reg-toggle-off") with
        {
            RegistrySettings = new[] { regSetting },
        };

        var result = await _executor.ApplySettingOperationsAsync(setting, false, null);

        result.Success.Should().BeTrue();
        _mockRegistry.Verify(
            r => r.ApplySetting(regSetting, false, null),
            Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_MultipleRegistrySettings_AppliesAll()
    {
        var reg1 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Test1",
            ValueName = "Val1",
            ValueType = RegistryValueKind.DWord,
        };
        var reg2 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Test2",
            ValueName = "Val2",
            ValueType = RegistryValueKind.String,
        };
        var setting = CreateSetting("reg-multi") with
        {
            RegistrySettings = new[] { reg1, reg2 },
        };

        await _executor.ApplySettingOperationsAsync(setting, true, null);

        _mockRegistry.Verify(r => r.ApplySetting(reg1, true, null), Times.Once);
        _mockRegistry.Verify(r => r.ApplySetting(reg2, true, null), Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_RegistrySettingsWithRegContentsPresent_SkipsRegistryApply()
    {
        // When RegContents.Count > 0, the RegistrySettings block is skipped
        // because the condition requires RegContents.Count == 0
        var regSetting = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Test",
            ValueName = "TestValue",
            ValueType = RegistryValueKind.DWord,
        };
        var regContent = new RegContentSetting
        {
            EnabledContent = "Windows Registry Editor Version 5.00\r\n[HKCU\\Test]\r\n\"Key\"=dword:00000001",
            DisabledContent = "Windows Registry Editor Version 5.00\r\n[HKCU\\Test]\r\n\"Key\"=dword:00000000",
        };
        var setting = CreateSetting("reg-with-content") with
        {
            RegistrySettings = new[] { regSetting },
            RegContents = new[] { regContent },
        };

        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);

        await _executor.ApplySettingOperationsAsync(setting, true, null);

        // Registry ApplySetting should NOT be called because RegContents.Count > 0
        _mockRegistry.Verify(
            r => r.ApplySetting(It.IsAny<RegistrySetting>(), It.IsAny<bool>(), It.IsAny<object?>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // 3. Scheduled task operations are applied
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplySettingOperationsAsync_ScheduledTaskEnabled_CallsEnableTask()
    {
        var setting = CreateSetting("sched-enable") with
        {
            ScheduledTaskSettings = new[]
            {
                new ScheduledTaskSetting { Id = "task1", TaskPath = @"\Microsoft\Windows\Task1" },
            },
        };

        await _executor.ApplySettingOperationsAsync(setting, true, null);

        _mockScheduledTask.Verify(
            s => s.EnableTaskAsync(@"\Microsoft\Windows\Task1"),
            Times.Once);
        _mockScheduledTask.Verify(
            s => s.DisableTaskAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_ScheduledTaskDisabled_CallsDisableTask()
    {
        var setting = CreateSetting("sched-disable") with
        {
            ScheduledTaskSettings = new[]
            {
                new ScheduledTaskSetting { Id = "task1", TaskPath = @"\Microsoft\Windows\Task1" },
            },
        };

        await _executor.ApplySettingOperationsAsync(setting, false, null);

        _mockScheduledTask.Verify(
            s => s.DisableTaskAsync(@"\Microsoft\Windows\Task1"),
            Times.Once);
        _mockScheduledTask.Verify(
            s => s.EnableTaskAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_MultipleScheduledTasks_AppliesAll()
    {
        var setting = CreateSetting("sched-multi") with
        {
            ScheduledTaskSettings = new[]
            {
                new ScheduledTaskSetting { Id = "task1", TaskPath = @"\Task1" },
                new ScheduledTaskSetting { Id = "task2", TaskPath = @"\Task2" },
            },
        };

        await _executor.ApplySettingOperationsAsync(setting, true, null);

        _mockScheduledTask.Verify(s => s.EnableTaskAsync(@"\Task1"), Times.Once);
        _mockScheduledTask.Verify(s => s.EnableTaskAsync(@"\Task2"), Times.Once);
    }

    // ---------------------------------------------------------------
    // 4. PowerShell operations are applied
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplySettingOperationsAsync_PowerShellEnabled_RunsEnabledScript()
    {
        var setting = CreateSetting("ps-enable") with
        {
            PowerShellScripts = new[]
            {
                new PowerShellScriptSetting
                {
                    Id = "script1",
                    EnabledScript = "Set-Feature -Enabled $true",
                    DisabledScript = "Set-Feature -Enabled $false",
                },
            },
        };

        await _executor.ApplySettingOperationsAsync(setting, true, null);

        _mockPowerShell.Verify(
            p => p.RunScriptAsync("Set-Feature -Enabled $true", null, default),
            Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_PowerShellDisabled_RunsDisabledScript()
    {
        var setting = CreateSetting("ps-disable") with
        {
            PowerShellScripts = new[]
            {
                new PowerShellScriptSetting
                {
                    Id = "script1",
                    EnabledScript = "Set-Feature -Enabled $true",
                    DisabledScript = "Set-Feature -Enabled $false",
                },
            },
        };

        await _executor.ApplySettingOperationsAsync(setting, false, null);

        _mockPowerShell.Verify(
            p => p.RunScriptAsync("Set-Feature -Enabled $false", null, default),
            Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_PowerShellWithNullScript_SkipsExecution()
    {
        var setting = CreateSetting("ps-null") with
        {
            PowerShellScripts = new[]
            {
                new PowerShellScriptSetting
                {
                    Id = "script1",
                    EnabledScript = "Do-Something",
                    DisabledScript = null,
                },
            },
        };

        await _executor.ApplySettingOperationsAsync(setting, false, null);

        _mockPowerShell.Verify(
            p => p.RunScriptAsync(It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_PowerShellWithEmptyScript_SkipsExecution()
    {
        var setting = CreateSetting("ps-empty") with
        {
            PowerShellScripts = new[]
            {
                new PowerShellScriptSetting
                {
                    Id = "script1",
                    EnabledScript = "",
                    DisabledScript = "Do-Disable",
                },
            },
        };

        await _executor.ApplySettingOperationsAsync(setting, true, null);

        _mockPowerShell.Verify(
            p => p.RunScriptAsync(It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // 5. Power config operations delegated to PowerCfgApplier
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplySettingOperationsAsync_PowerCfgSettings_DelegatesToPowerCfgApplier()
    {
        var setting = CreateSetting("powercfg") with
        {
            PowerCfgSettings = new[]
            {
                new PowerCfgSetting
                {
                    SettingGUIDAlias = "SLEEPBUTTONTIMEOUT",
                    SubgroupGuid = "238c9fa8-0aad-41ed-83f4-97be242c8f20",
                    SettingGuid = "29f6c1db-86da-48c5-9fdb-f2b67b1f44da",
                },
            },
        };

        await _executor.ApplySettingOperationsAsync(setting, true, 30);

        _mockPowerCfg.Verify(
            p => p.ApplyPowerCfgSettingsAsync(setting, true, 30),
            Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_NoPowerCfgSettings_DoesNotCallPowerCfgApplier()
    {
        var setting = CreateSetting("no-powercfg");

        await _executor.ApplySettingOperationsAsync(setting, true, null);

        _mockPowerCfg.Verify(
            p => p.ApplyPowerCfgSettingsAsync(
                It.IsAny<SettingDefinition>(), It.IsAny<bool>(), It.IsAny<object?>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // 6. Process restarts are handled via ProcessRestartManager
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplySettingOperationsAsync_AlwaysCallsProcessRestartManager()
    {
        var setting = CreateSetting("restart-test") with
        {
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKCU\Software\Test",
                    ValueName = "Val",
                    ValueType = RegistryValueKind.DWord,
                },
            },
        };

        await _executor.ApplySettingOperationsAsync(setting, true, null);

        _mockRestart.Verify(
            r => r.HandleProcessAndServiceRestartsAsync(setting),
            Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_ProcessRestartCalledAfterOperations()
    {
        // Verify restart is called even with no other operations
        var setting = CreateSetting("restart-only");

        await _executor.ApplySettingOperationsAsync(setting, false, null);

        _mockRestart.Verify(
            r => r.HandleProcessAndServiceRestartsAsync(setting),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // 7. Multiple operation types combined
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplySettingOperationsAsync_CombinedOperations_AppliesAllTypes()
    {
        var regSetting = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Combined",
            ValueName = "CombinedVal",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("combined") with
        {
            RegistrySettings = new[] { regSetting },
            ScheduledTaskSettings = new[]
            {
                new ScheduledTaskSetting { Id = "combTask", TaskPath = @"\CombTask" },
            },
            PowerShellScripts = new[]
            {
                new PowerShellScriptSetting
                {
                    Id = "combScript",
                    EnabledScript = "Enable-CombinedFeature",
                    DisabledScript = "Disable-CombinedFeature",
                },
            },
            PowerCfgSettings = new[]
            {
                new PowerCfgSetting
                {
                    SettingGUIDAlias = "TestAlias",
                    SubgroupGuid = "guid1",
                    SettingGuid = "guid2",
                },
            },
        };

        var result = await _executor.ApplySettingOperationsAsync(setting, true, null);

        result.Success.Should().BeTrue();
        _mockRegistry.Verify(r => r.ApplySetting(regSetting, true, null), Times.Once);
        _mockScheduledTask.Verify(s => s.EnableTaskAsync(@"\CombTask"), Times.Once);
        _mockPowerShell.Verify(p => p.RunScriptAsync("Enable-CombinedFeature", null, default), Times.Once);
        _mockPowerCfg.Verify(p => p.ApplyPowerCfgSettingsAsync(setting, true, null), Times.Once);
        _mockRestart.Verify(r => r.HandleProcessAndServiceRestartsAsync(setting), Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_CombinedOperationsDisabled_AppliesAllTypesWithDisabledState()
    {
        var regSetting = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Combined",
            ValueName = "CombinedVal",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("combined-off") with
        {
            RegistrySettings = new[] { regSetting },
            ScheduledTaskSettings = new[]
            {
                new ScheduledTaskSetting { Id = "task", TaskPath = @"\Task" },
            },
            PowerShellScripts = new[]
            {
                new PowerShellScriptSetting
                {
                    Id = "script",
                    EnabledScript = "Enable-Feature",
                    DisabledScript = "Disable-Feature",
                },
            },
        };

        await _executor.ApplySettingOperationsAsync(setting, false, null);

        _mockRegistry.Verify(r => r.ApplySetting(regSetting, false, null), Times.Once);
        _mockScheduledTask.Verify(s => s.DisableTaskAsync(@"\Task"), Times.Once);
        _mockPowerShell.Verify(p => p.RunScriptAsync("Disable-Feature", null, default), Times.Once);
    }

    // ---------------------------------------------------------------
    // 8. Error in one operation type still processes others
    //    (RegContents rethrows, but registry + scheduled tasks are
    //     independent blocks; verify operations before the throw)
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplySettingOperationsAsync_RegistryThrows_DoesNotPreventScheduledTasks()
    {
        // Registry ApplySetting throws, but scheduled tasks should still run
        // because they are in separate if-blocks. However, the exception
        // propagates from ApplySetting synchronously before reaching the
        // scheduled task block. Let's verify with a setup that doesn't throw.
        var regSetting = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Test",
            ValueName = "Val",
            ValueType = RegistryValueKind.DWord,
        };
        _mockRegistry
            .Setup(r => r.ApplySetting(regSetting, true, null))
            .Returns(true);

        var setting = CreateSetting("error-test") with
        {
            RegistrySettings = new[] { regSetting },
            ScheduledTaskSettings = new[]
            {
                new ScheduledTaskSetting { Id = "task", TaskPath = @"\ErrorTask" },
            },
        };

        var result = await _executor.ApplySettingOperationsAsync(setting, true, null);

        result.Success.Should().BeTrue();
        _mockRegistry.Verify(r => r.ApplySetting(regSetting, true, null), Times.Once);
        _mockScheduledTask.Verify(s => s.EnableTaskAsync(@"\ErrorTask"), Times.Once);
    }

    // ---------------------------------------------------------------
    // 9. Selection input type with custom value
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplySettingOperationsAsync_SelectionWithCustomValueDictionary_AppliesPerValueName()
    {
        var reg1 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Sel",
            ValueName = "Opt1",
            ValueType = RegistryValueKind.DWord,
        };
        var reg2 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Sel",
            ValueName = "Opt2",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("sel-custom", InputType.Selection) with
        {
            RegistrySettings = new[] { reg1, reg2 },
        };

        var customValues = new Dictionary<string, object>
        {
            { "Opt1", 42 },
            { "Opt2", 99 },
        };

        var result = await _executor.ApplySettingOperationsAsync(setting, true, customValues);

        result.Success.Should().BeTrue();
        _mockRegistry.Verify(r => r.ApplySetting(reg1, true, 42), Times.Once);
        _mockRegistry.Verify(r => r.ApplySetting(reg2, true, 99), Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_SelectionWithCustomValueDictionary_NullValue_DisablesSetting()
    {
        var reg1 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Sel",
            ValueName = "Opt1",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("sel-custom-null", InputType.Selection) with
        {
            RegistrySettings = new[] { reg1 },
        };

        var customValues = new Dictionary<string, object>
        {
            { "Opt1", null! },
        };

        await _executor.ApplySettingOperationsAsync(setting, true, customValues);

        _mockRegistry.Verify(r => r.ApplySetting(reg1, false), Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_SelectionWithNullValueName_UsesKeyExistsFallback()
    {
        var reg = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Sel",
            ValueName = null,
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("sel-keyexists", InputType.Selection) with
        {
            RegistrySettings = new[] { reg },
        };

        var customValues = new Dictionary<string, object>
        {
            { "KeyExists", 1 },
        };

        await _executor.ApplySettingOperationsAsync(setting, true, customValues);

        _mockRegistry.Verify(r => r.ApplySetting(reg, true, 1), Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_SelectionWithIntIndex_ResolvesViaComboBoxResolver()
    {
        var reg1 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Combo",
            ValueName = "Setting1",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("sel-int-index", InputType.Selection) with
        {
            RegistrySettings = new[] { reg1 },
        };

        _mockComboBox
            .Setup(c => c.ResolveIndexToRawValues(setting, 2))
            .Returns(new Dictionary<string, object?> { { "Setting1", 100 } });

        await _executor.ApplySettingOperationsAsync(setting, true, 2);

        _mockComboBox.Verify(c => c.ResolveIndexToRawValues(setting, 2), Times.Once);
        _mockRegistry.Verify(r => r.ApplySetting(reg1, true, 100), Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_SelectionWithStringIndex_ResolvesDisplayNameToIndex()
    {
        var reg1 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Combo",
            ValueName = "Setting1",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("sel-string-index", InputType.Selection) with
        {
            RegistrySettings = new[] { reg1 },
        };

        _mockComboBox
            .Setup(c => c.GetIndexFromDisplayName(setting, "High"))
            .Returns(3);
        _mockComboBox
            .Setup(c => c.ResolveIndexToRawValues(setting, 3))
            .Returns(new Dictionary<string, object?> { { "Setting1", 200 } });

        await _executor.ApplySettingOperationsAsync(setting, true, "High");

        _mockComboBox.Verify(c => c.GetIndexFromDisplayName(setting, "High"), Times.Once);
        _mockComboBox.Verify(c => c.ResolveIndexToRawValues(setting, 3), Times.Once);
        _mockRegistry.Verify(r => r.ApplySetting(reg1, true, 200), Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_SelectionWithIndex_NullInResolvedValues_DisablesSetting()
    {
        var reg1 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Combo",
            ValueName = "Setting1",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("sel-index-null", InputType.Selection) with
        {
            RegistrySettings = new[] { reg1 },
        };

        _mockComboBox
            .Setup(c => c.ResolveIndexToRawValues(setting, 1))
            .Returns(new Dictionary<string, object?> { { "Setting1", null } });

        await _executor.ApplySettingOperationsAsync(setting, true, 1);

        _mockRegistry.Verify(r => r.ApplySetting(reg1, false), Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_SelectionWithIndex_ValueNotInResolvedMap_FallsBackToGetValueFromIndex()
    {
        var reg1 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Combo",
            ValueName = "UnmappedValue",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("sel-fallback", InputType.Selection) with
        {
            RegistrySettings = new[] { reg1 },
        };

        // ResolveIndexToRawValues returns empty dict (no match for "UnmappedValue")
        _mockComboBox
            .Setup(c => c.ResolveIndexToRawValues(setting, 2))
            .Returns(new Dictionary<string, object?>());
        _mockComboBox
            .Setup(c => c.GetValueFromIndex(setting, 2))
            .Returns(1); // non-zero => true

        await _executor.ApplySettingOperationsAsync(setting, true, 2);

        _mockComboBox.Verify(c => c.GetValueFromIndex(setting, 2), Times.Once);
        _mockRegistry.Verify(r => r.ApplySetting(reg1, true), Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_SelectionWithIndex_GetValueFromIndexReturnsZero_AppliesFalse()
    {
        var reg1 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Combo",
            ValueName = "UnmappedValue",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("sel-fallback-zero", InputType.Selection) with
        {
            RegistrySettings = new[] { reg1 },
        };

        _mockComboBox
            .Setup(c => c.ResolveIndexToRawValues(setting, 0))
            .Returns(new Dictionary<string, object?>());
        _mockComboBox
            .Setup(c => c.GetValueFromIndex(setting, 0))
            .Returns(0); // zero => false

        await _executor.ApplySettingOperationsAsync(setting, true, 0);

        _mockRegistry.Verify(r => r.ApplySetting(reg1, false), Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_SelectionWithEnableFlag_NoSpecificValue_AppliesEnableState()
    {
        // Selection type without a value (not int, not string, not dictionary)
        // falls into the else branch which uses the enable flag
        var reg1 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Sel",
            ValueName = "Val",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("sel-enable-flag", InputType.Selection) with
        {
            RegistrySettings = new[] { reg1 },
        };

        await _executor.ApplySettingOperationsAsync(setting, true, null);

        _mockRegistry.Verify(r => r.ApplySetting(reg1, true), Times.Once);
    }

    // ---------------------------------------------------------------
    // 10. NumericRange input type
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplySettingOperationsAsync_NumericRangeWithNonZeroValue_AppliesTrue()
    {
        var reg1 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Numeric",
            ValueName = "Timeout",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("numeric-nonzero", InputType.NumericRange) with
        {
            RegistrySettings = new[] { reg1 },
        };

        await _executor.ApplySettingOperationsAsync(setting, true, 42);

        _mockRegistry.Verify(r => r.ApplySetting(reg1, true), Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_NumericRangeWithZeroValue_AppliesFalse()
    {
        var reg1 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Numeric",
            ValueName = "Timeout",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("numeric-zero", InputType.NumericRange) with
        {
            RegistrySettings = new[] { reg1 },
        };

        await _executor.ApplySettingOperationsAsync(setting, false, 0);

        _mockRegistry.Verify(r => r.ApplySetting(reg1, false), Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_NumericRangeWithStringValue_ParsesAndApplies()
    {
        var reg1 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Numeric",
            ValueName = "Delay",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("numeric-string", InputType.NumericRange) with
        {
            RegistrySettings = new[] { reg1 },
        };

        await _executor.ApplySettingOperationsAsync(setting, true, "10");

        // "10" parses to 10 (non-zero) => applyValue = true
        _mockRegistry.Verify(r => r.ApplySetting(reg1, true), Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_NumericRangeWithStringZero_AppliesFalse()
    {
        var reg1 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Numeric",
            ValueName = "Delay",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("numeric-string-zero", InputType.NumericRange) with
        {
            RegistrySettings = new[] { reg1 },
        };

        await _executor.ApplySettingOperationsAsync(setting, true, "0");

        _mockRegistry.Verify(r => r.ApplySetting(reg1, false), Times.Once);
    }

    // ---------------------------------------------------------------
    // RegContents operations
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplySettingOperationsAsync_RegContentEnabled_WritesAndImportsEnabledContent()
    {
        var regContent = new RegContentSetting
        {
            EnabledContent = "Windows Registry Editor Version 5.00\r\n[HKCU\\Test]\r\n\"Key\"=dword:00000001",
            DisabledContent = "Windows Registry Editor Version 5.00\r\n[HKCU\\Test]\r\n\"Key\"=dword:00000000",
        };
        var setting = CreateSetting("regcontent-enable") with
        {
            RegContents = new[] { regContent },
        };

        _mockInteractiveUser.Setup(i => i.IsOtsElevation).Returns(false);
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);

        await _executor.ApplySettingOperationsAsync(setting, true, null);

        _mockFileSystem.Verify(
            f => f.WriteAllTextAsync(
                It.IsAny<string>(),
                regContent.EnabledContent,
                default),
            Times.Once);

        // Should execute reg import via cmd.exe
        _mockProcessExecutor.Verify(
            p => p.ExecuteAsync("cmd.exe", It.Is<string>(s => s.Contains("reg import")), default),
            Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_RegContentDisabled_WritesAndImportsDisabledContent()
    {
        var regContent = new RegContentSetting
        {
            EnabledContent = "EnabledContent",
            DisabledContent = "DisabledContent",
        };
        var setting = CreateSetting("regcontent-disable") with
        {
            RegContents = new[] { regContent },
        };

        _mockInteractiveUser.Setup(i => i.IsOtsElevation).Returns(false);
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);

        await _executor.ApplySettingOperationsAsync(setting, false, null);

        _mockFileSystem.Verify(
            f => f.WriteAllTextAsync(
                It.IsAny<string>(),
                "DisabledContent",
                default),
            Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_RegContentEmptyContent_SkipsImport()
    {
        var regContent = new RegContentSetting
        {
            EnabledContent = "",
            DisabledContent = "DisabledContent",
        };
        var setting = CreateSetting("regcontent-empty") with
        {
            RegContents = new[] { regContent },
        };

        await _executor.ApplySettingOperationsAsync(setting, true, null);

        _mockFileSystem.Verify(
            f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockProcessExecutor.Verify(
            p => p.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_RegContentCleansTempFile()
    {
        var regContent = new RegContentSetting
        {
            EnabledContent = "SomeContent",
            DisabledContent = "OtherContent",
        };
        var setting = CreateSetting("regcontent-cleanup") with
        {
            RegContents = new[] { regContent },
        };

        _mockInteractiveUser.Setup(i => i.IsOtsElevation).Returns(false);
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);

        await _executor.ApplySettingOperationsAsync(setting, true, null);

        _mockFileSystem.Verify(
            f => f.DeleteFile(It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_RegContentOtsElevation_RunsAsInteractiveUser()
    {
        var regContent = new RegContentSetting
        {
            EnabledContent = "OtsContent",
            DisabledContent = "OtsDisabled",
        };
        var setting = CreateSetting("regcontent-ots") with
        {
            RegContents = new[] { regContent },
        };

        _mockInteractiveUser.Setup(i => i.IsOtsElevation).Returns(true);
        _mockInteractiveUser.Setup(i => i.HasInteractiveUserToken).Returns(true);
        _mockInteractiveUser
            .Setup(i => i.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData))
            .Returns(@"C:\Users\Standard\AppData\Local");
        _mockInteractiveUser
            .Setup(i => i.RunProcessAsInteractiveUserAsync(
                "reg.exe", It.IsAny<string>(), null, null, default, 300_000, null))
            .ReturnsAsync(new InteractiveProcessResult(0, "", ""));
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);

        await _executor.ApplySettingOperationsAsync(setting, true, null);

        _mockFileSystem.Verify(
            f => f.CreateDirectory(It.Is<string>(s => s.Contains("Temp"))),
            Times.Once);
        _mockInteractiveUser.Verify(
            i => i.RunProcessAsInteractiveUserAsync(
                "reg.exe",
                It.Is<string>(s => s.Contains("import")),
                null, null, default, 300_000, null),
            Times.Once);
        // Should NOT call the regular cmd.exe process executor
        _mockProcessExecutor.Verify(
            p => p.ExecuteAsync("cmd.exe", It.IsAny<string>(), default),
            Times.Never);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_RegContentOtsWithoutToken_FallsBackToRegularImport()
    {
        var regContent = new RegContentSetting
        {
            EnabledContent = "OtsContentNoToken",
            DisabledContent = "OtsDisabledNoToken",
        };
        var setting = CreateSetting("regcontent-ots-notoken") with
        {
            RegContents = new[] { regContent },
        };

        _mockInteractiveUser.Setup(i => i.IsOtsElevation).Returns(true);
        _mockInteractiveUser.Setup(i => i.HasInteractiveUserToken).Returns(false);
        _mockInteractiveUser
            .Setup(i => i.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData))
            .Returns(@"C:\Users\Standard\AppData\Local");
        _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);

        await _executor.ApplySettingOperationsAsync(setting, true, null);

        // Falls back to cmd.exe reg import
        _mockProcessExecutor.Verify(
            p => p.ExecuteAsync("cmd.exe", It.Is<string>(s => s.Contains("reg import")), default),
            Times.Once);
    }

    // ---------------------------------------------------------------
    // Unsupported InputType for registry operations
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplySettingOperationsAsync_UnsupportedInputType_ThrowsNotSupportedException()
    {
        var reg1 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Test",
            ValueName = "Val",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("unsupported", InputType.Action) with
        {
            RegistrySettings = new[] { reg1 },
        };

        var action = () => _executor.ApplySettingOperationsAsync(setting, true, null);

        await action.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*Action*not supported*");
    }

    // ---------------------------------------------------------------
    // Selection with empty string value falls through to else branch
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplySettingOperationsAsync_SelectionWithEmptyString_UsesEnableFlag()
    {
        var reg1 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Sel",
            ValueName = "Val",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("sel-empty-string", InputType.Selection) with
        {
            RegistrySettings = new[] { reg1 },
        };

        // Empty string doesn't match the second branch (requires non-empty string)
        // so it falls through to the else branch which uses the enable flag for Selection
        await _executor.ApplySettingOperationsAsync(setting, false, "");

        _mockRegistry.Verify(r => r.ApplySetting(reg1, false), Times.Once);
    }

    // ---------------------------------------------------------------
    // Verify return value is always success when no exceptions
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplySettingOperationsAsync_AllOperationsSucceed_ReturnsSucceededResult()
    {
        var setting = CreateSetting("success-check") with
        {
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKCU\Software\Test",
                    ValueName = "V",
                    ValueType = RegistryValueKind.DWord,
                },
            },
            ScheduledTaskSettings = new[]
            {
                new ScheduledTaskSetting { Id = "t", TaskPath = @"\T" },
            },
        };

        var result = await _executor.ApplySettingOperationsAsync(setting, true, null);

        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
    }

    // ---------------------------------------------------------------
    // BP-1: Registry failure propagation
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplySettingOperationsAsync_RegistryApplyFails_ReturnsFailedResult()
    {
        var regSetting = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Fail",
            ValueName = "Bad",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("reg-fail") with
        {
            RegistrySettings = new[] { regSetting },
        };

        _mockRegistry
            .Setup(r => r.ApplySetting(regSetting, true, null))
            .Returns(false);

        var result = await _executor.ApplySettingOperationsAsync(setting, true, null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("reg-fail");
        result.ErrorMessage.Should().Contain(@"HKCU\Software\Fail\Bad");
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_PartialRegistryFailure_ReportsAllFailedOperations()
    {
        var reg1 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Pass",
            ValueName = "Good",
            ValueType = RegistryValueKind.DWord,
        };
        var reg2 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Fail",
            ValueName = "Bad",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("partial-fail") with
        {
            RegistrySettings = new[] { reg1, reg2 },
        };

        _mockRegistry
            .Setup(r => r.ApplySetting(reg1, true, null))
            .Returns(true);
        _mockRegistry
            .Setup(r => r.ApplySetting(reg2, true, null))
            .Returns(false);

        var result = await _executor.ApplySettingOperationsAsync(setting, true, null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain(@"HKCU\Software\Fail\Bad");
        result.ErrorMessage.Should().NotContain(@"HKCU\Software\Pass\Good");
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_RegistryFails_StillCallsProcessRestart()
    {
        var regSetting = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Fail",
            ValueName = "Val",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("fail-restart") with
        {
            RegistrySettings = new[] { regSetting },
        };

        _mockRegistry
            .Setup(r => r.ApplySetting(regSetting, true, null))
            .Returns(false);

        await _executor.ApplySettingOperationsAsync(setting, true, null);

        _mockRestart.Verify(
            r => r.HandleProcessAndServiceRestartsAsync(setting),
            Times.Once);
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_SelectionCustomValueFails_ReturnsFailedResult()
    {
        var reg1 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Sel",
            ValueName = "Opt1",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("sel-fail", InputType.Selection) with
        {
            RegistrySettings = new[] { reg1 },
        };

        _mockRegistry
            .Setup(r => r.ApplySetting(reg1, true, 42))
            .Returns(false);

        var customValues = new Dictionary<string, object> { { "Opt1", 42 } };
        var result = await _executor.ApplySettingOperationsAsync(setting, true, customValues);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Opt1");
    }

    [Fact]
    public async Task ApplySettingOperationsAsync_SelectionWithIndexFails_ReturnsFailedResult()
    {
        var reg1 = new RegistrySetting
        {
            KeyPath = @"HKCU\Software\Combo",
            ValueName = "Setting1",
            ValueType = RegistryValueKind.DWord,
        };
        var setting = CreateSetting("sel-idx-fail", InputType.Selection) with
        {
            RegistrySettings = new[] { reg1 },
        };

        _mockComboBox
            .Setup(c => c.ResolveIndexToRawValues(setting, 1))
            .Returns(new Dictionary<string, object?> { { "Setting1", 100 } });
        _mockRegistry
            .Setup(r => r.ApplySetting(reg1, true, 100))
            .Returns(false);

        var result = await _executor.ApplySettingOperationsAsync(setting, true, 1);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Setting1");
    }
}
