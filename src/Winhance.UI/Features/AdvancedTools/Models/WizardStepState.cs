using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Winhance.UI.Features.AdvancedTools.Models;

/// <summary>
/// Represents the state of a wizard step in the WIM Utility wizard.
/// </summary>
public class WizardStepState : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isAvailable;
    private bool _isComplete;
    private string _statusText = string.Empty;

    public int StepNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ChevronRotation));
            }
        }
    }

    public bool IsAvailable
    {
        get => _isAvailable;
        set
        {
            if (_isAvailable != value)
            {
                _isAvailable = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsLocked));
                OnPropertyChanged(nameof(ShowChevron));
            }
        }
    }

    public bool IsComplete
    {
        get => _isComplete;
        set
        {
            if (_isComplete != value)
            {
                _isComplete = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowChevron));
            }
        }
    }

    public bool IsLocked => !IsAvailable;

    /// <summary>
    /// Whether the chevron should be visible (not locked and not complete).
    /// </summary>
    public bool ShowChevron => !IsLocked && !IsComplete;

    /// <summary>
    /// Rotation angle for the chevron icon.
    /// </summary>
    public double ChevronRotation => IsExpanded ? 180 : 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
