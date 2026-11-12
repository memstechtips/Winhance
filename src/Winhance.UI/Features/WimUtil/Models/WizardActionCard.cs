using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using FluentIcons.Common;

namespace Winhance.UI.Features.WimUtil.Models;

public partial class WizardActionCard : ObservableObject
{
    public WizardActionCard()
    {
        Title = string.Empty;
        Description = string.Empty;
        ButtonText = string.Empty;
        IsEnabled = true;
    }

    [ObservableProperty]
    public partial string Title { get; set; }

    // Read by the build list, where each step shows what it does; the fixed cards set theirs on the view.
    public Icon Icon { get; init; } = Icon.DocumentSave;

    [ObservableProperty]
    public partial string Description { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasButton))]
    public partial string ButtonText { get; set; }

    // Cards that only report progress have no button.
    public bool HasButton => !string.IsNullOrEmpty(ButtonText);

    [ObservableProperty]
    public partial ICommand? ButtonCommand { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisabledReason))]
    public partial bool IsEnabled { get; set; }

    // Each card is disabled only while its own task runs.
    public string BusyReason { get; init; } = string.Empty;

    public string? DisabledReason => IsEnabled ? null : BusyReason;

    [ObservableProperty]
    public partial bool IsComplete { get; set; }

    [ObservableProperty]
    public partial bool HasFailed { get; set; }

    [ObservableProperty]
    public partial bool IsProcessing { get; set; }

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
