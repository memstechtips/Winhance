using System.Collections.ObjectModel;
using System.ComponentModel;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IComboBoxSetupService
    {
        ComboBoxSetupResult SetupComboBoxOptions(SettingDefinition setting, object? currentValue);
        Task<ComboBoxSetupResult> SetupComboBoxOptionsAsync(SettingDefinition setting, object? currentValue);
        int ResolveIndexFromRawValues(SettingDefinition setting, Dictionary<string, object?> rawValues);
    }

    public class ComboBoxSetupResult
    {
        public ObservableCollection<ComboBoxOption> Options { get; set; } = new();
        public object? SelectedValue { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class ComboBoxOption : INotifyPropertyChanged
    {
        private string _displayText = string.Empty;
        
        public string DisplayText
        {
            get => _displayText;
            set
            {
                if (_displayText != value)
                {
                    _displayText = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayText)));
                }
            }
        }

        public object Value { get; set; } = new();
        public string? Description { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
