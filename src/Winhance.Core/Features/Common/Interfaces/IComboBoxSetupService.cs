using System.Collections.ObjectModel;
using System.ComponentModel;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IComboBoxSetupService
{
    Task<ComboBoxSetupResult> SetupComboBoxOptionsAsync(SettingDefinition setting, object? currentValue);
    Task<int> ResolveIndexFromRawValuesAsync(SettingDefinition setting, Dictionary<string, object?> rawValues);
}

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

    /// <summary>True if the source ComboBoxOption was flagged as Recommended.</summary>
    public bool IsRecommended { get; set; }

    /// <summary>True if the source ComboBoxOption was flagged as Windows Default.</summary>
    public bool IsDefault { get; set; }

    /// <summary>True when the source SettingDefinition is flagged IsSubjectivePreference.</summary>
    public bool IsSubjectivePreference { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    public override string ToString() => DisplayText;
}
