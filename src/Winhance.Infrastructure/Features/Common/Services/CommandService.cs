using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Infrastructure.Features.Common.Services;

public class CommandService(ILogService logService) : ICommandService
{

    public async Task<(bool Success, string Output, string Error)> ExecuteCommandAsync(
        string command,
        bool requiresElevation = true)
    {
        try
        {
            logService.Log(LogLevel.Info, $"[CommandService] Executing command: {GetTruncatedCommand(command)}");
            
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c chcp 65001 && {command}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
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

            await process.WaitForExitAsync();

            var output = outputBuilder.ToString().TrimEnd();
            var error = errorBuilder.ToString().TrimEnd();

            logService.Log(LogLevel.Info, $"[CommandService] Process exit code: {process.ExitCode}, Output length: {output.Length}, Error length: {error.Length}");

            if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
            {
                logService.Log(LogLevel.Warning, $"[CommandService] Command failed with error: {error}");
                return (false, output, error);
            }

            return (true, output, string.Empty);
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Command execution failed: {GetTruncatedCommand(command)} - {ex.Message}");
            return (false, string.Empty, ex.Message);
        }
    }

    public async Task<(bool Success, string Message)> ApplyCommandSettingsAsync(
        IEnumerable<CommandSetting> settings,
        bool isEnabled)
    {
        if (settings == null || !settings.Any())
            return (true, "No command settings to apply.");

        var successCount = 0;
        var failureCount = 0;

        foreach (var setting in settings)
        {
            var commandToExecute = isEnabled ? setting.EnabledCommand : setting.DisabledCommand;

            if (string.IsNullOrWhiteSpace(commandToExecute))
                continue;

            var (success, output, error) = await ExecuteCommandAsync(commandToExecute, setting.RequiresElevation);

            if (success)
                successCount++;
            else
            {
                failureCount++;
                logService.Log(LogLevel.Error, $"Command failed for {setting.Id}: {error}");
            }
        }

        var overallSuccess = failureCount == 0;
        var message = $"Applied {successCount}/{successCount + failureCount} command settings successfully";

        return (overallSuccess, message);
    }

    public async Task<bool> IsCommandSettingEnabledAsync(CommandSetting setting)
    {
        try
        {
            if (setting.EnabledCommand.Contains("bcdedit"))
                return await IsBcdeditSettingEnabledAsync(setting);

            if (setting.EnabledCommand.Contains("schtasks"))
                return await IsSchedTaskEnabledAsync(setting);

            return false;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> IsBcdeditSettingEnabledAsync(CommandSetting setting)
    {
        string settingName = ExtractBcdeditSettingName(setting.EnabledCommand);
        string expectedValue = ExtractBcdeditSettingValue(setting.EnabledCommand);

        if (string.IsNullOrEmpty(settingName))
            return false;

        var (success, output, error) = await ExecuteCommandAsync("bcdedit /enum {current}");

        if (!success || string.IsNullOrEmpty(output))
            return false;

        bool settingExists = output.Contains(settingName, StringComparison.OrdinalIgnoreCase);
        if (setting.DisabledCommand.Contains("/deletevalue"))
        {
            if (settingExists)
            {
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var settingLine = lines.FirstOrDefault(l => l.Contains(settingName, StringComparison.OrdinalIgnoreCase));

                if (settingLine != null)
                {
                    var parts = settingLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        string currentValue = parts[parts.Length - 1].Trim().ToLowerInvariant();
                        expectedValue = expectedValue.ToLowerInvariant();
                        return currentValue == expectedValue;
                    }
                }
            }
            return false;
        }
        else if (setting.DisabledCommand.Contains("/set"))
        {
            string disabledValue = ExtractBcdeditSettingValue(setting.DisabledCommand);

            if (settingExists)
            {
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var settingLine = lines.FirstOrDefault(l => l.Contains(settingName, StringComparison.OrdinalIgnoreCase));

                if (settingLine != null)
                {
                    var parts = settingLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        string currentValue = parts[parts.Length - 1].Trim().ToLowerInvariant();
                        expectedValue = expectedValue.ToLowerInvariant();
                        disabledValue = disabledValue.ToLowerInvariant();
                        return currentValue == expectedValue && currentValue != disabledValue;
                    }
                }
            }
            return false;
        }
        return false;
    }

    private async Task<bool> IsSchedTaskEnabledAsync(CommandSetting setting)
    {
        var taskName = ExtractTaskNameFromCommand(setting.EnabledCommand);
        if (string.IsNullOrEmpty(taskName))
            return false;

        var queryCommand = $"schtasks /Query /TN \"{taskName}\" /FO LIST";
        var (success, output, _) = await ExecuteCommandAsync(queryCommand);

        if (!success || string.IsNullOrEmpty(output))
            return false;

        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var statusLine = lines.FirstOrDefault(l => l.Trim().StartsWith("Status:", StringComparison.OrdinalIgnoreCase));

        if (statusLine == null)
            return false;

        var status = statusLine.Split(':')[1].Trim();
        return status.Equals("Ready", StringComparison.OrdinalIgnoreCase);
    }

    private string ExtractTaskNameFromCommand(string command)
    {
        var tnIndex = command.IndexOf("/TN", StringComparison.OrdinalIgnoreCase);
        if (tnIndex == -1)
            return string.Empty;

        var afterTN = command.Substring(tnIndex + 3).Trim();
        var startQuote = afterTN.IndexOf('"');
        if (startQuote == -1)
            return string.Empty;

        var endQuote = afterTN.IndexOf('"', startQuote + 1);
        if (endQuote == -1)
            return string.Empty;

        return afterTN.Substring(startQuote + 1, endQuote - startQuote - 1);
    }

    private string ExtractBcdeditSettingName(string command)
    {
        if (command.Contains("/set "))
        {
            var parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
                return parts[2];
        }
        else if (command.Contains("/deletevalue "))
        {
            var parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
                return parts[2];
        }
        return string.Empty;
    }

    private string ExtractBcdeditSettingValue(string command)
    {
        if (command.Contains("/set "))
        {
            var parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4)
                return parts[3];
        }
        return string.Empty;
    }


    private string GetTruncatedCommand(string command)
    {
        if (command.StartsWith("powercfg /query"))
            return command;
        return command.Length > 80 ? $"{command.Substring(0, 77)}..." : command;
    }
}
