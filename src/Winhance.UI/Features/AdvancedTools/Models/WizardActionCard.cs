using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace Winhance.UI.Features.AdvancedTools.Models;

/// <summary>
/// Represents an action card in the WIM Utility wizard.
/// </summary>
public partial class WizardActionCard : ObservableObject
{
    public WizardActionCard()
    {
        Icon = string.Empty;
        IconPath = string.Empty;
        Title = string.Empty;
        Description = string.Empty;
        ButtonText = string.Empty;
        IsEnabled = true;
        Opacity = 1.0;
    }

    [ObservableProperty]
    public partial string Icon { get; set; }

    [ObservableProperty]
    public partial string IconPath { get; set; }

    [ObservableProperty]
    public partial bool UsePathIcon { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; }

    [ObservableProperty]
    public partial Brush? DescriptionForeground { get; set; }

    [ObservableProperty]
    public partial string ButtonText { get; set; }

    [ObservableProperty]
    public partial ICommand? ButtonCommand { get; set; }

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsComplete { get; set; }

    [ObservableProperty]
    public partial bool HasFailed { get; set; }

    [ObservableProperty]
    public partial bool IsProcessing { get; set; }

    [ObservableProperty]
    public partial double Opacity { get; set; }

    partial void OnIconPathChanged(string value)
    {
        UsePathIcon = !string.IsNullOrEmpty(value);
    }

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
