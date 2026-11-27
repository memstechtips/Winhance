using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Winhance.Core.Features.Common.Interfaces;

// In Core rather than the UI because SettingStateSnapshot carries the option list into TechnicalDetailsBuilder.
public class ComboBoxSetupResult
{
    public ObservableCollection<ComboBoxDisplayOption> Options { get; set; } = new();
    public object? SelectedValue { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public partial class ComboBoxDisplayOption : ObservableObject
{
    [ObservableProperty]
    private string _displayText;

    public ComboBoxDisplayOption(string displayText, object value, string? description = null, object? tag = null)
    {
        _displayText = displayText;
        Value = value;
        Description = description;
        Tag = tag;
    }

    public object Value { get; }
    public string? Description { get; }
    public object? Tag { get; }

    public bool IsRecommended { get; set; }

    public bool IsDefault { get; set; }

    public bool IsSubjectivePreference { get; set; }

    // Set only on a list that shows each option's status (power plans): the dropdown row draws its status dot, its
    // Active badge and its delete button from these.
    public bool ShowsStatus { get; set; }

    public bool ExistsOnSystem { get; set; }

    public string? StatusTooltip { get; set; }

    public bool IsActive { get; set; }

    public string? ActiveBadgeText { get; set; }

    public bool CanDelete { get; set; }

    public string? DeleteTooltip { get; set; }

    public override string ToString() => DisplayText;
}
