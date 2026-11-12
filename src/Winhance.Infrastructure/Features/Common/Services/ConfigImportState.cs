using System.Collections.Concurrent;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class ConfigImportState : IConfigImportState
{
    // AsyncLocal, so only the import's own flow (and the Task.Run work it starts) sees the scope.
    private readonly AsyncLocal<Scope?> _current = new();
    private int _running;

    public bool IsActive => _current.Value is not null;
    public bool IsAnyImportRunning => Volatile.Read(ref _running) > 0;
    public event EventHandler? AnyImportRunningChanged;
    public string? SourceName { get; set; }

    public bool ImportSuppliesPowerValues
    {
        get => _current.Value?.SuppliesPowerValues ?? false;
        set
        {
            if (_current.Value is { } scope)
                scope.SuppliesPowerValues = value;
        }
    }

    public IDisposable Begin()
    {
        var scope = new Scope(this, _current.Value);
        _current.Value = scope;
        if (Interlocked.Increment(ref _running) == 1)
            AnyImportRunningChanged?.Invoke(this, EventArgs.Empty);
        return scope;
    }

    private void End()
    {
        if (Interlocked.Decrement(ref _running) == 0)
            AnyImportRunningChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReportNotApplied(string message) => _current.Value?.NotApplied.Enqueue(message);

    public IReadOnlyList<string> TakeNotApplied()
    {
        var taken = new List<string>();
        if (_current.Value is not { } scope)
            return taken;
        while (scope.NotApplied.TryDequeue(out var message))
            taken.Add(message);
        return taken;
    }

    private sealed class Scope(ConfigImportState owner, Scope? previous) : IDisposable
    {
        public ConcurrentQueue<string> NotApplied { get; } = new();
        public bool SuppliesPowerValues { get; set; }
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;
            owner._current.Value = previous;
            owner.End();
        }
    }
}
