using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IDismProcessRunner
{
    Task<(int ExitCode, string Output)> RunProcessWithProgressAsync(
        string fileName,
        string arguments,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken);

    Task<bool> CheckDiskSpaceAsync(string path, long requiredBytes, string operationName);
}
