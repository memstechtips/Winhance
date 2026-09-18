using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.UI.Features.Common.ViewModels;

// A DataTemplate can x:Bind only to its own data type, so every caption a field draws is localized onto the field.
public sealed partial class ListFieldViewModel : ObservableObject
{
    private readonly Field _field;
    private readonly Action<ListFieldViewModel> _changed;

    private string _text = string.Empty;
    private bool _checked;
    private int _optionIndex;
    private bool _isEnabled = true;

    public ListFieldViewModel(
        Field field,
        string label,
        string? tooltip,
        IReadOnlyList<string> options,
        string error,
        string? value,
        Action<ListFieldViewModel> changed)
    {
        _field = field;
        Label = label;
        Tooltip = tooltip;
        Options = options;
        Error = error;
        _changed = changed;

        switch (field.Kind)
        {
            case FieldKind.CheckBox:
                _checked = bool.TryParse(value, out var ticked) && ticked;
                break;
            case FieldKind.Selection:
                _optionIndex = int.TryParse(value, out var index) && index >= 0 && index < options.Count ? index : 0;
                break;
            default:
                _text = value ?? string.Empty;
                break;
        }
    }

    public string Key => _field.Key;

    public FieldKind Kind => _field.Kind;

    public bool OneRowOnly => _field.OneRowOnly;

    internal FieldCondition? OnlyWhen => _field.OnlyWhen;

    public string Label { get; }

    public string? Tooltip { get; }

    public IReadOnlyList<string> Options { get; }

    public string Error { get; }

    public string Text
    {
        get => _text;
        set
        {
            if (!SetProperty(ref _text, value ?? string.Empty))
                return;

            OnPropertyChanged(nameof(HasError));
            _changed(this);
        }
    }

    public bool HasError => _field.Rule is { } rule && HasText && !rule.Matches(_text);

    internal bool HasText => _field.Rule is { } rule
        ? rule.Normalize(_text).Length > 0
        : _text.Trim().Length > 0;

    public bool Checked
    {
        get => _checked;
        set
        {
            // A disabled field refuses the tick, and raises anyway so the two-way bound checkbox snaps back off.
            if (value && !_isEnabled)
            {
                OnPropertyChanged();
                return;
            }

            if (SetProperty(ref _checked, value))
                _changed(this);
        }
    }

    // Out of range is what a rebuilding ComboBox pushes; raising makes it read the index back.
    public int OptionIndex
    {
        get => _optionIndex;
        set
        {
            if (value < 0 || value >= Options.Count)
            {
                OnPropertyChanged();
                return;
            }

            if (SetProperty(ref _optionIndex, value))
                _changed(this);
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        internal set => SetProperty(ref _isEnabled, value);
    }

    // A password is recorded as typed: a space at either end is part of what the user will type at the sign-in screen.
    internal string Value => _field.Kind switch
    {
        FieldKind.Selection => _optionIndex.ToString(System.Globalization.CultureInfo.InvariantCulture),
        FieldKind.CheckBox => _checked ? "true" : "false",
        FieldKind.Password => _text,
        _ => _field.Rule is { } rule ? rule.Normalize(_text) : _text.Trim(),
    };

    // No callback: the caller is the row, already inside a change, or the card, already inside a write.
    internal void ClearTick()
    {
        if (_checked)
            SetProperty(ref _checked, false, nameof(Checked));
    }
}

public sealed partial class ListRowViewModel : ObservableObject
{
    private readonly Action<ListRowViewModel> _changed;
    private readonly Action<ListRowViewModel> _remove;

    private bool _canRemove = true;

    public ListRowViewModel(
        IReadOnlyList<Field> fields,
        Func<LocKey, string> localize,
        string removeLabel,
        ChoiceValue.ListRow? values,
        Action<ListRowViewModel> changed,
        Action<ListRowViewModel> remove)
    {
        _changed = changed;
        _remove = remove;
        RemoveLabel = removeLabel;
        Fields = fields
            .Select(field => new ListFieldViewModel(
                field,
                localize(field.Label),
                field.Tooltip is { } tooltip ? localize(tooltip) : null,
                field.Options is { } options ? options.Select(localize).ToList() : [],
                field.Rule is { } rule ? localize(rule.Message) : string.Empty,
                ValueFor(values, field),
                OnFieldChanged))
            .ToList();

        ApplyConditions();
    }

    public IReadOnlyList<ListFieldViewModel> Fields { get; }

    public string RemoveLabel { get; }

    public bool CanRemove
    {
        get => _canRemove;
        internal set => SetProperty(ref _canRemove, value);
    }

    public void Remove() => _remove(this);

    public ListFieldViewModel? FieldFor(string key) =>
        Fields.FirstOrDefault(field => string.Equals(field.Key, key, StringComparison.Ordinal));

    internal bool IsFilled =>
        Fields.FirstOrDefault(f => f.Kind == FieldKind.Text) is not { } first || first.HasText;

    internal ChoiceValue.ListRow ToRow() =>
        new(Fields.ToDictionary(field => field.Key, field => field.Value, StringComparer.Ordinal));

    private static string? ValueFor(ChoiceValue.ListRow? values, Field field) =>
        values is not null && values.Values.TryGetValue(field.Key, out var value) ? value : field.Default;

    private void OnFieldChanged(ListFieldViewModel field)
    {
        ApplyConditions();
        _changed(this);
    }

    // An unmet field loses its tick: the file should never carry a choice the row no longer shows.
    private void ApplyConditions()
    {
        foreach (var field in Fields)
        {
            if (field.OnlyWhen is not { } condition)
                continue;

            bool met = FieldFor(condition.FieldKey) is { } other && other.OptionIndex == condition.OptionIndex;
            field.IsEnabled = met;
            if (!met)
                field.ClearTick();
        }
    }
}
