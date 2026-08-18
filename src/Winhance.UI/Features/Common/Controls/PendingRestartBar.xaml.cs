using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.Common.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

// Sits in the same slot as the task-progress bars so it inherits their width, margins and UI-zoom scaling.
public sealed partial class PendingRestartBar : UserControl
{
    public PendingRestartBar()
    {
        this.InitializeComponent();
    }

    // Setting it wires the control's DataContext, which every binding in the XAML resolves against.
    public PendingRestartViewModel? ViewModel
    {
        get => DataContext as PendingRestartViewModel;
        set => DataContext = value;
    }
}
