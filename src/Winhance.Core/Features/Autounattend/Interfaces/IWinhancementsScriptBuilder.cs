using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.Core.Features.Autounattend.Interfaces;

public interface IWinhancementsScriptBuilder
{
    Task<string> BuildAsync(SelectionSet set, IReadOnlyDictionary<string, IReadOnlyList<Setting>> byFeature);
}
