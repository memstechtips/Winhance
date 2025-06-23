using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces.ScriptGeneration;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services.ScriptGeneration
{
    /// <summary>
    /// Service for building PowerShell script content.
    /// </summary>
    public class BloatRemovalScriptBuilderService : IBloatRemovalScriptBuilderService
    {
        private readonly IBloatRemovalScriptTemplateProvider _bloatRemovalScriptTemplateProvider;
        private readonly ILogService _logService;

        /// <summary>
        /// Initializes a new instance of the <see cref="BloatRemovalScriptBuilderService"/> class.
        /// </summary>
        /// <param name="bloatRemovalScriptTemplateProvider">The template provider.</param>
        /// <param name="logService">The logging service.</param>
        public BloatRemovalScriptBuilderService(
            IBloatRemovalScriptTemplateProvider bloatRemovalScriptTemplateProvider,
            ILogService logService
        )
        {
            _bloatRemovalScriptTemplateProvider =
                bloatRemovalScriptTemplateProvider
                ?? throw new ArgumentNullException(nameof(bloatRemovalScriptTemplateProvider));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <inheritdoc/>
        public string BuildPackageRemovalScript(IEnumerable<string> packageNames)
        {
            if (packageNames == null || !packageNames.Any())
            {
                return string.Empty;
            }

            // Get the full script template
            string fullTemplate = _bloatRemovalScriptTemplateProvider.GetFullScriptTemplate();
            
            // Create a script with just the packages
            var script = BuildCompleteRemovalScript(
                packageNames,
                null,
                null,
                null,
                null);
            
            return script;
        }

        /// <inheritdoc/>
        public string BuildCapabilityRemovalScript(IEnumerable<string> capabilityNames)
        {
            if (capabilityNames == null || !capabilityNames.Any())
            {
                return string.Empty;
            }

            // Get the full script template
            string fullTemplate = _bloatRemovalScriptTemplateProvider.GetFullScriptTemplate();
            
            // Create a script with just the capabilities
            var script = BuildCompleteRemovalScript(
                null,
                capabilityNames,
                null,
                null,
                null);
            
            return script;
        }

        /// <inheritdoc/>
        public string BuildFeatureRemovalScript(IEnumerable<string> featureNames)
        {
            if (featureNames == null || !featureNames.Any())
            {
                return string.Empty;
            }

            // Get the full script template
            string fullTemplate = _bloatRemovalScriptTemplateProvider.GetFullScriptTemplate();
            
            // Create a script with just the features
            var script = BuildCompleteRemovalScript(
                null,
                null,
                featureNames,
                null,
                null);
            
            return script;
        }

        /// <inheritdoc/>
        public string BuildRegistryScript(
            Dictionary<string, List<AppRegistrySetting>> registrySettings
        )
        {
            if (registrySettings == null || !registrySettings.Any())
            {
                return string.Empty;
            }

            // Create a script with just the registry settings
            var script = BuildCompleteRemovalScript(
                null,
                null,
                null,
                registrySettings,
                null);
            
            return script;
        }

        /// <inheritdoc/>
        public string BuildCompleteRemovalScript(
            IEnumerable<string> packageNames,
            IEnumerable<string> capabilityNames,
            IEnumerable<string> featureNames,
            Dictionary<string, List<AppRegistrySetting>> registrySettings,
            Dictionary<string, string[]> subPackages
        )
        {
            // Get the full script template
            string scriptTemplate = _bloatRemovalScriptTemplateProvider.GetFullScriptTemplate();
            
            // Process packages
            var allPackages = new List<string>();
            
            // Add main packages
            if (packageNames != null)
            {
                allPackages.AddRange(packageNames);
            }
            
            // Add subpackages
            if (subPackages != null)
            {
                foreach (var subPackageEntry in subPackages)
                {
                    if (subPackageEntry.Value != null)
                    {
                        allPackages.AddRange(subPackageEntry.Value);
                    }
                }
            }
            
            // Remove duplicates
            allPackages = allPackages.Distinct().ToList();
            
            // Update packages array in the template
            if (allPackages.Any())
            {
                var packagesBlock = new StringBuilder();
                foreach (var package in allPackages)
                {
                    packagesBlock.AppendLine($"    '{package}'");
                }
                
                // Replace the empty packages array with our packages
                const string packagesPattern = "$packages = @()"; 
                scriptTemplate = scriptTemplate.Replace(packagesPattern, $"$packages = @(\n{packagesBlock})");
            }
            
            // Update capabilities array in the template
            if (capabilityNames != null && capabilityNames.Any())
            {
                var capabilitiesBlock = new StringBuilder();
                foreach (var capability in capabilityNames)
                {
                    capabilitiesBlock.AppendLine($"    '{capability}'");
                }
                
                // Replace the empty capabilities array with our capabilities
                const string capabilitiesPattern = "$capabilities = @()"; 
                scriptTemplate = scriptTemplate.Replace(capabilitiesPattern, $"$capabilities = @(\n{capabilitiesBlock})");
            }
            
            // Update features array in the template
            if (featureNames != null && featureNames.Any())
            {
                var featuresBlock = new StringBuilder();
                foreach (var feature in featureNames)
                {
                    featuresBlock.AppendLine($"    '{feature}'");
                }
                
                // Replace the empty features array with our features
                const string featuresPattern = "$optionalFeatures = @()"; 
                scriptTemplate = scriptTemplate.Replace(featuresPattern, $"$optionalFeatures = @(\n{featuresBlock})");
            }
            
            // Add registry settings if needed
            if (registrySettings != null && registrySettings.Any())
            {
                var registryBlock = new StringBuilder();
                registryBlock.AppendLine("\n# Registry settings");
                
                foreach (var appEntry in registrySettings)
                {
                    string appName = appEntry.Key;
                    List<AppRegistrySetting> settings = appEntry.Value;
                    
                    if (settings == null || !settings.Any())
                    {
                        continue;
                    }
                    
                    registryBlock.AppendLine();
                    registryBlock.AppendLine($"# Registry settings for {appName}");
                    
                    foreach (var setting in settings)
                    {
                        string path = setting.Path;
                        string name = setting.Name;
                        string valueKind = GetRegTypeString(setting.ValueKind);
                        string value = setting.Value?.ToString() ?? string.Empty;
                        
                        // Check if this is a delete operation (value is null or empty)
                        bool isDelete = string.IsNullOrEmpty(value);
                        
                        if (isDelete)
                        {
                            registryBlock.AppendLine(
                                $"Remove-ItemProperty -Path '{path}' -Name '{name}' -ErrorAction SilentlyContinue");
                        }
                        else
                        {
                            // Format the value based on its type
                            string formattedValue = FormatRegistryValue(value, setting.ValueKind);
                            registryBlock.AppendLine(
                                $"Set-ItemProperty -Path '{path}' -Name '{name}' -Value {formattedValue} -Type {valueKind} -ErrorAction SilentlyContinue");
                        }
                    }
                }
                
                // Add registry settings at the end of the script, before the final log message
                const string logMessage = "Write-Log \"Bloat removal process completed\""; 
                scriptTemplate = scriptTemplate.Replace(logMessage, $"{registryBlock}\n\n{logMessage}");
            }
            
            return scriptTemplate;
        }

        /// <inheritdoc/>
        public string BuildSingleAppRemovalScript(AppInfo app)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            // Create a list with just this app's package name
            var packageNames = new List<string> { app.PackageName };
            
            // Use the appropriate build method based on app type
            string script;
            switch (app.Type)
            {
                case AppType.StandardApp:
                    script = BuildPackageRemovalScript(packageNames);
                    break;
                    
                case AppType.Capability:
                    script = BuildCapabilityRemovalScript(packageNames);
                    break;
                    
                case AppType.OptionalFeature:
                    script = BuildFeatureRemovalScript(packageNames);
                    break;
                    
                default:
                    // Default to package removal
                    script = BuildPackageRemovalScript(packageNames);
                    break;
            }
            
            // Add a custom header for this single app
            var header = new StringBuilder();
            header.AppendLine($"# Removal script for {app.Name} ({app.PackageName})");
            header.AppendLine($"# Generated on {DateTime.Now}");
            header.AppendLine();
            
            return header.ToString() + script;
        }

        /// <summary>
        /// Formats a registry value based on its type.
        /// </summary>
        /// <param name="value">The value to format.</param>
        /// <param name="valueKind">The type of the value.</param>
        /// <returns>The formatted value.</returns>
        private string FormatRegistryValue(
            string value,
            Microsoft.Win32.RegistryValueKind valueKind
        )
        {
            switch (valueKind)
            {
                case Microsoft.Win32.RegistryValueKind.String:
                case Microsoft.Win32.RegistryValueKind.ExpandString:
                    return $"\"{value}\"";

                case Microsoft.Win32.RegistryValueKind.DWord:
                case Microsoft.Win32.RegistryValueKind.QWord:
                    return value;

                case Microsoft.Win32.RegistryValueKind.Binary:
                    // Format as hex string
                    return value;

                case Microsoft.Win32.RegistryValueKind.MultiString:
                    // Format as comma-separated string
                    return $"\"{value}\"";

                default:
                    return value;
            }
        }

        /// <summary>
        /// Converts a RegistryValueKind to the corresponding reg.exe type string.
        /// </summary>
        /// <param name="valueKind">The registry value kind.</param>
        /// <returns>The reg.exe type string.</returns>
        private string GetRegTypeString(Microsoft.Win32.RegistryValueKind valueKind)
        {
            return valueKind switch
            {
                Microsoft.Win32.RegistryValueKind.String => "SZ",
                Microsoft.Win32.RegistryValueKind.ExpandString => "EXPAND_SZ",
                Microsoft.Win32.RegistryValueKind.Binary => "BINARY",
                Microsoft.Win32.RegistryValueKind.DWord => "DWORD",
                Microsoft.Win32.RegistryValueKind.MultiString => "MULTI_SZ",
                Microsoft.Win32.RegistryValueKind.QWord => "QWORD",
                _ => "SZ",
            };
        }
    }
}
