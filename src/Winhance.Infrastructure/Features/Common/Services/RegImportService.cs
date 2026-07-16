using System;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

/// <summary>Imports .reg content via reg.exe, OTS-aware (write a temp .reg file, run reg.exe import - as the
/// interactive user under OTS).</summary>
[SupportedOSPlatform("windows")]
public class RegImportService(
    IInteractiveUserService interactiveUserService,
    IFileSystemService fileSystemService,
    IProcessExecutor processExecutor,
    ILogService logService) : IRegImportService
{
    public async Task RunRegImportAsync(string regContent)
    {
        if (string.IsNullOrEmpty(regContent))
            return;

        // OTS: write temp file to the interactive user's temp folder
        // so reg.exe running as that user can access it.
        string tempDir;
        if (interactiveUserService.IsOtsElevation)
        {
            var userLocalAppData = interactiveUserService.GetInteractiveUserFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            tempDir = fileSystemService.CombinePath(userLocalAppData, "Temp");
            fileSystemService.CreateDirectory(tempDir);
        }
        else
        {
            tempDir = fileSystemService.GetTempPath();
        }

        var tempFile = fileSystemService.CombinePath(tempDir, $"winhance_{Guid.NewGuid()}.reg");
        try
        {
            await fileSystemService.WriteAllTextAsync(tempFile, regContent).ConfigureAwait(false);
            logService.Log(LogLevel.Debug, $"[RegImportService] Wrote registry content to temp file: {tempFile}");

            // OTS: run reg import as the interactive user so HKCU
            // entries land in the standard user's hive, not the admin's.
            if (interactiveUserService.IsOtsElevation
                && interactiveUserService.HasInteractiveUserToken)
            {
                logService.Log(LogLevel.Debug, "[RegImportService] OTS mode - running reg import as interactive user");
                var result = await interactiveUserService.RunProcessAsInteractiveUserAsync(
                    "reg.exe", $"import \"{tempFile}\"").ConfigureAwait(false);

                if (result.ExitCode != 0)
                {
                    logService.Log(LogLevel.Warning, $"[RegImportService] reg import as interactive user failed (exit {result.ExitCode}): {result.StandardError}");
                }
            }
            else
            {
                await RunCommandAsync($"reg import \"{tempFile}\"").ConfigureAwait(false);
            }

            logService.Log(LogLevel.Info, "[RegImportService] Registry import completed");
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[RegImportService] Failed to import registry content: {ex.Message}");
            throw;
        }
        finally
        {
            if (fileSystemService.FileExists(tempFile))
            {
                fileSystemService.DeleteFile(tempFile);
            }
        }
    }

    private async Task RunCommandAsync(string command)
    {
        try
        {
            var result = await processExecutor.ExecuteAsync("cmd.exe", $"/c {command}").ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                logService.Log(LogLevel.Warning, $"[RegImportService] Command failed: {command} - {result.StandardError}");
            }
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"[RegImportService] Command execution failed: {command} - {ex.Message}");
        }
    }
}
