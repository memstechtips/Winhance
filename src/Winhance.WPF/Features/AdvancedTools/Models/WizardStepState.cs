using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Winhance.WPF.Features.AdvancedTools.Models
{
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

        public string HeaderIcon => IsLocked ? "Lock"
                                  : IsComplete ? "CheckCircle"
                                  : IsExpanded ? "ChevronUp" : "ChevronDown";

        public Color HeaderIconColor => IsLocked ? Color.FromRgb(255, 165, 0)
                                        : IsComplete ? Color.FromRgb(76, 175, 80)
                                        : Color.FromRgb(255, 255, 255);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
