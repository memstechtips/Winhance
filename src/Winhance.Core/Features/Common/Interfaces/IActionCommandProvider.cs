using System.Collections.Generic;
using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Implemented by domain services that support named action commands.
/// Replaces reflection-based method dispatch with a typed pattern.
/// </summary>
public interface IActionCommandProvider
{
    /// <summary>
    /// Gets the set of action command names this service supports.
    /// </summary>
    IReadOnlySet<string> SupportedCommands { get; }

    /// <summary>
    /// Executes the named action command.
    /// </summary>
    Task ExecuteCommandAsync(string commandName);
}
