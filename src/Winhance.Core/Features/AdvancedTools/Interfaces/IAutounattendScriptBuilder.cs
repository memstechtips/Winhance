using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.AdvancedTools.Interfaces;

public interface IAutounattendScriptBuilder
{
    Task<string> BuildWinhancementsScriptAsync(
        UnifiedConfigurationFile config,
        IReadOnlyDictionary<string, IReadOnlyList<Setting>> allSettings);
}
