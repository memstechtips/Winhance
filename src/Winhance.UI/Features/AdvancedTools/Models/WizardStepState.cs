using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI;

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
                OnPropertyChanged(nameof(HeaderIcon));
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
                OnPropertyChanged(nameof(HeaderIcon));
                OnPropertyChanged(nameof(HeaderIconColor));
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
                OnPropertyChanged(nameof(HeaderIcon));
                OnPropertyChanged(nameof(HeaderIconColor));
            }
        }
    }

    public bool IsLocked => !IsAvailable;

    /// <summary>
    /// Gets the Segoe MDL2 Assets glyph for the header icon.
    /// Lock = E72E, CheckMark = E73E, ChevronUp = E70E, ChevronDown = E70D
    /// </summary>
    public string HeaderIcon => IsLocked ? "\uE72E"
                              : IsComplete ? "\uE73E"
                              : "\uE70D"; // We'll rotate it with ChevronRotation

    /// <summary>
    /// Rotation angle for the chevron icon.
    /// </summary>
    public double ChevronRotation => IsExpanded ? 180 : 0;

    /// <summary>
    /// Gets the color for the header icon.
    /// Orange for locked, Green for complete, White for normal.
    /// </summary>
    public Color HeaderIconColor => IsLocked ? Color.FromArgb(255, 255, 165, 0)   // Orange
                                  : IsComplete ? Color.FromArgb(255, 76, 175, 80)  // Green
                                  : Color.FromArgb(255, 255, 255, 255);            // White

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
