namespace Winhance.Core.Features.AdvancedTools.Interfaces
{
    /// <summary>
    /// Categorizes hardware drivers as storage (WinPE) or OEM and copies them to target directories.
    /// </summary>
    public interface IDriverCategorizer
    {
        bool IsStorageDriver(string infPath);
        int CategorizeAndCopyDrivers(string sourceDirectory, string winpeDriverPath, string oemDriverPath, string? workingDirectoryToExclude = null);
    }
}
