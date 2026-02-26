using System;
using System.Threading;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Provides functionality for tracking progress of multi-script operations.
/// </summary>
public interface IMultiScriptProgressService
{
    /// <summary>
    /// Starts a multi-script task with the specified script names.
    /// Each script gets its own progress slot.
    /// </summary>
    CancellationTokenSource StartMultiScriptTask(string[] scriptNames);

    /// <summary>
    /// Creates a progress reporter for a specific script slot.
    /// Must be called on the UI thread so Progress&lt;T&gt; captures the SynchronizationContext.
    /// </summary>
    IProgress<TaskProgressDetail> CreateScriptProgress(int slotIndex);

    /// <summary>
    /// Completes the multi-script task and resets slot state.
    /// </summary>
    void CompleteMultiScriptTask();
}
