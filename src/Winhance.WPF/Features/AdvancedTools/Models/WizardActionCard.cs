using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Winhance.WPF.Features.AdvancedTools.Models
{
    public partial class WizardActionCard : ObservableObject
    {
        [ObservableProperty]
        private string _icon = string.Empty;

        [ObservableProperty]
        private string _title = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private Brush _descriptionForeground = (Brush)Application.Current.Resources["SecondaryTextColor"];

        [ObservableProperty]
        private string _buttonText = string.Empty;

        [ObservableProperty]
        private ICommand? _buttonCommand;

        [ObservableProperty]
        private bool _isEnabled = true;

        [ObservableProperty]
        private bool _isComplete;

        [ObservableProperty]
        private bool _hasFailed;

        [ObservableProperty]
        private bool _isProcessing;

        [ObservableProperty]
        private double _opacity = 1.0;

        partial void OnIsCompleteChanged(bool value)
        {
            if (value)
            {
                IsProcessing = false;
                HasFailed = false;
            }
        }

        partial void OnHasFailedChanged(bool value)
        {
            if (value)
            {
                IsProcessing = false;
                IsComplete = false;
            }
        }

        partial void OnIsProcessingChanged(bool value)
        {
            if (value)
            {
                IsComplete = false;
                HasFailed = false;
            }
        }
    }
}
