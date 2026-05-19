// File: src/Winhance.Infrastructure/Features/Common/Services/SpecialSettingHandlerRegistry.cs
using System.Collections.Generic;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

public sealed class SpecialSettingHandlerRegistry(IReadOnlyDictionary<string, ISpecialSettingHandler> handlers)
    : ISpecialSettingHandlerRegistry
{
    public ISpecialSettingHandler? TryGet(string settingId)
        => handlers.TryGetValue(settingId, out var h) ? h : null;
}
