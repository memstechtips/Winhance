using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.Core.Features.AdvancedTools.Interfaces;

public interface IAutounattendScriptBuilder
{
    Task<string> BuildAsync(SelectionSet set, IReadOnlyDictionary<string, IReadOnlyList<Setting>> byFeature);
}
