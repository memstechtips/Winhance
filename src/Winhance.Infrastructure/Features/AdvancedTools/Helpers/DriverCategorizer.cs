using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.AdvancedTools.Helpers
{
    public static class DriverCategorizer
    {
        private static readonly HashSet<string> StorageClasses = new(StringComparer.OrdinalIgnoreCase)
        {
            "SCSIAdapter",
            "hdc",
            "HDC"
        };

        private static readonly HashSet<string> StorageFileNameKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "iaahci",
            "iastor",
            "iastorac",
            "iastora",
            "iastorv",
            "vmd",
            "irst",
            "rst"
        };

        public static bool IsStorageDriver(string infPath, ILogService logService)
        {
            try
            {
                var fileName = Path.GetFileName(infPath).ToLowerInvariant();

                if (StorageFileNameKeywords.Any(keyword => fileName.Contains(keyword)))
                {
                    logService.LogInformation($"Storage driver detected (filename): {Path.GetFileName(infPath)}");
                    return true;
                }

                string fileContent;
                try
                {
                    fileContent = File.ReadAllText(infPath, Encoding.Unicode);
                }
                catch
                {
                    fileContent = File.ReadAllText(infPath, Encoding.UTF8);
                }

                using var reader = new StringReader(fileContent);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var trimmedLine = line.Trim();

                    if (trimmedLine.StartsWith("Class", StringComparison.OrdinalIgnoreCase) && trimmedLine.Contains("="))
                    {
                        var parts = trimmedLine.Split('=');
                        if (parts.Length >= 2)
                        {
                            var className = parts[1].Trim();
                            if (StorageClasses.Contains(className))
                            {
                                logService.LogInformation($"Storage driver detected (class={className}): {Path.GetFileName(infPath)}");
                                return true;
                            }
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                logService.LogWarning($"Could not categorize driver {Path.GetFileName(infPath)}: {ex.Message}");
                return false;
            }
        }

        public static int CategorizeAndCopyDrivers(
            string sourceDirectory,
            string winpeDriverPath,
            string oemDriverPath,
            ILogService logService,
            string? workingDirectoryToExclude = null)
        {
            var infFiles = Directory.GetFiles(sourceDirectory, "*.inf", SearchOption.AllDirectories);

            if (infFiles.Length == 0)
            {
                logService.LogWarning($"No .inf files found in: {sourceDirectory}");
                return 0;
            }

            var validInfFiles = infFiles;

            if (!string.IsNullOrEmpty(workingDirectoryToExclude))
            {
                validInfFiles = infFiles
                    .Where(inf => !inf.StartsWith(workingDirectoryToExclude, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                int excludedCount = infFiles.Length - validInfFiles.Length;
                if (excludedCount > 0)
                {
                    logService.LogInformation($"Excluded {excludedCount} driver(s) from working directory");
                }
            }

            if (validInfFiles.Length == 0)
            {
                logService.LogWarning("No valid drivers found after filtering");
                return 0;
            }

            logService.LogInformation($"Found {validInfFiles.Length} driver(s) to categorize");
            int copiedCount = 0;
            var processedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var infFile in validInfFiles)
            {
                try
                {
                    var sourceDir = Path.GetDirectoryName(infFile)!;

                    if (processedFolders.Contains(sourceDir))
                        continue;

                    processedFolders.Add(sourceDir);

                    var isStorage = IsStorageDriver(infFile, logService);
                    var targetBase = isStorage ? winpeDriverPath : oemDriverPath;

                    var folderName = Path.GetFileName(sourceDir);
                    var targetDirectory = Path.Combine(targetBase, folderName);

                    int counter = 1;
                    while (Directory.Exists(targetDirectory) && counter < 100)
                    {
                        targetDirectory = Path.Combine(targetBase, $"{folderName}_{counter}");
                        counter++;
                    }

                    Directory.CreateDirectory(targetDirectory);

                    foreach (var file in Directory.GetFiles(sourceDir))
                    {
                        var targetFile = Path.Combine(targetDirectory, Path.GetFileName(file));
                        File.Copy(file, targetFile, overwrite: true);
                    }

                    copiedCount++;
                    logService.LogInformation($"Copied driver: {folderName}");
                }
                catch (Exception ex)
                {
                    logService.LogError($"Failed to copy driver {Path.GetFileName(infFile)}: {ex.Message}", ex);
                }
            }

            return copiedCount;
        }

        public static void MergeDriverDirectory(string sourceDirectory, string targetDirectory, ILogService logService)
        {
            if (!Directory.Exists(sourceDirectory))
            {
                logService.LogWarning($"Source directory does not exist: {sourceDirectory}");
                return;
            }

            Directory.CreateDirectory(targetDirectory);

            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                var fileName = Path.GetFileName(file);
                var targetFile = Path.Combine(targetDirectory, fileName);

                if (File.Exists(targetFile))
                {
                    logService.LogInformation($"File already exists, skipping: {fileName}");
                    continue;
                }

                File.Copy(file, targetFile, overwrite: false);
            }

            foreach (var dir in Directory.GetDirectories(sourceDirectory))
            {
                var dirName = Path.GetFileName(dir);
                var targetSubDir = Path.Combine(targetDirectory, dirName);
                MergeDriverDirectory(dir, targetSubDir, logService);
            }
        }
    }
}
