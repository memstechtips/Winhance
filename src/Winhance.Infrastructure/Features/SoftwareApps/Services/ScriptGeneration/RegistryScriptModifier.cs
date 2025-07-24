using System;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.ScriptGeneration
{
    /// <summary>
    /// Implementation of IRegistryScriptModifier that provides methods for modifying registry-related script content.
    /// </summary>
    public class RegistryScriptModifier : IRegistryScriptModifier
    {
        private readonly ILogService _logService;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistryScriptModifier"/> class.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        public RegistryScriptModifier(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <inheritdoc/>
        public string RemoveAppRegistrySettingsFromScript(string scriptContent, string appName)
        {
            // Find the registry settings section
            int registrySection = scriptContent.IndexOf("# Registry settings");
            if (registrySection == -1)
            {
                return scriptContent;
            }

            // Generate possible section headers for this app
            var possibleSectionHeaders = new List<string>
            {
                $"# Registry settings for {appName}",
                $"# Registry settings for Microsoft.{appName}",
            };

            // Add variations without "Microsoft." prefix if it already has it
            if (appName.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
            {
                string nameWithoutPrefix = appName.Substring("Microsoft.".Length);
                possibleSectionHeaders.Add($"# Registry settings for {nameWithoutPrefix}");
            }

            // Add common variations
            possibleSectionHeaders.Add($"# Registry settings for {appName}_8wekyb3d8bbwe");

            // For Copilot specifically, add known variations
            if (appName.Contains("Copilot", StringComparison.OrdinalIgnoreCase))
            {
                possibleSectionHeaders.Add("# Registry settings for Copilot");
                possibleSectionHeaders.Add("# Registry settings for Microsoft.Copilot");
                possibleSectionHeaders.Add("# Registry settings for Windows.Copilot");
            }

            // For Xbox/GamingApp specifically, add known variations
            if (
                appName.Contains("Xbox", StringComparison.OrdinalIgnoreCase)
                || appName.Equals("Microsoft.GamingApp", StringComparison.OrdinalIgnoreCase)
            )
            {
                possibleSectionHeaders.Add("# Registry settings for Xbox");
                possibleSectionHeaders.Add("# Registry settings for Microsoft.Xbox");
                possibleSectionHeaders.Add("# Registry settings for GamingApp");
            }

            // Find the earliest matching section header
            int appSection = -1;
            string matchedHeader = null;

            foreach (var header in possibleSectionHeaders)
            {
                int index = scriptContent.IndexOf(header, registrySection);
                if (index != -1 && (appSection == -1 || index < appSection))
                {
                    appSection = index;
                    matchedHeader = header;
                }
            }

            if (appSection == -1)
            {
                return scriptContent;
            }

            // Find the end of the app section (next section or end of file)
            int nextSection = scriptContent.IndexOf(
                "# Registry settings for",
                appSection + matchedHeader.Length
            );
            if (nextSection == -1)
            {
                // Look for the next major section
                nextSection = scriptContent.IndexOf("# Prevent apps from reinstalling", appSection);
                if (nextSection == -1)
                {
                    // If no next section, just return the original content
                    return scriptContent;
                }
            }

            // Remove the app registry settings section
            return scriptContent.Substring(0, appSection) + scriptContent.Substring(nextSection);
        }
    }
}