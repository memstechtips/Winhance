namespace Winhance.Core.Features.Common.Models
{
    public class ScriptMigrationResult
    {
        public bool MigrationPerformed { get; set; }
        public int ScriptsRenamed { get; set; }
        public int TasksDeleted { get; set; }
        public bool Success { get; set; }
    }
}
