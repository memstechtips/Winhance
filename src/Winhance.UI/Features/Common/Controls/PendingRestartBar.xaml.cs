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

    public PendingRestartViewModel? ViewModel
    {
        get => DataContext as PendingRestartViewModel;
        set => DataContext = value;
    }
}
