using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.Common.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>
/// A slim bar shown at the bottom of the window while one or more applied settings are still waiting
/// on an Explorer restart. Sits in the same slot as the task-progress bars so it inherits their width,
/// margins and UI-zoom scaling.
/// </summary>
public sealed partial class PendingRestartBar : UserControl
{
    public PendingRestartBar()
    {
        this.InitializeComponent();
    }

    /// <summary>
    /// The backing ViewModel. Assigned by the host after DI resolves it; setting it wires the control's
    /// DataContext, which is what every binding in the XAML resolves against.
    /// </summary>
    public PendingRestartViewModel? ViewModel
    {
        get => DataContext as PendingRestartViewModel;
        set => DataContext = value;
    }
}
