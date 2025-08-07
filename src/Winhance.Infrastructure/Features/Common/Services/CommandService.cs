using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;


namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Service for executing system commands related to optimizations.
    /// </summary>
    public class CommandService : ICommandService
    {
        private readonly ILogService _logService;
        private readonly IScriptPathDetectionService _scriptPathDetectionService;
        private readonly IPowerShellDetectionService _powerShellDetectionService;
        private readonly ISystemServices _systemServices;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandService"/> class.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        /// <param name="scriptPathDetectionService">The script path detection service.</param>
        /// <param name="powerShellDetectionService">The PowerShell detection service.</param>
        /// <param name="systemServices">The system services.</param>
        public CommandService(ILogService logService, IScriptPathDetectionService scriptPathDetectionService, IPowerShellDetectionService powerShellDetectionService, ISystemServices systemServices)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _scriptPathDetectionService = scriptPathDetectionService ?? throw new ArgumentNullException(nameof(scriptPathDetectionService));
            _powerShellDetectionService = powerShellDetectionService ?? throw new ArgumentNullException(nameof(powerShellDetectionService));
            _systemServices = systemServices ?? throw new ArgumentNullException(nameof(systemServices));
        }

        /// <inheritdoc/>
        public async Task<(bool Success, string Output, string Error)> ExecuteCommandAsync(
            string command,
            bool requiresElevation = true
        )
        {
            try
            {
                _logService.LogInformation($"Executing command: {command}");

                // Create a PowerShell instance
                var powerShell = PowerShell.Create();

                // Add the command to execute
                powerShell.AddScript(command);

                // Execute the command
                var results = await Task.Run(() => powerShell.Invoke());

                // Process the results
                var output = string.Join(Environment.NewLine, results.Select(r => r.ToString()));
                var error = string.Join(
                    Environment.NewLine,
                    powerShell.Streams.Error.ReadAll().Select(e => e.ToString())
                );

                // Log the results
                if (string.IsNullOrEmpty(error))
                {
                    _logService.LogInformation($"Command executed successfully: {command}");
                    _logService.LogInformation($"Command output: {output}");
                    return (true, output, string.Empty);
                }
                else
                {
                    _logService.LogWarning($"Command execution failed: {command}");
                    _logService.LogWarning($"Error: {error}");
                    return (false, output, error);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Exception executing command: {command}");
                _logService.LogError($"Exception: {ex}");
                return (false, string.Empty, ex.ToString());
            }
        }

        /// <inheritdoc/>
        public async Task<(bool Success, string Message)> ApplyCommandSettingsAsync(
            IEnumerable<CommandSetting> settings,
            bool isEnabled
        )
        {
            if (settings == null || !settings.Any())
            {
                return (true, "No command settings to apply.");
            }

            var successCount = 0;
            var failureCount = 0;
            var messages = new List<string>();

            foreach (var setting in settings)
            {
                var commandToExecute = isEnabled ? setting.EnabledCommand : setting.DisabledCommand;

                if (string.IsNullOrWhiteSpace(commandToExecute))
                {
                    _logService.LogWarning($"Empty command for setting: {setting.Id}");
                    continue;
                }

                var (success, output, error) = await ExecuteCommandAsync(
                    commandToExecute,
                    setting.RequiresElevation
                );

                if (success)
                {
                    successCount++;
                    messages.Add($"Successfully applied command setting: {setting.Id}");
                }
                else
                {
                    failureCount++;
                    messages.Add($"Failed to apply command setting: {setting.Id}. Error: {error}");
                }
            }

            var overallSuccess = failureCount == 0;
            var message =
                $"Applied {successCount} command settings successfully, {failureCount} failed.";

            if (messages.Any())
            {
                message += Environment.NewLine + string.Join(Environment.NewLine, messages);
            }

            return (overallSuccess, message);
        }

        /// <inheritdoc/>
        public async Task<bool> IsCommandSettingEnabledAsync(CommandSetting setting)
        {
            try
            {
                _logService.LogInformation($"Checking state for command setting: {setting.Id}");

                // Check if this is a bcdedit command
                if (setting.EnabledCommand.Contains("bcdedit"))
                {
                    return await IsBcdeditSettingEnabledAsync(setting);
                }

                // For other types of commands, implement specific checking logic here
                // For now, return false as a default for unhandled command types
                _logService.LogWarning(
                    $"No state checking implementation for command type: {setting.Id}"
                );
                return false;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error checking command setting state: {setting.Id}", ex);
                return false;
            }
        }

        /// <summary>
        /// Checks if a bcdedit setting is in its enabled state.
        /// </summary>
        /// <param name="setting">The command setting to check.</param>
        /// <returns>True if the setting is in its enabled state, false otherwise.</returns>
        private async Task<bool> IsBcdeditSettingEnabledAsync(CommandSetting setting)
        {
            // Extract the setting name and value from the command
            string settingName = ExtractBcdeditSettingName(setting.EnabledCommand);
            string expectedValue = ExtractBcdeditSettingValue(setting.EnabledCommand);

            if (string.IsNullOrEmpty(settingName))
            {
                _logService.LogWarning(
                    $"Could not extract setting name from bcdedit command: {setting.EnabledCommand}"
                );
                return false;
            }

            // Query the current boot configuration
            var (success, output, error) = await ExecuteCommandAsync("bcdedit /enum {current}");

            if (!success || string.IsNullOrEmpty(output))
            {
                _logService.LogWarning($"Failed to query boot configuration: {error}");
                return false;
            }

            // Parse the output to find the setting
            bool settingExists = output.Contains(settingName, StringComparison.OrdinalIgnoreCase);

            // For settings that should be deleted when disabled
            if (setting.DisabledCommand.Contains("/deletevalue"))
            {
                // If the setting exists, check if it has the expected value
                if (settingExists)
                {
                    // Find the line containing the setting
                    var lines = output.Split(
                        new[] { '\r', '\n' },
                        StringSplitOptions.RemoveEmptyEntries
                    );
                    var settingLine = lines.FirstOrDefault(l =>
                        l.Contains(settingName, StringComparison.OrdinalIgnoreCase)
                    );

                    if (settingLine != null)
                    {
                        // Extract the current value
                        var parts = settingLine.Split(
                            new[] { ' ' },
                            StringSplitOptions.RemoveEmptyEntries
                        );
                        if (parts.Length >= 2)
                        {
                            string currentValue = parts[parts.Length - 1].Trim().ToLowerInvariant();
                            expectedValue = expectedValue.ToLowerInvariant();

                            _logService.LogInformation(
                                $"Found bcdedit setting {settingName} with value {currentValue}, expected {expectedValue}"
                            );
                            return currentValue == expectedValue;
                        }
                    }
                }

                // If the setting doesn't exist or we couldn't parse the value, it's not in the enabled state
                return false;
            }
            // For settings that should be set to a different value when disabled
            else if (setting.DisabledCommand.Contains("/set"))
            {
                string disabledValue = ExtractBcdeditSettingValue(setting.DisabledCommand);

                // Find the line containing the setting
                if (settingExists)
                {
                    var lines = output.Split(
                        new[] { '\r', '\n' },
                        StringSplitOptions.RemoveEmptyEntries
                    );
                    var settingLine = lines.FirstOrDefault(l =>
                        l.Contains(settingName, StringComparison.OrdinalIgnoreCase)
                    );

                    if (settingLine != null)
                    {
                        // Extract the current value
                        var parts = settingLine.Split(
                            new[] { ' ' },
                            StringSplitOptions.RemoveEmptyEntries
                        );
                        if (parts.Length >= 2)
                        {
                            string currentValue = parts[parts.Length - 1].Trim().ToLowerInvariant();
                            expectedValue = expectedValue.ToLowerInvariant();
                            disabledValue = disabledValue.ToLowerInvariant();

                            _logService.LogInformation(
                                $"Found bcdedit setting {settingName} with value {currentValue}, expected {expectedValue} for enabled state"
                            );
                            return currentValue == expectedValue && currentValue != disabledValue;
                        }
                    }
                }

                return false;
            }

            // Default case
            return false;
        }

        /// <summary>
        /// Extracts the setting name from a bcdedit command.
        /// </summary>
        /// <param name="command">The bcdedit command.</param>
        /// <returns>The setting name.</returns>
        private string ExtractBcdeditSettingName(string command)
        {
            // Handle /set command
            if (command.Contains("/set "))
            {
                var parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    return parts[2]; // The setting name is the third part in "bcdedit /set settingname value"
                }
            }
            // Handle /deletevalue command
            else if (command.Contains("/deletevalue "))
            {
                var parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    return parts[2]; // The setting name is the third part in "bcdedit /deletevalue settingname"
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Extracts the setting value from a bcdedit command.
        /// </summary>
        /// <param name="command">The bcdedit command.</param>
        /// <returns>The setting value.</returns>
        private string ExtractBcdeditSettingValue(string command)
        {
            // Only handle /set command as /deletevalue doesn't have a value
            if (command.Contains("/set "))
            {
                var parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    return parts[3]; // The value is the fourth part in "bcdedit /set settingname value"
                }
            }

            return string.Empty;
        }
    }
}
