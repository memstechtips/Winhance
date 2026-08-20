namespace Winhance.Core.Features.AdvancedTools.Interfaces;

public interface IDriverCategorizer
{
    bool IsStorageDriver(string infPath);
    int CategorizeAndCopyDrivers(string sourceDirectory, string winpeDriverPath, string oemDriverPath, string? workingDirectoryToExclude = null);
}
