using FluentAssertions;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ConfigImportStateTests
{
    private readonly ConfigImportState _state = new();

    [Fact]
    public async Task IsActive_InsideTheScope_IsTrueForTheImportAndTheWorkItStarts()
    {
        using (_state.Begin())
        {
            _state.IsActive.Should().BeTrue();
            (await Task.Run(() => _state.IsActive)).Should().BeTrue();
        }

        _state.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task IsActive_OnAFlowThatDidNotBegin_IsFalse()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var outsider = Task.Run(async () =>
        {
            await gate.Task;
            return _state.IsActive;
        });

        using (_state.Begin())
        {
            gate.SetResult();
            (await outsider).Should().BeFalse();
        }
    }

    [Fact]
    public async Task ImportSuppliesPowerValues_SetBySectionWork_IsSeenByTheWholeImport()
    {
        using (_state.Begin())
        {
            await Task.Run(() => _state.ImportSuppliesPowerValues = true);

            _state.ImportSuppliesPowerValues.Should().BeTrue();
        }

        using (_state.Begin())
            _state.ImportSuppliesPowerValues.Should().BeFalse();
    }

    [Fact]
    public async Task IsAnyImportRunning_IsSeenFromAFlowThatDidNotBegin()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var outsider = Task.Run(async () =>
        {
            await gate.Task;
            return (_state.IsActive, _state.IsAnyImportRunning);
        });

        using (_state.Begin())
        {
            gate.SetResult();
            (await outsider).Should().Be((false, true));
        }

        _state.IsAnyImportRunning.Should().BeFalse();
    }

    [Fact]
    public async Task IsAnyImportRunning_StaysOnUntilTheLastNestedOrParallelScopeEnds()
    {
        var changes = 0;
        _state.AnyImportRunningChanged += (_, _) => changes++;

        var parallel = await Task.Run(() => _state.Begin());
        var outer = _state.Begin();
        var inner = _state.Begin();

        inner.Dispose();
        outer.Dispose();
        _state.IsAnyImportRunning.Should().BeTrue();

        await Task.Run(() => parallel.Dispose());
        _state.IsAnyImportRunning.Should().BeFalse();
        changes.Should().Be(2, "only the first begin and the last end flip the state");
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotEndAnotherImport()
    {
        var first = _state.Begin();
        using var second = _state.Begin();

        first.Dispose();
        first.Dispose();

        _state.IsAnyImportRunning.Should().BeTrue();
    }

    [Fact]
    public void TakeNotApplied_ReturnsOnlyWhatThisImportReported()
    {
        _state.ReportNotApplied("outside any import");

        using (_state.Begin())
        {
            _state.ReportNotApplied("Time zone");

            _state.TakeNotApplied().Should().Equal("Time zone");
            _state.TakeNotApplied().Should().BeEmpty();
        }
    }
}
