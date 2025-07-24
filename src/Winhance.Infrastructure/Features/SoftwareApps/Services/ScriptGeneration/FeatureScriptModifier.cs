using System;
using System.Collections.Generic;
using System.Text;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.ScriptGeneration
{
    /// <summary>
    /// Implementation of IFeatureScriptModifier that provides methods for modifying feature-related script content.
    /// </summary>
    public class FeatureScriptModifier : IFeatureScriptModifier
    {
        private readonly ILogService _logService;

        /// <summary>
        /// Initializes a new instance of the <see cref="FeatureScriptModifier"/> class.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        public FeatureScriptModifier(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <inheritdoc/>
        public string RemoveOptionalFeatureFromScript(string scriptContent, string featureName)
        {
            // Check if the optional features section exists
            int sectionStartIndex = scriptContent.IndexOf("# Disable Optional Features");
            if (sectionStartIndex == -1)
            {
                // Optional features section doesn't exist, so nothing to remove
                return scriptContent;
            }

            // Find the optional features array
            int arrayStartIndex = scriptContent.IndexOf("$optionalFeatures = @(", sectionStartIndex);
            if (arrayStartIndex == -1)
            {
                return scriptContent;
            }

            // Find the end of the optional features array
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

            // Parse the optional features in the array
            var optionalFeatures = new List<string>();
            var lines = arrayContent.Split('\n');
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("'") || trimmedLine.StartsWith("\""))
                {
                    var feature = trimmedLine.Trim('\'', '"', ' ', ',');
                    optionalFeatures.Add(feature);
                }
            }

            // Check if the feature is in the array
            bool removed =
                optionalFeatures.RemoveAll(f =>
                    f.Equals(featureName, StringComparison.OrdinalIgnoreCase)
                ) > 0;

            if (!removed)
            {
                // Feature not found in the array
                return scriptContent;
            }

            // If the array is now empty, remove the entire optional features section
            if (optionalFeatures.Count == 0)
            {
                // Find the end of the optional features section
                int sectionEndIndex = scriptContent.IndexOf(
                    "foreach ($feature in $optionalFeatures) {",
                    arrayEndIndex
                );
                if (sectionEndIndex != -1)
                {
                    sectionEndIndex = scriptContent.IndexOf("}", sectionEndIndex);
                    if (sectionEndIndex != -1)
                    {
                        sectionEndIndex = scriptContent.IndexOf('\n', sectionEndIndex);
                        if (sectionEndIndex != -1)
                        {
                            // Remove the entire section
                            return scriptContent.Substring(0, sectionStartIndex)
                                + scriptContent.Substring(sectionEndIndex + 1);
                        }
                    }
                }
            }

            // Rebuild the array content
            var newArrayContent = new StringBuilder();
            newArrayContent.AppendLine("$optionalFeatures = @(");

            foreach (var feature in optionalFeatures)
            {
                newArrayContent.AppendLine($"    '{feature}'");
            }

            newArrayContent.Append(")");

            // Replace the old array content with the new one
            return scriptContent.Substring(0, arrayStartIndex)
                + newArrayContent.ToString()
                + scriptContent.Substring(arrayEndIndex + 1);
        }
    }
}