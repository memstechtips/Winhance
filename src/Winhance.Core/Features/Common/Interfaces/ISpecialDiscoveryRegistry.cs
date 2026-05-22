// File: src/Winhance.Core/Features/Common/Interfaces/ISpecialDiscoveryRegistry.cs
using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Used by SystemSettingsDiscoveryService to iterate every handler that
/// implements DiscoverSpecialSettingsAsync, so each can declare which raw values
/// it wants to inject into the discovery results.
/// </summary>
public interface ISpecialDiscoveryRegistry
{
    /// <summary>Every registered discovery-capable handler, in registration order.</summary>
    IEnumerable<ISpecialSettingHandler> All { get; }
}
