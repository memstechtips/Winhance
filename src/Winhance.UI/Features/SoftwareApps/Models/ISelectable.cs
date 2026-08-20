namespace Winhance.UI.Features.SoftwareApps.Models;

public interface ISelectable
{
    bool IsSelected { get; set; }
    string Name { get; }
}
