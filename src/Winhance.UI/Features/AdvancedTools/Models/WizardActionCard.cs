using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace Winhance.UI.Features.AdvancedTools.Models;

/// <summary>
/// Represents an action card in the WIM Utility wizard.
/// </summary>
public partial class WizardActionCard : ObservableObject
{
    [ObservableProperty]
    private string _icon = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private Brush? _descriptionForeground;

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
