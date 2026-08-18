using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Winhance.UI.Features.AdvancedTools.Models;

public class WizardStepState : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isAvailable;
    private bool _isComplete;
    private string _statusText = string.Empty;

    private string _title = string.Empty;

    public int StepNumber { get; set; }

    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
            }
        }
    }

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

    public bool ShowChevron => !IsLocked && !IsComplete;

    public double ChevronRotation => IsExpanded ? 180 : 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
