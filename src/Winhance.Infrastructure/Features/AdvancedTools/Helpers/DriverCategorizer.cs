using System.Text;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.AdvancedTools.Helpers;

internal class DriverCategorizer(ILogService logService, IFileSystemService fileSystemService) : IDriverCategorizer
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

                if (trimmedLine.StartsWith("Class", StringComparison.OrdinalIgnoreCase) && trimmedLine.Contains('='))
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

        foreach (var package in PackageRoots(validInfFiles))
        {
            try
            {
                var targetBase = IsStoragePackage(package, validInfFiles) ? winpeDriverPath : oemDriverPath;
                CopyTree(package, AvailableTarget(targetBase, package));

                copiedCount++;
                logService.LogInformation($"Copied driver package: {fileSystemService.GetFileName(package)}");
            }
            catch (Exception ex)
            {
                logService.LogError($"Failed to copy driver package {fileSystemService.GetFileName(package)}: {ex.Message}", ex);
            }
        }

        return copiedCount;
    }

    // DISM exported straight into the OEM staging folder; only the boot-critical storage
    // packages move to the media-root folder Setup scans, so the big set is never written twice.
    public int MoveStorageDrivers(string oemDriverPath, string winpeDriverPath)
    {
        var infFiles = fileSystemService.GetFiles(oemDriverPath, "*.inf", SearchOption.AllDirectories);
        var packages = PackageRoots(infFiles);

        // A loose INF at the staging root makes the root itself a package; never move that.
        foreach (var package in packages.Where(package =>
            !package.Equals(oemDriverPath, StringComparison.OrdinalIgnoreCase) && IsStoragePackage(package, infFiles)))
        {
            try
            {
                fileSystemService.CreateDirectory(winpeDriverPath);
                fileSystemService.MoveDirectory(package, AvailableTarget(winpeDriverPath, package));
                logService.LogInformation($"Moved storage driver package: {fileSystemService.GetFileName(package)}");
            }
            catch (Exception ex)
            {
                logService.LogError($"Failed to move storage driver package {fileSystemService.GetFileName(package)}: {ex.Message}", ex);
            }
        }

        return packages.Count;
    }

    private bool IsStoragePackage(string package, string[] infFiles) =>
        infFiles.Where(inf => IsUnder(inf, package)).Any(IsStorageDriver);

    private string AvailableTarget(string targetBase, string package)
    {
        var folderName = fileSystemService.GetFileName(package);
        var target = fileSystemService.CombinePath(targetBase, folderName);
        for (var counter = 1; fileSystemService.DirectoryExists(target) && counter < 100; counter++)
            target = fileSystemService.CombinePath(targetBase, $"{folderName}_{counter}");
        return target;
    }

    // A DISM export nests payload and further INFs inside each package folder, so the unit to
    // copy is the outermost INF-bearing directory. Flattening it dropped subfolder payload
    // (pnputil then fails with "The system cannot find the file specified") and staged the inner
    // INFs again as fragments.
    private List<string> PackageRoots(string[] infFiles)
    {
        var roots = new List<string>();
        foreach (var dir in infFiles
            .Select(inf => fileSystemService.GetDirectoryName(inf)!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(d => d.Length))
        {
            if (!roots.Any(root => IsUnder(dir, root)))
                roots.Add(dir);
        }

        return roots;
    }

    private static bool IsUnder(string path, string ancestor) =>
        path.Equals(ancestor, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(ancestor + "\\", StringComparison.OrdinalIgnoreCase);

    private void CopyTree(string sourceDirectory, string targetDirectory)
    {
        fileSystemService.CreateDirectory(targetDirectory);
        foreach (var file in fileSystemService.GetFiles(sourceDirectory))
            fileSystemService.CopyFile(file, fileSystemService.CombinePath(targetDirectory, fileSystemService.GetFileName(file)), overwrite: true);
        foreach (var sub in fileSystemService.GetDirectories(sourceDirectory))
            CopyTree(sub, fileSystemService.CombinePath(targetDirectory, fileSystemService.GetFileName(sub)));
    }
}
