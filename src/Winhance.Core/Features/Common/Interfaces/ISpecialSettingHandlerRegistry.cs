// File: src/Winhance.Core/Features/Common/Interfaces/ISpecialSettingHandlerRegistry.cs
namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Maps setting ids to the ISpecialSettingHandler that handles them at apply time.
/// Registered in DI from a fixed table (see DomainServicesExtensions).
/// </summary>
public interface ISpecialSettingHandlerRegistry
{
    /// <summary>Returns the handler for the given setting id, or null if none registered.</summary>
    ISpecialSettingHandler? TryGet(string settingId);
}
