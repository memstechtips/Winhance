namespace Winhance.Core.Features.AdvancedTools.Interfaces;

public interface IAutounattendXmlGeneratorService
{
    Task<string> GenerateFromCurrentSelectionsAsync(string outputPath);
}
