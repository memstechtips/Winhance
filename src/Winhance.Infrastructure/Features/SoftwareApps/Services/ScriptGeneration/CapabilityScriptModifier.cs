using System;
using System.Collections.Generic;
using System.Text;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.ScriptGeneration
{
    /// <summary>
    /// Implementation of ICapabilityScriptModifier that provides methods for modifying capability-related script content.
    /// </summary>
    public class CapabilityScriptModifier : ICapabilityScriptModifier
    {
        private readonly ILogService _logService;

        /// <summary>
        /// Initializes a new instance of the <see cref="CapabilityScriptModifier"/> class.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        public CapabilityScriptModifier(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <inheritdoc/>
        public string RemoveCapabilityFromScript(string scriptContent, string capabilityName)
        {
            // Find the capabilities array section in the script
            int arrayStartIndex = scriptContent.IndexOf("$capabilities = @(");
            if (arrayStartIndex == -1)
            {
                return scriptContent;
            }

            // Find the end of the capabilities array
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

            // Parse the capabilities in the array
            var capabilities = new List<string>();
            var lines = arrayContent.Split('\n');
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("'") || trimmedLine.StartsWith("\""))
                {
                    var capability = trimmedLine.Trim('\'', '"', ' ', ',');
                    capabilities.Add(capability);
                }
            }

            // Check if the capability is in the array
            bool removed =
                capabilities.RemoveAll(c =>
                    c.Equals(capabilityName, StringComparison.OrdinalIgnoreCase)
                ) > 0;

            if (!removed)
            {
                // Capability not found in the array
                return scriptContent;
            }

            // Rebuild the array content
            var newArrayContent = new StringBuilder();
            newArrayContent.AppendLine("$capabilities = @(");

            foreach (var capability in capabilities)
            {
                newArrayContent.AppendLine($"    '{capability}'");
            }

            newArrayContent.Append(")");

            // Replace the old array content with the new one
            return scriptContent.Substring(0, arrayStartIndex)
                + newArrayContent.ToString()
                + scriptContent.Substring(arrayEndIndex + 1);
        }
    }
}