namespace Winhance.UI.Features.SoftwareApps.Models;

/// <summary>
/// Interface for items that can be selected in a list.
/// </summary>
public interface ISelectable
{
    bool IsSelected { get; set; }
    string Name { get; }
}
