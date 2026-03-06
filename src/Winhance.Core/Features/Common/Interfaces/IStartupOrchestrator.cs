using System;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Orchestrates the application startup sequence (settings init, backup, migration, script updates).
/// </summary>
public interface IStartupOrchestrator
{
    /// <summary>
    /// Runs the full startup sequence (phases 1-5).
    /// </summary>
    /// <param name="statusProgress">Reports localization keys for loading overlay text.</param>
    /// <param name="detailedProgress">Reports detailed progress for the backup phase.</param>
    /// <returns>The startup result containing backup information.</returns>
    Task<StartupResult> RunStartupSequenceAsync(
        IProgress<string> statusProgress,
        IProgress<TaskProgressDetail> detailedProgress);
}
