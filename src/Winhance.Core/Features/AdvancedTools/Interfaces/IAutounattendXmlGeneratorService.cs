using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.AdvancedTools.Interfaces;

public interface IAutounattendXmlGeneratorService
{
    Task<string> GenerateFromCurrentSelectionsAsync(string outputPath,
        IReadOnlyList<ConfigurationItem>? selectedWindowsApps = null);

    /// <summary>
    /// Builder Autounattend Save: generate autounattend.xml from a pre-built configuration
    /// (the UI-state config produced in Builder mode) rather than reading the live system.
    /// This is the path that lets the user's authored choices — not the current machine —
    /// drive the XML, fixing the wrong-values problem (#639).
    /// </summary>
    Task<string> GenerateFromConfigAsync(UnifiedConfigurationFile config, string outputPath);
}
