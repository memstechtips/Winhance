using System.ComponentModel;
using FluentAssertions;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Tests.ViewModels;

// What the four select-all boxes show. A OneWay binding reads a box only when the view model raises it, so a box
// shows the value it had at its last raise, not the value its getter would return now.
internal sealed class SelectAllBoxes
{
    private static readonly string[] Names =
    [
        nameof(ISelectAllSource.IsAllSelected), nameof(ISelectAllSource.IsAllSelectedInstalled),
        nameof(ISelectAllSource.IsAllSelectedNotInstalled), nameof(ISelectAllSource.IsAllSelectedWithUpdates),
    ];

    private readonly ISelectAllSource _source;
    private readonly Dictionary<string, bool> _shown = [];

    public SelectAllBoxes(ISelectAllSource source)
    {
        _source = source;
        foreach (var name in Names)
            _shown[name] = Read(name);
        source.PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is { } name && _shown.ContainsKey(name))
            _shown[name] = Read(name);
    }

    private bool Read(string name) => name switch
    {
        nameof(ISelectAllSource.IsAllSelected) => _source.IsAllSelected,
        nameof(ISelectAllSource.IsAllSelectedInstalled) => _source.IsAllSelectedInstalled,
        nameof(ISelectAllSource.IsAllSelectedNotInstalled) => _source.IsAllSelectedNotInstalled,
        _ => _source.IsAllSelectedWithUpdates,
    };

    public void ShouldShowWhatTheRowsSay()
    {
        foreach (var name in Names)
            _shown[name].Should().Be(Read(name), $"{name} was not raised when the rows it covers changed");
    }
}
