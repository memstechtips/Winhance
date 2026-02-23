namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Tracks whether a config import operation is currently active.
    /// Services check this to defer expensive side effects (process restarts, explorer kills)
    /// until the import completes.
    /// </summary>
    public interface IConfigImportState
    {
        bool IsActive { get; set; }
    }
}
