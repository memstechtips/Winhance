using System;
using System.Collections.Generic;
using System.Net.Http;
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

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Service that handles custom application installations.
/// </summary>
public class CustomAppInstallationService : ICustomAppInstallationService
{
    private readonly ILogService _logService;
    private readonly IOneDriveInstallationService _oneDriveInstallationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CustomAppInstallationService"/> class.
    /// </summary>
    /// <param name="logService">The log service.</param>
    /// <param name="oneDriveInstallationService">The OneDrive installation service.</param>
    public CustomAppInstallationService(
        ILogService logService,
        IOneDriveInstallationService oneDriveInstallationService)
    {
        _logService = logService;
        _oneDriveInstallationService = oneDriveInstallationService;
    }

    /// <inheritdoc/>
    public async Task<bool> InstallCustomAppAsync(
        AppInfo appInfo,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Handle different custom app installations based on package name
            switch (appInfo.PackageName.ToLowerInvariant())
            {
                case "onedrive":
                    return await _oneDriveInstallationService.InstallOneDriveAsync(progress, cancellationToken);

                // Add other custom app installation cases here
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
                        { "OriginalError", ex.Message }
                    }
                }
            );

            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> CheckInternetConnectionAsync()
    {
        try
        {
            // Try to reach a reliable site to check for internet connectivity
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var response = await client.GetAsync("https://www.google.com");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
