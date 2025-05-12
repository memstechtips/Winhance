using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.Core.Features.SoftwareApps.Exceptions;
using Winhance.Core.Features.SoftwareApps.Helpers;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.Common.ScriptGeneration;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Service that installs standard applications.
/// </summary>
public class AppInstallationService
    : BaseInstallationService<AppInfo>,
        IAppInstallationService,
        IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "WinhanceInstaller");
    private readonly HttpClient _httpClient;
    private readonly IScriptUpdateService _scriptUpdateService;
    private readonly ISystemServices _systemServices;
    private readonly IWinGetInstallationService _winGetInstallationService;
    private IProgress<TaskProgressDetail>? _currentProgress;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppInstallationService"/> class.
    /// </summary>
    /// <param name="logService">The log service.</param>
    /// <param name="powerShellService">The PowerShell execution service.</param>
    /// <param name="scriptUpdateService">The script update service.</param>
    /// <param name="systemServices">The system services.</param>
    /// <param name="winGetInstallationService">The WinGet installation service.</param>
    public AppInstallationService(
        ILogService logService,
        IPowerShellExecutionService powerShellService,
        IScriptUpdateService scriptUpdateService,
        ISystemServices systemServices,
        IWinGetInstallationService winGetInstallationService
    )
        : base(logService, powerShellService)
    {
        _httpClient = new HttpClient();
        _scriptUpdateService = scriptUpdateService;
        _systemServices = systemServices;
        _winGetInstallationService = winGetInstallationService;
        Directory.CreateDirectory(_tempDir);
    }

    /// <inheritdoc/>
    public Task<OperationResult<bool>> InstallAppAsync(
        AppInfo appInfo,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        return InstallItemAsync(appInfo, progress, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<OperationResult<bool>> CanInstallAppAsync(AppInfo appInfo)
    {
        return CanInstallItemAsync(appInfo);
    }

    /// <inheritdoc/>
    protected override async Task<OperationResult<bool>> PerformInstallationAsync(
        AppInfo appInfo,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken
    )
    {
        _currentProgress = progress;

        try
        {
            bool success = false;

            if (appInfo.PackageName.Equals("OneDrive", StringComparison.OrdinalIgnoreCase))
            {
                // Special handling for OneDrive
                success = await InstallOneDriveAsync(progress, cancellationToken);
            }
            else if (appInfo.IsCustomInstall)
            {
                success = await InstallCustomAppAsync(appInfo, progress, cancellationToken);
            }
            else
            {
                // Use WinGet for all standard apps, including appx packages
                // Use PackageID if available, otherwise fall back to PackageName
                string packageIdentifier = !string.IsNullOrEmpty(appInfo.PackageID)
                    ? appInfo.PackageID
                    : appInfo.PackageName;

                // Pass the app's display name to use in progress messages
                success = await _winGetInstallationService.InstallWithWingetAsync(
                    packageIdentifier,
                    progress,
                    cancellationToken,
                    appInfo.Name
                );
            }

            // Only update BloatRemoval.ps1 if installation was successful AND it's not an external app
            // External apps should never be added to BloatRemoval.ps1
            if (success && !IsExternalApp(appInfo))
            {
                try
                {
                    _logService.LogInformation(
                        $"Starting BloatRemoval.ps1 script update for {appInfo.Name}"
                    );

                    // Update the BloatRemoval.ps1 script to remove the installed app from the removal list
                    var appNames = new List<string> { appInfo.PackageName };
                    _logService.LogInformation(
                        $"Removing package name from BloatRemoval.ps1: {appInfo.PackageName}"
                    );

                    var appsWithRegistry = new Dictionary<string, List<AppRegistrySetting>>();
                    var appSubPackages = new Dictionary<string, string[]>();

                    // Add any subpackages if present
                    if (appInfo.SubPackages != null && appInfo.SubPackages.Length > 0)
                    {
                        _logService.LogInformation(
                            $"Adding {appInfo.SubPackages.Length} subpackages for {appInfo.Name}"
                        );
                        appSubPackages.Add(appInfo.PackageName, appInfo.SubPackages);
                    }

                    // Add registry settings if present
                    if (appInfo.RegistrySettings != null && appInfo.RegistrySettings.Length > 0)
                    {
                        _logService.LogInformation(
                            $"Adding {appInfo.RegistrySettings.Length} registry settings for {appInfo.Name}"
                        );
                        appsWithRegistry.Add(
                            appInfo.PackageName,
                            new List<AppRegistrySetting>(appInfo.RegistrySettings)
                        );
                    }

                    _logService.LogInformation(
                        $"Updating BloatRemoval.ps1 to remove {appInfo.Name} from removal list"
                    );
                    var result = await _scriptUpdateService.UpdateExistingBloatRemovalScriptAsync(
                        appNames,
                        appsWithRegistry,
                        appSubPackages,
                        true
                    ); // true = install operation, so remove from script

                    _logService.LogInformation(
                        $"Successfully updated BloatRemoval.ps1 script - {appInfo.Name} will no longer be removed"
                    );
                    _logService.LogInformation($"Script update result: {result?.Name ?? "null"}");
                }
                catch (Exception ex)
                {
                    _logService.LogError(
                        $"Error updating BloatRemoval.ps1 script for {appInfo.Name}",
                        ex
                    );
                    _logService.LogError($"Exception details: {ex.Message}");
                    _logService.LogError($"Stack trace: {ex.StackTrace}");
                    // Don't fail the installation if script update fails
                }
            }
            else if (success)
            {
                _logService.LogInformation(
                    $"Skipping BloatRemoval.ps1 update because {appInfo.Name} is an external app"
                );
            }
            else
            {
                _logService.LogInformation(
                    $"Skipping BloatRemoval.ps1 update because installation of {appInfo.Name} was not successful"
                );
            }

            return OperationResult<bool>.Succeeded(success);
        }
        finally
        {
            _currentProgress = null;
        }
    }

    /// <summary>
    /// Determines if an app is an external app (third-party) rather than a Windows built-in app.
    /// External apps should not be added to the BloatRemoval script.
    /// </summary>
    /// <param name="appInfo">The app to check</param>
    /// <returns>True if the app is an external app, false otherwise</returns>
    private bool IsExternalApp(AppInfo appInfo)
    {
        // Consider all apps with IsCustomInstall as external apps
        if (appInfo.IsCustomInstall)
            return true;

        // Check if the package name starts with Microsoft or matches known Windows app patterns
        bool isMicrosoftApp = appInfo.PackageName.StartsWith(
            "Microsoft.",
            StringComparison.OrdinalIgnoreCase
        );

        // Check if the app is a number-based Microsoft Store app ID
        bool isStoreAppId =
            !string.IsNullOrEmpty(appInfo.PackageID)
            && (
                appInfo.PackageID.All(c => char.IsLetterOrDigit(c) || c == '.')
                && (appInfo.PackageID.Length == 9 || appInfo.PackageID.Length == 12)
            ); // Microsoft Store app IDs are typically 9 or 12 chars

        // Check if it's an optional feature or capability, which are Windows components
        bool isWindowsComponent =
            appInfo.Type == AppType.Capability || appInfo.Type == AppType.OptionalFeature;

        // Any third-party app with a period in its name is likely an external app (e.g., VideoLAN.VLC)
        bool isThirdPartyNamedApp =
            !isMicrosoftApp && appInfo.PackageName.Contains('.') && !isWindowsComponent;

        // If it's a Microsoft app or Windows component, it's not external
        // Otherwise, it's likely an external app
        return isThirdPartyNamedApp
            || appInfo.IsCustomInstall
            || (!isMicrosoftApp && !isStoreAppId && !isWindowsComponent);
    }

    /// <inheritdoc/>
    public Task<bool> InstallCustomAppAsync(
        AppInfo appInfo,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        // Implementation remains in this class for now
        // In future refactoring phases, this can be moved to a specialized service
        _currentProgress = progress;

        try
        {
            // Handle different custom app installations based on package name
            switch (appInfo.PackageName.ToLowerInvariant())
            {
                // Special handling for OneDrive
                case "onedrive":
                    return InstallOneDriveAsync(progress, cancellationToken);

                // Add custom app installation logic here
                // case "some-app":
                //    return await InstallSomeAppAsync(progress, cancellationToken);

                default:
                    throw new NotSupportedException(
                        $"Custom installation for '{appInfo.PackageName}' is not supported."
                    );
            }
        }
        catch (Exception ex)
        {
            var errorType = InstallationErrorHelper.DetermineErrorType(ex.Message);
            var errorMessage = InstallationErrorHelper.GetUserFriendlyErrorMessage(errorType);

            progress?.Report(
                new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText = $"Error in custom installation for {appInfo.Name}: {errorMessage}",
                    DetailedMessage = $"Exception during custom installation: {ex.Message}",
                    LogLevel = LogLevel.Error,
                    AdditionalInfo = new Dictionary<string, string>
                    {
                        { "ErrorType", errorType.ToString() },
                        { "PackageName", appInfo.PackageName },
                        { "AppName", appInfo.Name },
                        { "IsCustomInstall", "True" },
                        { "OriginalError", ex.Message },
                    },
                }
            );

            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Installs an app using WinGet.
    /// </summary>
    /// <param name="packageName">The package name to install.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <param name="displayName">Optional display name for the app.</param>
    /// <returns>True if installation was successful; otherwise, false.</returns>
    public Task<bool> InstallWithWingetAsync(
        string packageName,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default,
        string displayName = null
    )
    {
        return _winGetInstallationService.InstallWithWingetAsync(
            packageName,
            progress,
            cancellationToken,
            displayName
        );
    }

    /// <summary>
    /// Disposes the resources used by the service.
    /// </summary>
    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Installs OneDrive from the Microsoft download link.
    /// </summary>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>True if installation was successful; otherwise, false.</returns>
    private async Task<bool> InstallOneDriveAsync(
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken
    )
    {
        try
        {
            progress?.Report(
                new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText = "Starting OneDrive installation...",
                    DetailedMessage = "Downloading OneDrive installer from Microsoft",
                }
            );

            // Download OneDrive from the specific URL
            string downloadUrl = "https://go.microsoft.com/fwlink/p/?LinkID=2182910";
            string installerPath = Path.Combine(_tempDir, "OneDriveSetup.exe");

            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(downloadUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    progress?.Report(
                        new TaskProgressDetail
                        {
                            Progress = 0,
                            StatusText = "Failed to download OneDrive installer",
                            DetailedMessage = $"HTTP error: {response.StatusCode}",
                            LogLevel = LogLevel.Error,
                        }
                    );
                    return false;
                }

                using (
                    var fileStream = new FileStream(
                        installerPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None
                    )
                )
                {
                    await response.Content.CopyToAsync(fileStream, cancellationToken);
                }
            }

            progress?.Report(
                new TaskProgressDetail
                {
                    Progress = 50,
                    StatusText = "Installing OneDrive...",
                    DetailedMessage = "Running OneDrive installer",
                }
            );

            // Run the installer
            using (var process = new System.Diagnostics.Process())
            {
                process.StartInfo.FileName = installerPath;
                process.StartInfo.Arguments = "/silent";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.Start();
                await Task.Run(() => process.WaitForExit(), cancellationToken);

                bool success = process.ExitCode == 0;

                progress?.Report(
                    new TaskProgressDetail
                    {
                        Progress = 100,
                        StatusText = success
                            ? "OneDrive installed successfully"
                            : "OneDrive installation failed",
                        DetailedMessage = $"Installer exited with code: {process.ExitCode}",
                        LogLevel = success ? LogLevel.Success : LogLevel.Error,
                    }
                );

                return success;
            }
        }
        catch (Exception ex)
        {
            progress?.Report(
                new TaskProgressDetail
                {
                    Progress = 0,
                    StatusText = "Error installing OneDrive",
                    DetailedMessage = $"Exception: {ex.Message}",
                    LogLevel = LogLevel.Error,
                }
            );
            return false;
        }
    }
}
