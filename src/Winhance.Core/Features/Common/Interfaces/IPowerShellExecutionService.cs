using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Provides functionality for executing PowerShell scripts.
    /// </summary>
    public interface IPowerShellExecutionService
    {
        /// <summary>
        /// Executes a PowerShell script.
        /// </summary>
        /// <param name="script">The script to execute.</param>
        /// <param name="progress">The progress reporter.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The output of the script.</returns>
        Task<string> ExecuteScriptAsync(
            string script,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a PowerShell script file.
        /// </summary>
        /// <param name="scriptPath">The path to the script file.</param>
        /// <param name="arguments">The arguments to pass to the script.</param>
        /// <param name="progress">The progress reporter.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The output of the script.</returns>
        Task<string> ExecuteScriptFileAsync(
            string scriptPath,
            string arguments = "",
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);
    }
}