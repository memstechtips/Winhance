using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

public class PowerShellExecutionService(ILogService logService) : IPowerShellExecutionService
{
    private const string PowerShellPath = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";

    public async Task<string> ExecuteScriptAsync(
        string script,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(script))
            throw new ArgumentException("Script cannot be null or empty.", nameof(script));

        var startInfo = new ProcessStartInfo
        {
            FileName = PowerShellPath,
            Arguments = $"-ExecutionPolicy Bypass -Command \"{EscapeScript(script)}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using var process = new Process { StartInfo = startInfo };
        
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    logService.Log(Core.Features.Common.Enums.LogLevel.Info, "Cancellation requested - killing PowerShell process");
                    process.Kill();
                }
            }
            catch (Exception ex)
            {
                logService.Log(Core.Features.Common.Enums.LogLevel.Warning, $"Error killing PowerShell process: {ex.Message}");
            }
        });

        await process.WaitForExitAsync(cancellationToken);

        var output = outputBuilder.ToString().TrimEnd();
        var error = errorBuilder.ToString().TrimEnd();

        if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
        {
            var errorDetails = $"PowerShell execution failed:\n" +
                              $"Exit Code: {process.ExitCode}\n" +
                              $"Error Output: {error}\n" +
                              $"Standard Output: {output}";

            logService.Log(Core.Features.Common.Enums.LogLevel.Error, errorDetails);
            throw new InvalidOperationException(errorDetails);
        }

        return output;
    }

    public Task<bool> ExecuteScriptVisibleAsync(string script, string windowTitle = "Winhance PowerShell Task - Administrator")
    {
        if (string.IsNullOrEmpty(script))
            throw new ArgumentException("Script cannot be null or empty.", nameof(script));

        var windowSetupCommands = $"$Host.UI.RawUI.WindowTitle='{EscapeScript(windowTitle)}';$Host.UI.RawUI.BackgroundColor='Black';$Host.PrivateData.ProgressBackgroundColor='Black';$Host.PrivateData.ProgressForegroundColor='White';Clear-Host;";

        var startInfo = new ProcessStartInfo
        {
            FileName = PowerShellPath,
            Arguments = $"-ExecutionPolicy Bypass -Command \"{windowSetupCommands} & {{ {EscapeScript(script)} }}\"",
            UseShellExecute = true,
            CreateNoWindow = false,
            WindowStyle = ProcessWindowStyle.Normal
        };

        try
        {
            var process = Process.Start(startInfo);
            return Task.FromResult(process != null);
        }
        catch (Exception ex)
        {
            logService.Log(Core.Features.Common.Enums.LogLevel.Error, $"Failed to launch visible PowerShell: {ex.Message}");
            return Task.FromResult(false);
        }
    }

    private static string EscapeScript(string script)
    {
        return script.Replace("\"", "'");
    }

    public async Task<string> ExecuteScriptFileAsync(
        string scriptPath,
        string arguments = "",
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(scriptPath))
            throw new ArgumentException("Script path cannot be null or empty.", nameof(scriptPath));

        if (!File.Exists(scriptPath))
            throw new FileNotFoundException($"PowerShell script file not found: {scriptPath}");

        var startInfo = new ProcessStartInfo
        {
            FileName = PowerShellPath,
            Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" {arguments}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using var process = new Process { StartInfo = startInfo };
        
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    logService.Log(Core.Features.Common.Enums.LogLevel.Info, "Cancellation requested - killing PowerShell process");
                    process.Kill();
                }
            }
            catch (Exception ex)
            {
                logService.Log(Core.Features.Common.Enums.LogLevel.Warning, $"Error killing PowerShell process: {ex.Message}");
            }
        });

        await process.WaitForExitAsync(cancellationToken);

        var output = outputBuilder.ToString().TrimEnd();
        var error = errorBuilder.ToString().TrimEnd();

        if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
        {
            var errorDetails = $"PowerShell script execution failed:\n" +
                              $"Script Path: {scriptPath}\n" +
                              $"Arguments: {arguments}\n" +
                              $"Exit Code: {process.ExitCode}\n" +
                              $"Error Output: {error}\n" +
                              $"Standard Output: {output}";

            logService.Log(Core.Features.Common.Enums.LogLevel.Error, errorDetails);
            throw new InvalidOperationException(errorDetails);
        }

        return output;
    }
}