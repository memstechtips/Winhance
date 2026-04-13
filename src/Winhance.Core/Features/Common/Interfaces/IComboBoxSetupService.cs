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
    private bool _showPill;

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

    /// <summary>
    /// True if the source ComboBoxOption was flagged as Recommended.
    /// Drives the green pill background in the open dropdown.
    /// </summary>
    public bool IsRecommended { get; set; }

    /// <summary>
    /// True if the source ComboBoxOption was flagged as Windows Default.
    /// Drives the grey pill background in the open dropdown.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// True when the source SettingDefinition is flagged IsSubjectivePreference —
    /// Winhance has no opinion on which option is "correct", so the open-dropdown
    /// pill is suppressed entirely (no green, no grey), even on the IsDefault option.
    /// </summary>
    public bool IsSubjectivePreference { get; set; }

    /// <summary>
    /// Computed: pill background is visible only when the user has info-badges enabled
    /// AND the option is Recommended or Default. Reset when the global toggle flips.
    /// Notifies <see cref="PillStyleTrigger"/> and <see cref="PillTooltipTrigger"/>
    /// so bindings that use them as a trigger re-evaluate.
    /// </summary>
    public bool ShowPill
    {
        get => _showPill;
        set
        {
            if (_showPill != value)
            {
                _showPill = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowPill)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PillStyleTrigger)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PillTooltipTrigger)));
            }
        }
    }

    /// <summary>
    /// Returns the option itself when the pill should paint, or null when it should not.
    /// XAML binds the pill style converter to this so that the style re-resolves when
    /// <see cref="ShowPill"/> flips via the ShowInfoBadges toggle.
    /// </summary>
    public ComboBoxDisplayOption? PillStyleTrigger => ShowPill && !IsSubjectivePreference && (IsRecommended || IsDefault) ? this : null;

    /// <summary>
    /// Same pattern as <see cref="PillStyleTrigger"/> but for the tooltip binding. Null when no pill.
    /// </summary>
    public ComboBoxDisplayOption? PillTooltipTrigger => ShowPill && !IsSubjectivePreference && (IsRecommended || IsDefault) ? this : null;

    public event PropertyChangedEventHandler? PropertyChanged;

    public override string ToString()
    {
        return $"{DisplayText} (Value: {Value})";
    }
}
