using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class VersionService : IVersionService
    {
        private readonly ILogService _logService;
        private readonly HttpClient _httpClient;
        private readonly string _latestReleaseApiUrl = "https://api.github.com/repos/memstechtips/Winhance/releases/latest";
        private readonly string _latestReleaseDownloadUrl = "https://github.com/memstechtips/Winhance/releases/latest/download/Winhance.Installer.exe";
        private readonly string _userAgent = "Winhance-Update-Checker";
        
        public VersionService(ILogService logService)
        {
            _logService = logService;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", _userAgent);
        }
        
        public VersionInfo GetCurrentVersion()
        {
            try
            {
                // Get the assembly version
                Assembly assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                string? location = assembly.Location;
                
                if (string.IsNullOrEmpty(location))
                {
                    _logService.Log(LogLevel.Error, "Could not determine assembly location for version check");
                    return CreateDefaultVersion();
                }
                
                // Get the InformationalVersion which can include the -beta tag
                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(location);
                string version = versionInfo.ProductVersion ?? versionInfo.FileVersion ?? "v0.0.0";
                
                // Trim any build metadata (anything after the + symbol)
                int plusIndex = version.IndexOf('+');
                if (plusIndex > 0)
                {
                    version = version.Substring(0, plusIndex);
                }
                
                // If the version doesn't start with 'v', add it
                if (!version.StartsWith("v"))
                {
                    version = $"v{version}";
                }
                
                return VersionInfo.FromTag(version);
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error getting current version: {ex.Message}", ex);
                return CreateDefaultVersion();
            }
        }
        
        public async Task<VersionInfo> CheckForUpdateAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Checking for updates...");
                
                // Get the latest release information from GitHub API
                HttpResponseMessage response = await _httpClient.GetAsync(_latestReleaseApiUrl);
                response.EnsureSuccessStatusCode();
                
                string responseBody = await response.Content.ReadAsStringAsync();
                using JsonDocument doc = JsonDocument.Parse(responseBody);
                
                // Extract the tag name (version) from the response
                string tagName = doc.RootElement.GetProperty("tag_name").GetString() ?? "v0.0.0";
                string htmlUrl = doc.RootElement.GetProperty("html_url").GetString() ?? string.Empty;
                DateTime publishedAt = doc.RootElement.TryGetProperty("published_at", out JsonElement publishedElement) && 
                                      DateTime.TryParse(publishedElement.GetString(), out DateTime published) 
                                      ? published 
                                      : DateTime.MinValue;
                
                VersionInfo latestVersion = VersionInfo.FromTag(tagName);
                latestVersion.DownloadUrl = _latestReleaseDownloadUrl;
                
                // Compare with current version
                VersionInfo currentVersion = GetCurrentVersion();
                latestVersion.IsUpdateAvailable = latestVersion.IsNewerThan(currentVersion);
                
                _logService.Log(LogLevel.Info, $"Current version: {currentVersion.Version}, Latest version: {latestVersion.Version}, Update available: {latestVersion.IsUpdateAvailable}");
                
                return latestVersion;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error checking for updates: {ex.Message}", ex);
                return new VersionInfo { IsUpdateAvailable = false };
            }
        }
        
        public async Task DownloadAndInstallUpdateAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Downloading update...");
                
                // Create a temporary file to download the installer
                string tempPath = Path.Combine(Path.GetTempPath(), "Winhance.Installer.exe");
                
                // Download the installer
                byte[] installerBytes = await _httpClient.GetByteArrayAsync(_latestReleaseDownloadUrl);
                await File.WriteAllBytesAsync(tempPath, installerBytes);
                
                _logService.Log(LogLevel.Info, $"Update downloaded to {tempPath}, launching installer...");
                
                // Launch the installer
                Process.Start(new ProcessStartInfo
                {
                    FileName = tempPath,
                    UseShellExecute = true
                });
                
                _logService.Log(LogLevel.Info, "Installer launched successfully");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error downloading or installing update: {ex.Message}", ex);
                throw;
            }
        }
        
        private VersionInfo CreateDefaultVersion()
        {
            // Create a default version based on the current date
            DateTime now = DateTime.Now;
            string versionTag = $"v{now.Year - 2000:D2}.{now.Month:D2}.{now.Day:D2}";
            
            return new VersionInfo
            {
                Version = versionTag,
                ReleaseDate = now
            };
        }
    }
}
