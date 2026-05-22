// File: src/Winhance.Core/Features/Common/Interfaces/IActionCommandRegistry.cs
namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Maps setting ids to the IActionCommandProvider that handles their button-driven
/// commands (e.g. Taskbar.CleanTaskbar, StartMenu.Clean*). Registered in DI from a
/// fixed table.
/// </summary>
public interface IActionCommandRegistry
{
    IActionCommandProvider? TryGet(string settingId);
}
