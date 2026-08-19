using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.Core.Features.AdvancedTools.Interfaces;

public interface IAutounattendScriptBuilder
{
    Task<string> BuildWinhancementsScriptAsync(
        WinhanceConfigFile config,
        IReadOnlyDictionary<string, IReadOnlyList<Setting>> allSettings);

    Task<string> BuildAsync(SelectionSet set, IReadOnlyDictionary<string, IReadOnlyList<Setting>> byFeature);
}
