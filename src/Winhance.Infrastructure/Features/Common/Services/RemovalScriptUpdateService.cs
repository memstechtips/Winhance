using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Core.Features.SoftwareApps.Utilities;

namespace Winhance.Infrastructure.Features.Common.Services;

public class RemovalScriptUpdateService : IRemovalScriptUpdateService
{
    private readonly ILogService _logService;
    private readonly IScheduledTaskService _scheduledTaskService;
    private readonly IFileSystemService _fileSystemService;
    private static readonly Regex ScriptVersionRegex = new(@"Script Version:\s*(\d+\.\d+)", RegexOptions.Compiled);

    private static readonly ScriptInfo[] Scripts =
    {
        new("EdgeRemoval", EdgeRemovalScript.ScriptVersion, GetContent: EdgeRemovalScript.GetScript),
        new("OneDriveRemoval", OneDriveRemovalScript.ScriptVersion, GetContent: OneDriveRemovalScript.GetScript),
        new("BloatRemoval", BloatRemovalScriptGenerator.ScriptVersion,
            UpdateContent: BloatRemovalScriptGenerator.UpdateScriptTemplate,
            RunAfterUpdate: false)
    };

    public RemovalScriptUpdateService(ILogService logService, IScheduledTaskService scheduledTaskService, IFileSystemService fileSystemService)
    {
        _logService = logService;
        _scheduledTaskService = scheduledTaskService;
        _fileSystemService = fileSystemService;
    }

    public async Task CheckAndUpdateScriptsAsync()
    {
        foreach (var script in Scripts)
        {
            await CheckAndUpdateScriptAsync(script).ConfigureAwait(false);
        }
    }

    private async Task CheckAndUpdateScriptAsync(ScriptInfo script)
    {
        var scriptPath = _fileSystemService.CombinePath(ScriptPaths.ScriptsDirectory, $"{script.Name}.ps1");

        if (!_fileSystemService.FileExists(scriptPath))
        {
            return;
        }

        var installedVersion = ExtractVersionFromFile(scriptPath);

        if (installedVersion == script.CurrentVersion)
        {
            _logService.LogInformation($"{script.Name} script is up to date (v{installedVersion})");
            return;
        }

        _logService.LogInformation($"Updating {script.Name} script from v{installedVersion ?? "unknown"} to v{script.CurrentVersion}");

        try
        {
            if (script.UpdateContent != null)
            {
                var existingContent = _fileSystemService.ReadAllText(scriptPath);
                _fileSystemService.WriteAllText(scriptPath, script.UpdateContent(existingContent));
            }
            else
            {
                _fileSystemService.WriteAllText(scriptPath, script.GetContent!());
            }

            _logService.LogInformation($"{script.Name} script file updated");

            if (script.RunAfterUpdate)
            {
                await _scheduledTaskService.RunScheduledTaskAsync(script.Name).ConfigureAwait(false);
                _logService.LogInformation($"{script.Name} scheduled task executed");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Failed to update {script.Name} script: {ex.Message}");
        }
    }

    private string? ExtractVersionFromFile(string filePath)
    {
        try
        {
            var content = _fileSystemService.ReadAllText(filePath);
            var match = ScriptVersionRegex.Match(content);
            return match.Success ? match.Groups[1].Value : null;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Failed to extract version from {filePath}: {ex.Message}");
            return null;
        }
    }

    private record ScriptInfo(
        string Name,
        string CurrentVersion,
        Func<string>? GetContent = null,
        Func<string, string>? UpdateContent = null,
        bool RunAfterUpdate = true);
}
