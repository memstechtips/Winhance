// File: src/Winhance.Infrastructure/Features/Common/Services/ActionCommandRegistry.cs
using System.Collections.Generic;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

public sealed class ActionCommandRegistry(IReadOnlyDictionary<string, IActionCommandProvider> providers)
    : IActionCommandRegistry
{
    public IActionCommandProvider? TryGet(string settingId)
        => providers.TryGetValue(settingId, out var p) ? p : null;
}
