using System;
using System.IO;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.ScriptGeneration
{
    /// <summary>
    /// Implementation of IBloatRemovalScriptSavingService that handles saving scripts to the file system.
    /// </summary>
    public class BloatRemovalScriptSavingService : IBloatRemovalScriptSavingService
    {
        private readonly ILogService _logService;
        private readonly string _scriptsPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="BloatRemovalScriptSavingService"/> class.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        public BloatRemovalScriptSavingService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _scriptsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Winhance",
                "Scripts"
            );

            // Ensure the scripts directory exists
            if (!Directory.Exists(_scriptsPath))
            {
                Directory.CreateDirectory(_scriptsPath);
                _logService.LogInformation($"Created scripts directory at {_scriptsPath}");
            }
        }

        /// <inheritdoc/>
        public async Task SaveScriptAsync(RemovalScript script)
        {
            try
            {
                string scriptPath = Path.Combine(_scriptsPath, $"{script.Name}.ps1");
                await File.WriteAllTextAsync(scriptPath, script.Content);
                _logService.LogInformation($"Saved script to {scriptPath}");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error saving script: {script.Name}", ex);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SaveScriptAsync(string scriptPath, string scriptContent)
        {
            try
            {
                await File.WriteAllTextAsync(scriptPath, scriptContent);
                _logService.LogInformation($"Saved script to {scriptPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error saving script to {scriptPath}", ex);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<string> GetScriptContentAsync(string scriptPath)
        {
            try
            {
                if (!File.Exists(scriptPath))
                {
                    _logService.LogWarning($"Script file not found at {scriptPath}");
                    return null;
                }

                string content = await File.ReadAllTextAsync(scriptPath);
                return content;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error reading script from {scriptPath}", ex);
                throw;
            }
        }
    }
}
