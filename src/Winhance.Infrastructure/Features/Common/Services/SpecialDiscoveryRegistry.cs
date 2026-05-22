// File: src/Winhance.Infrastructure/Features/Common/Services/SpecialDiscoveryRegistry.cs
using System.Collections.Generic;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

public sealed class SpecialDiscoveryRegistry(IReadOnlyList<ISpecialSettingHandler> handlers)
    : ISpecialDiscoveryRegistry
{
    public IEnumerable<ISpecialSettingHandler> All => handlers;
}
