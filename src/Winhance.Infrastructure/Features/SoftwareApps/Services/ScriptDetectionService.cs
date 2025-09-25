using System.Collections.Generic;
using System.IO;
using System.Linq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services
{
    public class ScriptDetectionService : IScriptDetectionService
    {
        private static readonly Dictionary<string, string> ScriptDescriptions = new(StringComparer.OrdinalIgnoreCase)
        {
            { "BloatRemoval.ps1", "Multiple items removed via BloatRemoval.ps1" },
            { "EdgeRemoval.ps1", "Microsoft Edge Removed via EdgeRemoval.ps1" },
            { "OneDriveRemoval.ps1", "OneDrive Removed via OneDriveRemoval.ps1" },
        };

        public bool AreRemovalScriptsPresent()
        {
            var scriptsDirectory = ScriptPaths.ScriptsDirectory;
            if (!Directory.Exists(scriptsDirectory))
            {
                return false;
            }

            return GetScriptFiles().Any();
        }

        public IEnumerable<ScriptInfo> GetActiveScripts()
        {
            var scriptsDirectory = ScriptPaths.ScriptsDirectory;
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
            var scriptsDirectory = ScriptPaths.ScriptsDirectory;
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
