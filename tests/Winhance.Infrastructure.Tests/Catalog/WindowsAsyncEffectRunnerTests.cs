using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Catalog;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

/// <summary>The two apply effects that launch a process. Split out of WindowsStateWriter so the
/// synchronous writer never blocks on real async I/O.</summary>
public class WindowsAsyncEffectRunnerTests
{
    private readonly Mock<IPowerShellRunner> _powerShell = new();
    private readonly Mock<IRegImportService> _regImport = new();
    private readonly Mock<ILogService> _log = new();
    private readonly WindowsAsyncEffectRunner _sut;

    public WindowsAsyncEffectRunnerTests()
    {
        _sut = new WindowsAsyncEffectRunner(_powerShell.Object, _regImport.Object, _log.Object);
    }

    [Fact]
    public async Task RunAsync_Script_RunsInMemory()
    {
        _powerShell
            .Setup(p => p.RunScriptInMemoryAsync("Write-Host hi", It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        (await _sut.RunAsync(new ScriptEffect("Write-Host hi", RunContext.System))).Should().BeTrue();

        _powerShell.Verify(
            p => p.RunScriptInMemoryAsync("Write-Host hi", It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_RegContent_Imports()
    {
        _regImport.Setup(r => r.RunRegImportAsync("REGCONTENT")).Returns(Task.CompletedTask);

        (await _sut.RunAsync(new RegContentEffect("REGCONTENT"))).Should().BeTrue();

        _regImport.Verify(r => r.RunRegImportAsync("REGCONTENT"), Times.Once);
    }

    [Fact]
    public async Task RunAsync_WhenTheEffectThrows_ReportsFailureInsteadOfPropagating()
    {
        // Apply is best-effort: one failed effect must not abort the rest of the operation.
        _regImport.Setup(r => r.RunRegImportAsync(It.IsAny<string>())).ThrowsAsync(new InvalidOperationException("reg.exe missing"));

        (await _sut.RunAsync(new RegContentEffect("REGCONTENT"))).Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_UnroutedEffectKind_FailsLoudlyRatherThanSucceedingSilently()
    {
        // Only effects whose Effect.IsAsyncIo is true reach this runner. If the two ever drift apart,
        // a permissive default would silently drop the effect - so this returns false instead.
        (await _sut.RunAsync(new RegistryWriteEffect(@"HKLM\A", "V", RegistryValueKind.DWord, 1)))
            .Should().BeFalse();
    }

    [Fact]
    public async Task RunAllAsync_RunsInOrder_AndCollectsOnlyTheFailures()
    {
        var order = new List<string>();
        _powerShell
            .Setup(p => p.RunScriptInMemoryAsync(It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .Callback((string s, IProgress<TaskProgressDetail>? _, CancellationToken _) => order.Add(s))
            .ReturnsAsync(string.Empty);
        _regImport
            .Setup(r => r.RunRegImportAsync(It.IsAny<string>()))
            .Callback((string c) => order.Add(c))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var failures = await _sut.RunAllAsync(new Effect[]
        {
            new ScriptEffect("first", RunContext.System),
            new RegContentEffect("second"),
            new ScriptEffect("third", RunContext.System),
        });

        order.Should().Equal("first", "second", "third");
        failures.Should().ContainSingle().Which.Should().Contain(nameof(RegContentEffect));
    }

    [Fact]
    public async Task RunAllAsync_NoEffects_IsANoOp()
    {
        (await _sut.RunAllAsync(Array.Empty<Effect>())).Should().BeEmpty();

        _powerShell.VerifyNoOtherCalls();
        _regImport.VerifyNoOtherCalls();
    }
}
