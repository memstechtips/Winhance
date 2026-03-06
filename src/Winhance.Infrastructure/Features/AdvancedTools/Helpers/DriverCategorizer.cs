using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.AdvancedTools.Helpers;

public class DriverCategorizer(ILogService logService, IFileSystemService fileSystemService) : IDriverCategorizer
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

    public bool IsStorageDriver(string infPath)
    {
        try
        {
            var fileName = fileSystemService.GetFileName(infPath).ToLowerInvariant();

            if (StorageFileNameKeywords.Any(keyword => fileName.Contains(keyword)))
            {
                logService.LogInformation($"Storage driver detected (filename): {fileSystemService.GetFileName(infPath)}");
                return true;
            }

            string fileContent;
            try
            {
                fileContent = fileSystemService.ReadAllText(infPath, Encoding.Unicode);
            }
            catch
            {
                fileContent = fileSystemService.ReadAllText(infPath, Encoding.UTF8);
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
                            logService.LogInformation($"Storage driver detected (class={className}): {fileSystemService.GetFileName(infPath)}");
                            return true;
                        }
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            logService.LogWarning($"Could not categorize driver {fileSystemService.GetFileName(infPath)}: {ex.Message}");
            return false;
        }
    }

    public int CategorizeAndCopyDrivers(
        string sourceDirectory,
        string winpeDriverPath,
        string oemDriverPath,
        string? workingDirectoryToExclude = null)
    {
        var infFiles = fileSystemService.GetFiles(sourceDirectory, "*.inf", SearchOption.AllDirectories);

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
                var sourceDir = fileSystemService.GetDirectoryName(infFile)!;

                if (processedFolders.Contains(sourceDir))
                    continue;

                processedFolders.Add(sourceDir);

                var isStorage = IsStorageDriver(infFile);
                var targetBase = isStorage ? winpeDriverPath : oemDriverPath;

                var folderName = fileSystemService.GetFileName(sourceDir);
                var targetDirectory = fileSystemService.CombinePath(targetBase, folderName);

                int counter = 1;
                while (fileSystemService.DirectoryExists(targetDirectory) && counter < 100)
                {
                    targetDirectory = fileSystemService.CombinePath(targetBase, $"{folderName}_{counter}");
                    counter++;
                }

                fileSystemService.CreateDirectory(targetDirectory);

                foreach (var file in fileSystemService.GetFiles(sourceDir))
                {
                    var targetFile = fileSystemService.CombinePath(targetDirectory, fileSystemService.GetFileName(file));
                    fileSystemService.CopyFile(file, targetFile, overwrite: true);
                }

                copiedCount++;
                logService.LogInformation($"Copied driver: {folderName}");
            }
            catch (Exception ex)
            {
                logService.LogError($"Failed to copy driver {fileSystemService.GetFileName(infFile)}: {ex.Message}", ex);
            }
        }

        return copiedCount;
    }

}
