using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IComboBoxResolver
{
    int ResolveRawValuesToIndex(Setting setting, Dictionary<string, object?> rawValues);
}
