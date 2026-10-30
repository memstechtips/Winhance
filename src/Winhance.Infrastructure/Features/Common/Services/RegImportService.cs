using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

[SupportedOSPlatform("windows")]
internal class RegImportService(
    IInteractiveUserService interactiveUserService,
    IFileSystemService fileSystemService,
    IProcessExecutor processExecutor,
    ILogService logService) : IRegImportService
{
    private static readonly Regex s_hkcuHeaderRegex = new(
        @"^(\s*\[-?)HKEY_CURRENT_USER\\",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    public async Task RunRegImportAsync(string regContent)
    {
        if (string.IsNullOrEmpty(regContent))
            return;

        // OTS: the import runs elevated so HKLM sections can be written, which a standard signed-in user cannot do,
        // and the HKCU sections are pointed at the signed-in user's hive instead of the admin's.
        if (interactiveUserService.IsOtsElevation && interactiveUserService.InteractiveUserSid is { } sid)
            regContent = s_hkcuHeaderRegex.Replace(regContent, m => $"{m.Groups[1].Value}HKEY_USERS\\{sid}\\");

        var tempFile = fileSystemService.CombinePath(fileSystemService.GetTempPath(), $"winhance_{Guid.NewGuid()}.reg");
        try
        {
            await fileSystemService.WriteAllTextAsync(tempFile, regContent).ConfigureAwait(false);
            var result = await processExecutor.ExecuteAsync("reg.exe", $"import \"{tempFile}\"").ConfigureAwait(false);
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"reg import failed (exit {result.ExitCode}): {result.StandardError.Trim()}");

            logService.Log(LogLevel.Info, "Registry import completed");
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Failed to import registry content: {ex.Message}");
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
}
