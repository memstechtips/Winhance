using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.Common.ScriptGeneration
{
    /// <summary>
    /// Service for building PowerShell script content.
    /// </summary>
    public class PowerShellScriptBuilderService : IScriptBuilderService
    {
        private readonly IScriptTemplateProvider _templateProvider;
        private readonly ILogService _logService;

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerShellScriptBuilderService"/> class.
        /// </summary>
        /// <param name="templateProvider">The template provider.</param>
        /// <param name="logService">The logging service.</param>
        public PowerShellScriptBuilderService(
            IScriptTemplateProvider templateProvider,
            ILogService logService)
        {
            _templateProvider = templateProvider ?? throw new ArgumentNullException(nameof(templateProvider));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <inheritdoc/>
        public string BuildPackageRemovalScript(IEnumerable<string> packageNames)
        {
            if (packageNames == null || !packageNames.Any())
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Remove packages");
            
            string template = _templateProvider.GetPackageRemovalTemplate();
            
            foreach (var packageName in packageNames)
            {
                sb.AppendLine();
                sb.AppendLine($"# Remove {packageName}");
                sb.AppendLine(string.Format(template, packageName));
            }

            return sb.ToString();
        }

        /// <inheritdoc/>
        public string BuildCapabilityRemovalScript(IEnumerable<string> capabilityNames)
        {
            if (capabilityNames == null || !capabilityNames.Any())
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Remove capabilities");
            
            string template = _templateProvider.GetCapabilityRemovalTemplate();
            
            foreach (var capabilityName in capabilityNames)
            {
                sb.AppendLine();
                sb.AppendLine($"# Remove {capabilityName}");
                sb.AppendLine(string.Format(template, capabilityName));
            }

            return sb.ToString();
        }

        /// <inheritdoc/>
        public string BuildFeatureRemovalScript(IEnumerable<string> featureNames)
        {
            if (featureNames == null || !featureNames.Any())
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Disable Optional Features");
            
            string template = _templateProvider.GetFeatureRemovalTemplate();
            
            foreach (var featureName in featureNames)
            {
                sb.AppendLine();
                sb.AppendLine($"# Disable {featureName}");
                sb.AppendLine(string.Format(template, featureName));
            }

            return sb.ToString();
        }

        /// <inheritdoc/>
        public string BuildRegistryScript(Dictionary<string, List<AppRegistrySetting>> registrySettings)
        {
            if (registrySettings == null || !registrySettings.Any())
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            sb.AppendLine("# Registry settings");

            foreach (var appEntry in registrySettings)
            {
                string appName = appEntry.Key;
                List<AppRegistrySetting> settings = appEntry.Value;

                if (settings == null || !settings.Any())
                {
                    continue;
                }

                sb.AppendLine();
                sb.AppendLine($"# Registry settings for {appName}");

                foreach (var setting in settings)
                {
                    string path = setting.Path;
                    string name = setting.Name;
                    string valueKind = GetRegTypeString(setting.ValueKind);
                    string value = setting.Value?.ToString() ?? string.Empty;

                    // Check if this is a delete operation (value is null or empty)
                    bool isDelete = string.IsNullOrEmpty(value);
                    string template = _templateProvider.GetRegistrySettingTemplate(isDelete);

                    if (isDelete)
                    {
                        sb.AppendLine(string.Format(template, path, name));
                    }
                    else
                    {
                        // Format the value based on its type
                        string formattedValue = FormatRegistryValue(value, setting.ValueKind);
                        sb.AppendLine(string.Format(template, path, name, valueKind, formattedValue));
                    }
                }
            }

            return sb.ToString();
        }

        /// <inheritdoc/>
        public string BuildCompleteRemovalScript(
            IEnumerable<string> packageNames,
            IEnumerable<string> capabilityNames,
            IEnumerable<string> featureNames,
            Dictionary<string, List<AppRegistrySetting>> registrySettings,
            Dictionary<string, string[]> subPackages)
        {
            var sb = new StringBuilder();

            // Add script header
            sb.Append(_templateProvider.GetScriptHeader("BloatRemoval"));

            // Add packages section
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
            
            // Update the packages array in the script
            if (allPackages.Any())
            {
                sb.AppendLine("# Remove packages");
                sb.AppendLine("$packages = @(");
                
                for (int i = 0; i < allPackages.Count; i++)
                {
                    string package = allPackages[i];
                    if (i < allPackages.Count - 1)
                    {
                        sb.AppendLine($"    '{package}',");
                    }
                    else
                    {
                        sb.AppendLine($"    '{package}'");
                    }
                }
                
                sb.AppendLine(")");
                sb.AppendLine();
                sb.AppendLine("foreach ($package in $packages) {");
                sb.AppendLine("    Get-AppxPackage -AllUsers -Name $package | ");
                sb.AppendLine("    ForEach-Object {");
                sb.AppendLine("        Remove-AppxPackage -Package $_.PackageFullName -ErrorAction SilentlyContinue");
                sb.AppendLine("    }");
                sb.AppendLine("    Get-AppxProvisionedPackage -Online | ");
                sb.AppendLine("    Where-Object { $_.DisplayName -eq $package } | ");
                sb.AppendLine("    ForEach-Object {");
                sb.AppendLine("        Remove-AppxProvisionedPackage -Online -PackageName $_.PackageName -ErrorAction SilentlyContinue");
                sb.AppendLine("    }");
                sb.AppendLine("}");
                sb.AppendLine();
            }
            
            // Add capabilities section
            if (capabilityNames != null && capabilityNames.Any())
            {
                sb.AppendLine("# Remove capabilities");
                sb.AppendLine("$capabilities = @(");
                
                var capabilitiesList = capabilityNames.ToList();
                for (int i = 0; i < capabilitiesList.Count; i++)
                {
                    string capability = capabilitiesList[i];
                    if (i < capabilitiesList.Count - 1)
                    {
                        sb.AppendLine($"    '{capability}',");
                    }
                    else
                    {
                        sb.AppendLine($"    '{capability}'");
                    }
                }
                
                sb.AppendLine(")");
                sb.AppendLine();
                sb.AppendLine("foreach ($capability in $capabilities) {");
                sb.AppendLine("    Get-WindowsCapability -Online | Where-Object { $_.Name -like \"$capability*\" } | Remove-WindowsCapability -Online");
                sb.AppendLine("}");
                sb.AppendLine();
            }
            
            // Add features section
            if (featureNames != null && featureNames.Any())
            {
                sb.AppendLine("# Disable Optional Features");
                sb.AppendLine("$optionalFeatures = @(");
                
                var featuresList = featureNames.ToList();
                for (int i = 0; i < featuresList.Count; i++)
                {
                    string feature = featuresList[i];
                    if (i < featuresList.Count - 1)
                    {
                        sb.AppendLine($"    '{feature}',");
                    }
                    else
                    {
                        sb.AppendLine($"    '{feature}'");
                    }
                }
                
                sb.AppendLine(")");
                sb.AppendLine();
                sb.AppendLine("foreach ($feature in $optionalFeatures) {");
                sb.AppendLine("    Write-Host \"Disabling optional feature: $feature\" -ForegroundColor Yellow");
                sb.AppendLine("    Disable-WindowsOptionalFeature -Online -FeatureName $feature -NoRestart | Out-Null");
                sb.AppendLine("}");
                sb.AppendLine();
            }
            
            // Add registry settings
            if (registrySettings != null && registrySettings.Any())
            {
                sb.AppendLine("# Registry settings");
                
                foreach (var appEntry in registrySettings)
                {
                    string appName = appEntry.Key;
                    List<AppRegistrySetting> settings = appEntry.Value;
                    
                    if (settings == null || !settings.Any())
                    {
                        continue;
                    }
                    
                    sb.AppendLine();
                    sb.AppendLine($"# Registry settings for {appName}");
                    
                    foreach (var setting in settings)
                    {
                        string path = setting.Path;
                        string name = setting.Name;
                        string valueKind = GetRegTypeString(setting.ValueKind);
                        string value = setting.Value?.ToString() ?? string.Empty;
                        
                        // Check if this is a delete operation (value is null or empty)
                        bool isDelete = string.IsNullOrEmpty(value);
                        string template = _templateProvider.GetRegistrySettingTemplate(isDelete);
                        
                        if (isDelete)
                        {
                            sb.AppendLine(string.Format(template, path, name));
                        }
                        else
                        {
                            // Format the value based on its type
                            string formattedValue = FormatRegistryValue(value, setting.ValueKind);
                            sb.AppendLine(string.Format(template, path, name, valueKind, formattedValue));
                        }
                    }
                }
                
                sb.AppendLine();
            }
            
            // Add script footer
            sb.Append(_templateProvider.GetScriptFooter());
            
            return sb.ToString();
        }

        /// <inheritdoc/>
        public string BuildSingleAppRemovalScript(AppInfo app)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            var sb = new StringBuilder();
            
            sb.AppendLine($"# Removal script for {app.Name} ({app.PackageName})");
            sb.AppendLine($"# Generated on {DateTime.Now}");
            sb.AppendLine();
            sb.AppendLine("try {");
            sb.AppendLine($"    Write-Host \"Removing {app.Name}...\" -ForegroundColor Yellow");
            sb.AppendLine();
            
            // Add the appropriate removal command based on the app type
            switch (app.Type)
            {
                case AppType.StandardApp:
                    string packageTemplate = _templateProvider.GetPackageRemovalTemplate();
                    sb.AppendLine("    # Remove the app package");
                    sb.AppendLine("    " + string.Format(packageTemplate, app.PackageName));
                    break;
                    
                case AppType.Capability:
                    string capabilityTemplate = _templateProvider.GetCapabilityRemovalTemplate();
                    sb.AppendLine("    # Remove the capability");
                    sb.AppendLine("    " + string.Format(capabilityTemplate, app.PackageName));
                    break;
                    
                case AppType.OptionalFeature:
                    string featureTemplate = _templateProvider.GetFeatureRemovalTemplate();
                    sb.AppendLine("    # Disable the optional feature");
                    sb.AppendLine("    " + string.Format(featureTemplate, app.PackageName));
                    break;
                    
                default:
                    // Default to package removal
                    string defaultTemplate = _templateProvider.GetPackageRemovalTemplate();
                    sb.AppendLine("    # Remove the app");
                    sb.AppendLine("    " + string.Format(defaultTemplate, app.PackageName));
                    break;
            }
            
            sb.AppendLine();
            sb.AppendLine($"    Write-Host \"{app.Name} removed successfully.\" -ForegroundColor Green");
            sb.AppendLine("} catch {");
            sb.AppendLine($"    Write-Host \"Error removing {app.Name}: $($_.Exception.Message)\" -ForegroundColor Red");
            sb.AppendLine("}");
            
            return sb.ToString();
        }

        /// <summary>
        /// Formats a registry value based on its type.
        /// </summary>
        /// <param name="value">The value to format.</param>
        /// <param name="valueKind">The type of the value.</param>
        /// <returns>The formatted value.</returns>
        private string FormatRegistryValue(string value, Microsoft.Win32.RegistryValueKind valueKind)
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
                _ => "SZ"
            };
        }
    }
}