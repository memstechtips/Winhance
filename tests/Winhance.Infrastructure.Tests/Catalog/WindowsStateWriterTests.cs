using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Native;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Infrastructure.Features.Common.Catalog;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

/// <summary>Unit tests for the live <see cref="WindowsStateWriter"/>: each writer method must delegate to the right
/// WindowsRegistryService primitive / scheduled-task / powercfg / effect service with the right arguments (the byte
/// logic itself lives in the proven primitives and is covered elsewhere; the native CallNtPowerInformation in the
/// NativePowerEffect branch is review + apply-smoke gated).</summary>
public class WindowsStateWriterTests
{
    private const string Path = @"HKEY_LOCAL_MACHINE\SOFTWARE\Winhance\Test";
    private const string ValueName = "MyValue";

    private readonly Mock<IWindowsRegistryService> _reg = new(MockBehavior.Strict);
    private readonly Mock<IScheduledTaskStateService> _tasks = new(MockBehavior.Strict);
    private readonly Mock<IPowerCfgApplier> _powerCfg = new(MockBehavior.Strict);
    private readonly Mock<IPowerPlanActivationService> _activation = new();
    private readonly Mock<ILogService> _log = new();
    private readonly WindowsStateWriter _sut;

    public WindowsStateWriterTests()
    {
        _sut = new WindowsStateWriter(_reg.Object, _tasks.Object, _powerCfg.Object, _activation.Object, _log.Object);
    }

    private static RegTarget Reg(string? valueName = ValueName, RegistryValueKind kind = RegistryValueKind.DWord) =>
        new("key", new[] { Path }, valueName, kind);

    // --- WriteRegistry: CreateKey-first, then SetValue ---

    [Fact]
    public void WriteRegistry_CreatesKeyThenSetsValue()
    {
        _reg.Setup(r => r.CreateKey(Path)).Returns(true);
        _reg.Setup(r => r.SetValue(Path, ValueName, 1, RegistryValueKind.DWord)).Returns(true);

        _sut.WriteRegistry(Reg(), Path, 1).Should().BeTrue();

        _reg.Verify(r => r.CreateKey(Path), Times.Once);
        _reg.Verify(r => r.SetValue(Path, ValueName, 1, RegistryValueKind.DWord), Times.Once);
    }

    // --- ActivatePowerPlan: delegate to IPowerPlanActivationService.EnsureActivatedAsync
    //     (import-if-missing + activate + InvalidateCache); a cheap guard still rejects an invalid GUID. ---

    [Fact]
    public void ActivatePowerPlan_DelegatesToActivationService_AndReturnsTrueOnSuccess()
    {
        var guid = Guid.NewGuid();
        _activation
            .Setup(a => a.EnsureActivatedAsync(guid.ToString(), It.IsAny<string?>()))
            .ReturnsAsync((true, guid.ToString()));

        _sut.ActivatePowerPlan(guid.ToString()).Should().BeTrue();

        _activation.Verify(a => a.EnsureActivatedAsync(guid.ToString(), It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public void ActivatePowerPlan_ReturnsFalse_OnInvalidGuid()
    {
        // The cheap up-front guard rejects an unparseable GUID without reaching the activation service.
        _sut.ActivatePowerPlan("not-a-guid").Should().BeFalse();

        _activation.Verify(a => a.EnsureActivatedAsync(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public void ActivatePowerPlan_ReturnsFalse_WhenActivationServiceReportsFailure()
    {
        var guid = Guid.NewGuid();
        _activation
            .Setup(a => a.EnsureActivatedAsync(guid.ToString(), It.IsAny<string?>()))
            .ReturnsAsync((false, guid.ToString()));

        _sut.ActivatePowerPlan(guid.ToString()).Should().BeFalse();
    }

    [Fact]
    public void WriteRegistry_WhenCreateKeyFails_DoesNotWriteAndReturnsFalse()
    {
        _reg.Setup(r => r.CreateKey(Path)).Returns(false);

        _sut.WriteRegistry(Reg(), Path, 1).Should().BeFalse();

        _reg.Verify(r => r.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Never);
    }

    // --- DeleteRegistry: named value -> DeleteValue; ValueName-less -> DeleteKey ---

    [Fact]
    public void DeleteRegistry_NamedValue_DeletesValue()
    {
        _reg.Setup(r => r.DeleteValue(Path, ValueName)).Returns(true);

        _sut.DeleteRegistry(Reg(), Path).Should().BeTrue();

        _reg.Verify(r => r.DeleteValue(Path, ValueName), Times.Once);
        _reg.Verify(r => r.DeleteKey(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void DeleteRegistry_ValueNameless_DeletesKey()
    {
        _reg.Setup(r => r.DeleteKey(Path)).Returns(true);

        _sut.DeleteRegistry(Reg(valueName: null, kind: RegistryValueKind.None), Path).Should().BeTrue();

        _reg.Verify(r => r.DeleteKey(Path), Times.Once);
        _reg.Verify(r => r.DeleteValue(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void EnsureRegistryKey_CreatesKey()
    {
        _reg.Setup(r => r.CreateKey(Path)).Returns(true);

        _sut.EnsureRegistryKey(Reg(valueName: null), Path).Should().BeTrue();

        _reg.Verify(r => r.CreateKey(Path), Times.Once);
    }

    [Fact]
    public void UnlockKey_DelegatesToUnlockRegistryKey()
    {
        _reg.Setup(r => r.UnlockRegistryKey(Path)).Returns(true);

        _sut.UnlockKey(Reg(), Path).Should().BeTrue();

        _reg.Verify(r => r.UnlockRegistryKey(Path), Times.Once);
    }

    [Fact]
    public void LockKey_DelegatesToLockRegistryKey()
    {
        _reg.Setup(r => r.LockRegistryKey(Path)).Returns(true);

        _sut.LockKey(Reg(), Path).Should().BeTrue();

        _reg.Verify(r => r.LockRegistryKey(Path), Times.Once);
    }

    // --- Binary surgical edits: CreateKey-first, then Modify ---

    [Fact]
    public void SetRegistryBit_CreatesKeyThenModifiesBit()
    {
        _reg.Setup(r => r.CreateKey(Path)).Returns(true);
        _reg.Setup(r => r.ModifyBinaryBit(Path, ValueName, 4, 0x08, true)).Returns(true);

        _sut.SetRegistryBit(Reg(), Path, 4, 0x08, true).Should().BeTrue();

        _reg.Verify(r => r.CreateKey(Path), Times.Once);
        _reg.Verify(r => r.ModifyBinaryBit(Path, ValueName, 4, 0x08, true), Times.Once);
    }

    [Fact]
    public void SetRegistryBit_WhenCreateKeyFails_DoesNotModifyAndReturnsFalse()
    {
        _reg.Setup(r => r.CreateKey(Path)).Returns(false);

        _sut.SetRegistryBit(Reg(), Path, 4, 0x08, true).Should().BeFalse();

        _reg.Verify(r => r.ModifyBinaryBit(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<byte>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public void SetRegistryByte_CreatesKeyThenModifiesByte()
    {
        _reg.Setup(r => r.CreateKey(Path)).Returns(true);
        _reg.Setup(r => r.ModifyBinaryByte(Path, ValueName, 2, 0x5A)).Returns(true);

        _sut.SetRegistryByte(Reg(), Path, 2, 0x5A).Should().BeTrue();

        _reg.Verify(r => r.CreateKey(Path), Times.Once);
        _reg.Verify(r => r.ModifyBinaryByte(Path, ValueName, 2, 0x5A), Times.Once);
    }

    [Fact]
    public void SetRegistryByte_WhenCreateKeyFails_DoesNotModifyAndReturnsFalse()
    {
        _reg.Setup(r => r.CreateKey(Path)).Returns(false);

        _sut.SetRegistryByte(Reg(), Path, 2, 0x5A).Should().BeFalse();

        _reg.Verify(r => r.ModifyBinaryByte(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<byte>()), Times.Never);
    }

    [Fact]
    public void SetRegistryComposite_DelegatesToSetCompositeSubValue()
    {
        _reg.Setup(r => r.SetCompositeSubValue(Path, ValueName, "SubKey", "1")).Returns(true);

        _sut.SetRegistryComposite(Reg(), Path, "SubKey", "1").Should().BeTrue();

        _reg.Verify(r => r.SetCompositeSubValue(Path, ValueName, "SubKey", "1"), Times.Once);
    }

    // --- Per-sub-key (per-NIC / per-monitor): live enumeration, write/delete under each ---

    [Fact]
    public void WriteRegistryPerSubkey_EnumeratesAndWritesUnderEachSubkey()
    {
        _reg.Setup(r => r.GetSubKeyNames(Path)).Returns(new[] { "if1", "if2" });
        _reg.Setup(r => r.CreateKey(It.IsAny<string>())).Returns(true);
        _reg.Setup(r => r.SetValue(It.IsAny<string>(), ValueName, 1, RegistryValueKind.DWord)).Returns(true);

        _sut.WriteRegistryPerSubkey(Reg(), Path, 1).Should().BeTrue();

        _reg.Verify(r => r.SetValue($@"{Path}\if1", ValueName, 1, RegistryValueKind.DWord), Times.Once);
        _reg.Verify(r => r.SetValue($@"{Path}\if2", ValueName, 1, RegistryValueKind.DWord), Times.Once);
    }

    [Fact]
    public void WriteRegistryPerSubkey_WhenNoSubkeys_ReturnsFalse()
    {
        _reg.Setup(r => r.GetSubKeyNames(Path)).Returns(System.Array.Empty<string>());

        _sut.WriteRegistryPerSubkey(Reg(), Path, 1).Should().BeFalse();

        _reg.Verify(r => r.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Never);
    }

    [Fact]
    public void WriteRegistryPerSubkey_WhenOneSubkeyFails_ReturnsFalseButWritesAll()
    {
        _reg.Setup(r => r.GetSubKeyNames(Path)).Returns(new[] { "if1", "if2" });
        _reg.Setup(r => r.CreateKey(It.IsAny<string>())).Returns(true);
        _reg.Setup(r => r.SetValue($@"{Path}\if1", ValueName, 1, RegistryValueKind.DWord)).Returns(true);
        _reg.Setup(r => r.SetValue($@"{Path}\if2", ValueName, 1, RegistryValueKind.DWord)).Returns(false);

        _sut.WriteRegistryPerSubkey(Reg(), Path, 1).Should().BeFalse();

        _reg.Verify(r => r.SetValue($@"{Path}\if1", ValueName, 1, RegistryValueKind.DWord), Times.Once);
        _reg.Verify(r => r.SetValue($@"{Path}\if2", ValueName, 1, RegistryValueKind.DWord), Times.Once);
    }

    [Fact]
    public void DeleteRegistryPerSubkey_EnumeratesAndDeletesUnderEachSubkey()
    {
        _reg.Setup(r => r.GetSubKeyNames(Path)).Returns(new[] { "if1", "if2" });
        _reg.Setup(r => r.DeleteValue(It.IsAny<string>(), ValueName)).Returns(true);

        _sut.DeleteRegistryPerSubkey(Reg(), Path).Should().BeTrue();

        _reg.Verify(r => r.DeleteValue($@"{Path}\if1", ValueName), Times.Once);
        _reg.Verify(r => r.DeleteValue($@"{Path}\if2", ValueName), Times.Once);
    }

    [Fact]
    public void DeleteRegistryPerSubkey_WhenNoSubkeys_ReturnsFalse()
    {
        _reg.Setup(r => r.GetSubKeyNames(Path)).Returns(System.Array.Empty<string>());

        _sut.DeleteRegistryPerSubkey(Reg(), Path).Should().BeFalse();

        _reg.Verify(r => r.DeleteValue(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // --- Scheduled task: enable/disable, result.Success passthrough ---

    [Fact]
    public void SetTask_WhenEnabled_EnablesTask()
    {
        _tasks.Setup(t => t.SetTaskEnabled(@"\MS\Win\Foo", true)).Returns(OperationResult.Succeeded());

        _sut.SetTask(new TaskTarget("key", @"\MS\Win\Foo"), enabled: true).Should().BeTrue();

        _tasks.Verify(t => t.SetTaskEnabled(@"\MS\Win\Foo", true), Times.Once);
        _tasks.Verify(t => t.SetTaskEnabled(It.IsAny<string>(), false), Times.Never);
    }

    [Fact]
    public void SetTask_WhenDisabled_DisablesTask()
    {
        _tasks.Setup(t => t.SetTaskEnabled(@"\MS\Win\Foo", false)).Returns(OperationResult.Succeeded());

        _sut.SetTask(new TaskTarget("key", @"\MS\Win\Foo"), enabled: false).Should().BeTrue();

        _tasks.Verify(t => t.SetTaskEnabled(@"\MS\Win\Foo", false), Times.Once);
        _tasks.Verify(t => t.SetTaskEnabled(It.IsAny<string>(), true), Times.Never);
    }

    [Fact]
    public void SetTask_WhenServiceFails_ReturnsFalse()
    {
        _tasks.Setup(t => t.SetTaskEnabled(@"\MS\Win\Foo", true)).Returns(OperationResult.Failed("nope"));

        _sut.SetTask(new TaskTarget("key", @"\MS\Win\Foo"), enabled: true).Should().BeFalse();
    }

    // --- RunEffect: dispatch each effect to the right service (NativePowerEffect calls the static PowerProf
    //     P/Invoke directly, so it is review + apply-smoke gated, not unit-tested here). ---

    // Script and .reg effects launch a process, so ApplyExecutor defers them to IAsyncEffectRunner and
    // they must never reach this synchronous writer. If one does, that is a routing bug: it fails loudly
    // rather than falling through to the permissive unknown-effect default.

    [Fact]
    public void RunEffect_Script_IsRejected_BecauseItShouldHaveBeenDeferred()
    {
        _sut.RunEffect(new ScriptEffect("Write-Host hi", RunContext.System)).Should().BeFalse();
    }

    [Fact]
    public void RunEffect_RegContent_IsRejected_BecauseItShouldHaveBeenDeferred()
    {
        _sut.RunEffect(new RegContentEffect("REGCONTENT")).Should().BeFalse();
    }

    [Fact]
    public void RunEffect_RegistryWrite_CreatesKeyThenSetsValue()
    {
        _reg.Setup(r => r.CreateKey(Path)).Returns(true);
        _reg.Setup(r => r.SetValue(Path, ValueName, 7, RegistryValueKind.DWord)).Returns(true);

        _sut.RunEffect(new RegistryWriteEffect(Path, ValueName, RegistryValueKind.DWord, 7)).Should().BeTrue();

        _reg.Verify(r => r.CreateKey(Path), Times.Once);
        _reg.Verify(r => r.SetValue(Path, ValueName, 7, RegistryValueKind.DWord), Times.Once);
    }

    [Fact]
    public void RunEffect_RegistryWrite_WhenCreateKeyFails_ReturnsFalse()
    {
        _reg.Setup(r => r.CreateKey(Path)).Returns(false);

        _sut.RunEffect(new RegistryWriteEffect(Path, ValueName, RegistryValueKind.DWord, 7)).Should().BeFalse();

        _reg.Verify(r => r.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Never);
    }

    [Theory]
    [InlineData(PowerContext.AC)]
    [InlineData(PowerContext.DC)]
    public void WritePowerCfgValue_DelegatesToApplierPerContext(PowerContext context)
    {
        var target = new PowerCfgTarget("key", "381b4222-f694-41f0-9685-ff5bb260df2e", "29f6c1db-86da-48c5-9fdb-f2b67b1f44da", PowerModeSupport.Both);
        _powerCfg.Setup(p => p.WriteValueIndex(target, context, 3)).Returns(true);

        _sut.WritePowerCfgValue(target, context, 3).Should().BeTrue();

        _powerCfg.Verify(p => p.WriteValueIndex(target, context, 3), Times.Once);
    }

    [Fact]
    public void WritePowerCfgValue_PassesThroughFailure()
    {
        var target = new PowerCfgTarget("key", "381b4222-f694-41f0-9685-ff5bb260df2e", "29f6c1db-86da-48c5-9fdb-f2b67b1f44da", PowerModeSupport.Both);
        _powerCfg.Setup(p => p.WriteValueIndex(target, PowerContext.AC, 1)).Returns(false);

        _sut.WritePowerCfgValue(target, PowerContext.AC, 1).Should().BeFalse();
    }
}
