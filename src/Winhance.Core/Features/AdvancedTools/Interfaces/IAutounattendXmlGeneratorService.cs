using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.AdvancedTools.Interfaces;

public interface IAutounattendXmlGeneratorService
{
    Task<string> GenerateFromCurrentSelectionsAsync(string outputPath,
        IReadOnlyList<ConfigurationItem>? selectedWindowsApps = null);
}
