using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Collects diagnostic system information for log headers.
/// </summary>
public interface ISystemInfoProvider
{
    SystemInfo Collect();
}
