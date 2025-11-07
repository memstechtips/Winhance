using System;
using System.Diagnostics;
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
        private readonly IUserPreferencesService _userPreferencesService;

        public ApplicationCloseService(
            ILogService logService,
            ITaskProgressService taskProgressService,
            IUserPreferencesService userPreferencesService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _taskProgressService = taskProgressService ?? throw new ArgumentNullException(nameof(taskProgressService));
            _userPreferencesService = userPreferencesService ?? throw new ArgumentNullException(nameof(userPreferencesService));
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
                _logService.LogInformation("Checking DontShowSupport preference");

                bool dontShow = await _userPreferencesService.GetPreferenceAsync("DontShowSupport", false);

                if (dontShow)
                {
                    _logService.LogInformation("DontShowSupport is set to true, skipping dialog");
                    return false;
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
                _logService.LogInformation($"Saving DontShowSupport preference: {dontShow}");

                bool success = await _userPreferencesService.SetPreferenceAsync("DontShowSupport", dontShow);

                if (success)
                {
                    _logService.LogInformation("Successfully saved DontShowSupport preference");
                }
                else
                {
                    _logService.LogError("Failed to save DontShowSupport preference");
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error saving DontShowSupport preference: {ex.Message}", ex);
            }
        }
    }
}
