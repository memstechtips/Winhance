using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class DismProcessRunner : IDismProcessRunner
    {
        private readonly IProcessExecutor _processExecutor;
        private readonly ILogService _logService;
        private readonly IFileSystemService _fileSystemService;

        private static readonly Regex ProgressRegex =
            new(@"(\d+\.?\d*)\s*%", RegexOptions.Compiled);

        public DismProcessRunner(
            IProcessExecutor processExecutor,
            ILogService logService,
            IFileSystemService fileSystemService)
        {
            _processExecutor = processExecutor;
            _logService = logService;
            _fileSystemService = fileSystemService;
        }

        public async Task<(int ExitCode, string Output)> RunProcessWithProgressAsync(
            string fileName,
            string arguments,
            IProgress<TaskProgressDetail>? progress,
            CancellationToken cancellationToken)
        {
            var output = new StringBuilder();

            var result = await _processExecutor.ExecuteWithStreamingAsync(
                fileName,
                arguments,
                onOutputLine: line =>
                {
                    output.AppendLine(line);
                    var match = ProgressRegex.Match(line);
                    if (match.Success && double.TryParse(match.Groups[1].Value, out var pct))
                    {
                        progress?.Report(new TaskProgressDetail
                        {
                            TerminalOutput = line,
                            Progress = pct
                        });
                    }
                    else
                    {
                        progress?.Report(new TaskProgressDetail { TerminalOutput = line });
                    }
                },
                onErrorLine: line =>
                {
                    output.AppendLine(line);
                    progress?.Report(new TaskProgressDetail { TerminalOutput = line });
                },
                ct: cancellationToken).ConfigureAwait(false);

            return (result.ExitCode, output.ToString());
        }

        public async Task<bool> CheckDiskSpaceAsync(string path, long requiredBytes, string operationName)
        {
            try
            {
                var drive = new DriveInfo(_fileSystemService.GetPathRoot(path)!);
                var availableBytes = drive.AvailableFreeSpace;

                var availableGB = availableBytes / (1024.0 * 1024 * 1024);
                var requiredGB = requiredBytes / (1024.0 * 1024 * 1024);

                _logService.LogInformation(
                    $"Disk space check for {operationName}: " +
                    $"Required: {requiredGB:F2} GB, Available: {availableGB:F2} GB on {drive.Name}"
                );

                if (availableBytes < requiredBytes)
                {
                    _logService.LogError(
                        $"Insufficient disk space for {operationName}. " +
                        $"Required: {requiredGB:F2} GB, Available: {availableGB:F2} GB"
                    );

                    throw new InsufficientDiskSpaceException(
                        drive.Name,
                        requiredGB,
                        availableGB,
                        operationName
                    );
                }

                return true;
            }
            catch (InsufficientDiskSpaceException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Could not check disk space: {ex.Message}");
                return true;
            }
        }
    }
}
