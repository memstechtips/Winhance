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
        private const string ScriptsDirectory = @"C:\Program Files\Winhance\Scripts";

        private static readonly Dictionary<string, string> ScriptDescriptions = new Dictionary<
            string,
            string
        >(StringComparer.OrdinalIgnoreCase)
        {
            { "BloatRemoval.ps1", "Multiple items removed via BloatRemoval.ps1" },
            { "EdgeRemoval.ps1", "Microsoft Edge Removed via EdgeRemoval.ps1" },
            { "OneDriveRemoval.ps1", "OneDrive Removed via OneDriveRemoval.ps1" },
        };

        /// <inheritdoc />
        public bool AreRemovalScriptsPresent()
        {
            if (!Directory.Exists(ScriptsDirectory))
            {
                return false;
            }

            return GetScriptFiles().Any();
        }

        /// <inheritdoc />
        public IEnumerable<ScriptInfo> GetActiveScripts()
        {
            if (!Directory.Exists(ScriptsDirectory))
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
            if (!Directory.Exists(ScriptsDirectory))
            {
                return Enumerable.Empty<string>();
            }

            return Directory
                .GetFiles(ScriptsDirectory, "*.ps1")
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
