using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Winhance.UI.Features.Common.Interfaces;

public interface ISelectAllSource : INotifyPropertyChanged
{
    string SelectAllLabel { get; }
    string SelectAllInstalledLabel { get; }
    string SelectAllNotInstalledLabel { get; }
    string SelectAllWithUpdatesLabel { get; }

    bool IsAllSelected { get; }
    bool IsAllSelectedInstalled { get; }
    bool IsAllSelectedNotInstalled { get; }
    bool IsAllSelectedWithUpdates { get; }
    bool HasUpdates { get; }

    IRelayCommand ToggleSelectAllCommand { get; }
    IRelayCommand ToggleSelectAllInstalledCommand { get; }
    IRelayCommand ToggleSelectAllNotInstalledCommand { get; }
    IRelayCommand ToggleSelectAllWithUpdatesCommand { get; }
}
