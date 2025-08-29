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
    /// <summary>
    /// Service for handling application close functionality
    /// </summary>
    public class ApplicationCloseService : IApplicationCloseService
    {
        private readonly ILogService _logService;
        private readonly string _preferencesFilePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="ApplicationCloseService"/> class
        /// </summary>
        /// <param name="logService">Service for logging</param>
        public ApplicationCloseService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));

            // Set up the preferences file path
            _preferencesFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Winhance",
                "Config",
                "UserPreferences.json"
            );
        }

        /// <inheritdoc/>
        public async Task CloseApplicationWithSupportDialogAsync()
        {
            try
            {
                _logService.LogInformation("Closing application with support dialog check");

                // Check if we should show the dialog
                bool showDialog = await ShouldShowSupportDialogAsync();

                if (showDialog)
                {
                    _logService.LogInformation("Showing donation dialog");

                    // Show the dialog
                    string supportMessage = "Your support helps keep this project going!";
                    var dialog = await DonationDialog.ShowDonationDialogAsync(
                        "Support Winhance",
                        supportMessage,
                        "Click 'Yes' to show your support!"
                    );

                    _logService.LogInformation($"Donation dialog completed with result: {dialog?.DialogResult}, DontShowAgain: {dialog?.DontShowAgain}");

                    // Save the "Don't show again" preference if checked
                    if (dialog != null && dialog.DontShowAgain)
                    {
                        _logService.LogInformation("Saving DontShowSupport preference");
                        await SaveDontShowSupportPreferenceAsync(true);
                    }

                    // Open the donation page if the user clicked Yes
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

                // Close the application
                _logService.LogInformation("Shutting down application");
                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error in CloseApplicationWithSupportDialogAsync: {ex.Message}", ex);

                // Fallback to direct application shutdown
                try
                {
                    _logService.LogInformation("Falling back to Application.Current.Shutdown()");
                    Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
                }
                catch (Exception shutdownEx)
                {
                    _logService.LogError($"Error shutting down application: {shutdownEx.Message}", shutdownEx);

                    // Last resort
                    Environment.Exit(0);
                }
            }
        }

        /// <inheritdoc/>
        public async Task<bool> ShouldShowSupportDialogAsync()
        {
            try
            {
                _logService.LogInformation($"Checking preferences file: {_preferencesFilePath}");

                // Check if the preference file exists and contains the DontShowSupport setting
                if (File.Exists(_preferencesFilePath))
                {
                    string json = await File.ReadAllTextAsync(_preferencesFilePath);
                    _logService.LogInformation($"Preferences file content: {json}");

                    // Check if the preference is set to true
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

                // Default to showing the dialog
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error checking donation dialog preference: {ex.Message}", ex);
                // Default to showing the dialog if there's an error
                return true;
            }
        }

        /// <inheritdoc/>
        public async Task SaveDontShowSupportPreferenceAsync(bool dontShow)
        {
            try
            {
                // Get the preferences directory path
                string preferencesDir = Path.GetDirectoryName(_preferencesFilePath);

                // Create the directory if it doesn't exist
                if (!Directory.Exists(preferencesDir))
                {
                    Directory.CreateDirectory(preferencesDir);
                }

                // Create or update the preferences file
                string json = "{}";
                if (File.Exists(_preferencesFilePath))
                {
                    json = await File.ReadAllTextAsync(_preferencesFilePath);
                }

                // Simple JSON manipulation (not ideal but should work for this case)
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
                    // Remove the closing brace
                    json = json.TrimEnd('}');
                    // Add a comma if there are other properties
                    if (json.TrimEnd().EndsWith("}"))
                    {
                        json += ",";
                    }
                    // Add the new property and closing brace
                    json += " \"DontShowSupport\": " + (dontShow ? "true" : "false") + " }";
                }

                // Write the updated JSON back to the file
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
