using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Optimize.Interfaces
{
    /// <summary>
    /// Service interface for managing Windows security optimization settings.
    /// Handles UAC, Windows Defender, and security-related optimizations.
    /// </summary>
    public interface ISecurityService : IDomainService
    {
        // Inherits all base functionality from IDomainService
        // Domain-specific methods can be added here as needed
    }
}
