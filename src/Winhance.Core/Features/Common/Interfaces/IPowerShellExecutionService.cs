using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IPowerShellExecutionService
    {
        Task<string> ExecuteScriptAsync(
            string script,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);

        Task<bool> ExecuteScriptVisibleAsync(
            string script,
            string windowTitle = "Winhance PowerShell Task");

        Task<string> ExecuteScriptFileAsync(
            string scriptPath,
            string arguments = "",
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);
    }
}