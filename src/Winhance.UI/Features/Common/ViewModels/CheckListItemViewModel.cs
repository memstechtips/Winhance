using CommunityToolkit.Mvvm.ComponentModel;

namespace Winhance.UI.Features.Common.ViewModels;

public sealed partial class CheckListItemViewModel(string key, bool isChecked, Action changed) : ObservableObject
{
    public string Key { get; } = key;

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = isChecked;

    partial void OnIsCheckedChanged(bool value) => changed();
}
