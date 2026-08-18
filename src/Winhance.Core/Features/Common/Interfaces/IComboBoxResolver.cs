using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IComboBoxResolver
{
    int ResolveRawValuesToIndex(Setting setting, Dictionary<string, object?> rawValues);
}
