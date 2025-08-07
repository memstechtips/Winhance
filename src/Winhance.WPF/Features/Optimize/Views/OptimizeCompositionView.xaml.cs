using System.Windows.Controls;

namespace Winhance.WPF.Features.Optimize.Views
{
    /// <summary>
    /// Simple composition view that hosts individual optimization feature UserControls.
    /// Each feature UserControl gets its ViewModel injected directly via DI.
    /// No complex container ViewModel needed - follows SOLID principles.
    /// </summary>
    public partial class OptimizeCompositionView : UserControl
    {
        public OptimizeCompositionView()
        {
            InitializeComponent();
        }
    }
}
