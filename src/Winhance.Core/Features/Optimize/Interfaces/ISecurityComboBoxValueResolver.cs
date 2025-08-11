using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Optimize.Interfaces
{
    /// <summary>
    /// Domain interface for resolving Security-related ComboBox values.
    /// Follows Interface Segregation Principle by providing Security-specific contract.
    /// </summary>
    public interface ISecurityComboBoxValueResolver : IComboBoxValueResolver
    {
        // Security domain might have specific methods later
        // This interface extends the base resolver for Security domain
    }
}
