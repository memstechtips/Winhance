namespace Winhance.Core.Features.AdvancedTools.Interfaces;

public interface IDriverCategorizer
{
    bool IsStorageDriver(string infPath);
    int CategorizeAndCopyDrivers(string sourceDirectory, string winpeDriverPath, string oemDriverPath, string? workingDirectoryToExclude = null);

    // Returns how many packages are staged in total, moved and left alike.
    int MoveStorageDrivers(string oemDriverPath, string winpeDriverPath);
}
