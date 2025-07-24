using System;
using System.Collections.Generic;
using System.Text;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.ScriptGeneration
{
    /// <summary>
    /// Implementation of IPackageScriptModifier that provides methods for modifying package-related script content.
    /// </summary>
    public class PackageScriptModifier : IPackageScriptModifier
    {
        private readonly ILogService _logService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageScriptModifier"/> class.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        public PackageScriptModifier(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <inheritdoc/>
        public string RemovePackageFromScript(string scriptContent, string packageName)
        {
            // Find the packages array section in the script
            int arrayStartIndex = scriptContent.IndexOf("$packages = @(");
            if (arrayStartIndex == -1)
            {
                return scriptContent;
            }

            // Find the end of the packages array
            int arrayEndIndex = scriptContent.IndexOf(")", arrayStartIndex);
            if (arrayEndIndex == -1)
            {
                return scriptContent;
            }

            // Extract the array content
            string arrayContent = scriptContent.Substring(
                arrayStartIndex,
                arrayEndIndex - arrayStartIndex + 1
            );

            // Parse the packages in the array
            var packages = new List<string>();
            var lines = arrayContent.Split('\n');
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("'") || trimmedLine.StartsWith("\""))
                {
                    var package = trimmedLine.Trim('\'', '"', ' ', ',');
                    packages.Add(package);
                }
            }

            // Check if the package is in the array
            bool removed =
                packages.RemoveAll(p => p.Equals(packageName, StringComparison.OrdinalIgnoreCase)) > 0;

            if (!removed)
            {
                // Package not found in the array
                return scriptContent;
            }

            // Rebuild the array content
            var newArrayContent = new StringBuilder();
            newArrayContent.AppendLine("$packages = @(");

            foreach (var package in packages)
            {
                newArrayContent.AppendLine($"    '{package}'");
            }

            newArrayContent.Append(")");

            // Replace the old array content with the new one
            return scriptContent.Substring(0, arrayStartIndex)
                + newArrayContent.ToString()
                + scriptContent.Substring(arrayEndIndex + 1);
        }
    }
}