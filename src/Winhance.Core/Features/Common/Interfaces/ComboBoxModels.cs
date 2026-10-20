using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Winhance.Core.Features.Common.Interfaces;

// In Core rather than the UI because SettingStateSnapshot carries the option list into TechnicalDetailsBuilder.
public class ComboBoxSetupResult
{
    public ObservableCollection<ComboBoxDisplayOption> Options { get; set; } = new();
    public object? SelectedValue { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ComboBoxDisplayOption : INotifyPropertyChanged
{
    private string _displayText;

    public ComboBoxDisplayOption(string displayText, object value, string? description = null, object? tag = null)
    {
        _displayText = displayText;
        Value = value;
        Description = description;
        Tag = tag;
    }

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

    public object Value { get; }
    public string? Description { get; }
    public object? Tag { get; }

    public bool IsRecommended { get; set; }

    public bool IsDefault { get; set; }

    public bool IsSubjectivePreference { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public override string ToString() => DisplayText;
}
