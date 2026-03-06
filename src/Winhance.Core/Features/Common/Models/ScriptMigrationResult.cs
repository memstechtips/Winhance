namespace Winhance.Core.Features.Common.Models;

public record ScriptMigrationResult
{
    public bool MigrationPerformed { get; init; }
    public int ScriptsRenamed { get; init; }
    public int TasksDeleted { get; init; }
    public bool Success { get; init; }
}
