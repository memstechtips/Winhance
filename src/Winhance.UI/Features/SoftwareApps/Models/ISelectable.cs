namespace Winhance.UI.Features.SoftwareApps.Models;

public interface ISelectable
{
    bool IsSelected { get; set; }
    bool IsSelectable { get; }
    string Name { get; }
}
