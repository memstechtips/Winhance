using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

public class ApplicationCloseService : IApplicationCloseService
{
    private readonly ILogService _logService;
    private readonly ITaskProgressService _taskProgressService;
    private readonly IUserPreferencesService _userPreferencesService;
    private readonly IDialogService _dialogService;
    private readonly IProcessExecutor _processExecutor;

    public Func<Task>? BeforeShutdown { get; set; }

    public ApplicationCloseService(
        ILogService logService,
        ITaskProgressService taskProgressService,
        IUserPreferencesService userPreferencesService,
        IDialogService dialogService,
        IProcessExecutor processExecutor)
    {
        _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        _taskProgressService = taskProgressService ?? throw new ArgumentNullException(nameof(taskProgressService));
        _userPreferencesService = userPreferencesService ?? throw new ArgumentNullException(nameof(userPreferencesService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _processExecutor = processExecutor ?? throw new ArgumentNullException(nameof(processExecutor));
    }

    public async Task<bool> CheckOperationsAndCloseAsync()
    {
        try
        {
            if (BeforeShutdown != null)
            {
                try
                {
                    await BeforeShutdown.Invoke();
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error running cleanup tasks: {ex.Message}", ex);
                }
            }

            if (_taskProgressService.IsTaskRunning)
            {
                string currentOperation = _taskProgressService.CurrentStatusText ?? "an operation";

                _logService.LogInformation($"Close requested while operation in progress: {currentOperation}");

                var confirmed = await _dialogService.ShowConfirmationAsync(
                    $"The following operation is still running:\n\n{currentOperation}\n\n" +
                    $"Closing now may leave incomplete files or mounted drives.\n\n" +
                    $"Cancel this operation and close Winhance?",
                    "Warning: Operation in Progress",
                    "Yes, Close",
                    "Cancel");

                if (!confirmed)
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
                Application.Current.Exit();
            }
            return true;
        }
    }

    private async Task CloseApplicationWithSupportDialogAsync()
    {
        try
        {
            _logService.LogInformation("Closing application with support dialog check");

            bool showDialog = await ShouldShowSupportDialogAsync();

            if (showDialog)
            {
                _logService.LogInformation("Showing donation dialog");

                var (result, dontShowAgain) = await _dialogService.ShowDonationDialogAsync();

                _logService.LogInformation($"Donation dialog completed with result: {result}, DontShowAgain: {dontShowAgain}");

                if (dontShowAgain)
                {
                    _logService.LogInformation("Saving DontShowSupport preference");
                    await SaveDontShowSupportPreferenceAsync(true);
                }

                if (result == true)
                {
                    _logService.LogInformation("User clicked Yes, opening donation page");
                    try
                    {
                        await _processExecutor.ShellExecuteAsync("https://ko-fi.com/memstechtips");
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
            Application.Current.Exit();
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error in CloseApplicationWithSupportDialogAsync: {ex.Message}", ex);

            try
            {
                Application.Current.Exit();
            }
            catch (Exception shutdownEx)
            {
                _logService.LogError($"Error shutting down application: {shutdownEx.Message}", shutdownEx);
                Environment.Exit(0);
            }
        }
    }

    private async Task<bool> ShouldShowSupportDialogAsync()
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

    private async Task SaveDontShowSupportPreferenceAsync(bool dontShow)
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
