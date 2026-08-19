using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.AdvancedTools.Interfaces;

public interface IAutounattendXmlGeneratorService
{
    Task<string> GenerateFromCurrentSelectionsAsync(string outputPath,
        IReadOnlyList<ConfigurationItem>? selectedWindowsApps = null);

    // Builder mode: the user's authored config drives the XML, not the live machine (#639).
    Task<string> GenerateFromConfigAsync(WinhanceConfigFile config, string outputPath);
}
