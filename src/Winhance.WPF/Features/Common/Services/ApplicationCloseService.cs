using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Views;

namespace Winhance.WPF.Features.Common.Services
{
    public class ApplicationCloseService : IApplicationCloseService
    {
        private readonly ILogService _logService;
        private readonly ITaskProgressService _taskProgressService;
        private readonly string _preferencesFilePath;

        public ApplicationCloseService(
            ILogService logService,
            ITaskProgressService taskProgressService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _taskProgressService = taskProgressService ?? throw new ArgumentNullException(nameof(taskProgressService));

            _preferencesFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance",
                "Config",
                "UserPreferences.json"
            );
        }

        public async Task<bool> CheckOperationsAndCloseAsync()
        {
            try
            {
                if (_taskProgressService.IsTaskRunning)
                {
                    string currentOperation = _taskProgressService.CurrentStatusText ?? "an operation";

                    _logService.LogInformation($"Close requested while operation in progress: {currentOperation}");

                    var result = CustomDialog.ShowConfirmation(
                        "Operation in Progress",
                        "Warning: Operation in Progress",
                        $"The following operation is still running:\n\n{currentOperation}\n\n" +
                        $"Closing now may leave incomplete files or mounted drives.\n\n" +
                        $"Cancel this operation and close Winhance?",
                        ""
                    );

                    if (result != true)
                    {
                        _logService.LogInformation("User cancelled application close due to running operation");
                        return false;
                    }

                    _logService.LogInformation("User confirmed close, cancelling operation...");
                    _taskProgressService.CancelCurrentTask();
                }

                await CloseApplicationWithSupportDialogAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error in CheckOperationsAndCloseAsync: {ex.Message}", ex);

                try
                {
                    await CloseApplicationWithSupportDialogAsync();
                }
                catch
                {
                    Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
                }
                return true;
            }
        }

        public async Task CloseApplicationWithSupportDialogAsync()
        {
            try
            {
                _logService.LogInformation("Closing application with support dialog check");

                bool showDialog = await ShouldShowSupportDialogAsync();

                if (showDialog)
                {
                    _logService.LogInformation("Showing donation dialog");

                    string supportMessage = "Your support helps keep this project going!";
                    var dialog = await DonationDialog.ShowDonationDialogAsync(
                        "Support Winhance",
                        supportMessage,
                        "Click 'Yes' to show your support!"
                    );

                    _logService.LogInformation($"Donation dialog completed with result: {dialog?.DialogResult}, DontShowAgain: {dialog?.DontShowAgain}");

                    if (dialog != null && dialog.DontShowAgain)
                    {
                        _logService.LogInformation("Saving DontShowSupport preference");
                        await SaveDontShowSupportPreferenceAsync(true);
                    }

                    if (dialog?.DialogResult == true)
                    {
                        _logService.LogInformation("User clicked Yes, opening donation page");
                        try
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = "https://ko-fi.com/memstechtips",
                                UseShellExecute = true,
                            };
                            Process.Start(psi);
                            _logService.LogInformation("Donation page opened successfully");
                        }
                        catch (Exception openEx)
                        {
                            _logService.LogError($"Error opening donation page: {openEx.Message}", openEx);
                        }
                    }
                }
                else
                {
                    _logService.LogInformation("Skipping donation dialog due to user preference");
                }

                _logService.LogInformation("Shutting down application");
                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error in CloseApplicationWithSupportDialogAsync: {ex.Message}", ex);

                try
                {
                    _logService.LogInformation("Falling back to Application.Current.Shutdown()");
                    Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
                }
                catch (Exception shutdownEx)
                {
                    _logService.LogError($"Error shutting down application: {shutdownEx.Message}", shutdownEx);
                    Environment.Exit(0);
                }
            }
        }

        public async Task<bool> ShouldShowSupportDialogAsync()
        {
            try
            {
                _logService.LogInformation($"Checking preferences file: {_preferencesFilePath}");

                if (File.Exists(_preferencesFilePath))
                {
                    string json = await File.ReadAllTextAsync(_preferencesFilePath);
                    _logService.LogInformation($"Preferences file content: {json}");

                    if (
                        json.Contains("\"DontShowSupport\": true")
                        || json.Contains("\"DontShowSupport\":true")
                        || json.Contains("\"DontShowSupport\": 1")
                        || json.Contains("\"DontShowSupport\":1")
                    )
                    {
                        _logService.LogInformation("DontShowSupport is set to true, skipping dialog");
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error checking donation dialog preference: {ex.Message}", ex);
                return true;
            }
        }

        public async Task SaveDontShowSupportPreferenceAsync(bool dontShow)
        {
            try
            {
                string preferencesDir = Path.GetDirectoryName(_preferencesFilePath);

                if (!Directory.Exists(preferencesDir))
                {
                    Directory.CreateDirectory(preferencesDir);
                }

                string json = "{}";
                if (File.Exists(_preferencesFilePath))
                {
                    json = await File.ReadAllTextAsync(_preferencesFilePath);
                }

                if (json == "{}")
                {
                    json = "{ \"DontShowSupport\": " + (dontShow ? "true" : "false") + " }";
                }
                else if (json.Contains("\"DontShowSupport\":") || json.Contains("\"DontShowSupport\": "))
                {
                    json = json.Replace("\"DontShowSupport\": true", "\"DontShowSupport\": " + (dontShow ? "true" : "false"));
                    json = json.Replace("\"DontShowSupport\": false", "\"DontShowSupport\": " + (dontShow ? "true" : "false"));
                    json = json.Replace("\"DontShowSupport\":true", "\"DontShowSupport\":" + (dontShow ? "true" : "false"));
                    json = json.Replace("\"DontShowSupport\":false", "\"DontShowSupport\":" + (dontShow ? "true" : "false"));
                }
                else
                {
                    json = json.TrimEnd('}');
                    if (json.TrimEnd().EndsWith("}"))
                    {
                        json += ",";
                    }
                    json += " \"DontShowSupport\": " + (dontShow ? "true" : "false") + " }";
                }

                await File.WriteAllTextAsync(_preferencesFilePath, json);
                _logService.LogInformation($"Successfully saved DontShowSupport preference: {dontShow}");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error saving DontShowSupport preference: {ex.Message}", ex);
            }
        }
    }
}
