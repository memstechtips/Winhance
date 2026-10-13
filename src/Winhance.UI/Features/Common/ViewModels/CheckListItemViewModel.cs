using CommunityToolkit.Mvvm.ComponentModel;

namespace Winhance.UI.Features.Common.ViewModels;

public sealed partial class CheckListItemViewModel(string key, bool isChecked, Action changed) : ObservableObject
{
    private bool _isChecked = isChecked;

    public string Key { get; } = key;

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (SetProperty(ref _isChecked, value))
                changed();
        }
    }
}
