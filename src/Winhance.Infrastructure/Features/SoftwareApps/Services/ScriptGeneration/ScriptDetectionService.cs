using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.ScriptGeneration
{
    /// <summary>
    /// Implementation of the IScriptDetectionService interface.
    /// </summary>
    public class ScriptDetectionService : IScriptDetectionService
    {
        private readonly IScriptPathService _scriptPathService;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ScriptDetectionService"/> class.
        /// </summary>
        /// <param name="scriptPathService">The script path service.</param>
        public ScriptDetectionService(IScriptPathService scriptPathService)
        {
            _scriptPathService = scriptPathService ?? throw new ArgumentNullException(nameof(scriptPathService));
        }

        private static readonly Dictionary<string, string> ScriptDescriptions = new Dictionary<
            string,
            string
        >(StringComparer.OrdinalIgnoreCase)
        {
            { "BloatRemoval.ps1", "Multiple items removed via BloatRemoval.ps1" },
            { "EdgeRemoval.ps1", "Microsoft Edge Removed via EdgeRemoval.ps1" },
            { "OneDriveRemoval.ps1", "OneDrive Removed via OneDriveRemoval.ps1" },
            { "OneNoteRemoval.ps1", "OneNote Removed via OneNoteRemoval.ps1" },
        };

        /// <inheritdoc />
        public bool AreRemovalScriptsPresent()
        {
            var scriptsDirectory = _scriptPathService.GetScriptsDirectory();
            if (!Directory.Exists(scriptsDirectory))
            {
                return false;
            }

            return GetScriptFiles().Any();
        }

        /// <inheritdoc />
        public IEnumerable<ScriptInfo> GetActiveScripts()
        {
            var scriptsDirectory = _scriptPathService.GetScriptsDirectory();
            if (!Directory.Exists(scriptsDirectory))
            {
                return Enumerable.Empty<ScriptInfo>();
            }

            var scriptFiles = GetScriptFiles();

            return scriptFiles.Select(file => new ScriptInfo
            {
                Name = Path.GetFileName(file),
                Description = GetScriptDescription(Path.GetFileName(file)),
                FilePath = file,
            });
        }

        private IEnumerable<string> GetScriptFiles()
        {
            var scriptsDirectory = _scriptPathService.GetScriptsDirectory();
            if (!Directory.Exists(scriptsDirectory))
            {
                return Enumerable.Empty<string>();
            }

            return Directory
                .GetFiles(scriptsDirectory, "*.ps1")
                .Where(file => ScriptDescriptions.ContainsKey(Path.GetFileName(file)));
        }

        private string GetScriptDescription(string fileName)
        {
            return ScriptDescriptions.TryGetValue(fileName, out var description)
                ? description
                : $"Unknown script: {fileName}";
        }
    }
}
