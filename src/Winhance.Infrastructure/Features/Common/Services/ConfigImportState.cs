using System.Collections.Concurrent;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class ConfigImportState : IConfigImportState
{
    private readonly ConcurrentQueue<string> _notApplied = new();

    public bool IsActive { get; set; }
    public string? SourceName { get; set; }
    public bool ImportSuppliesPowerValues { get; set; }

    public void ReportNotApplied(string message) => _notApplied.Enqueue(message);

    public IReadOnlyList<string> TakeNotApplied()
    {
        var taken = new List<string>();
        while (_notApplied.TryDequeue(out var message))
            taken.Add(message);
        return taken;
    }
}
