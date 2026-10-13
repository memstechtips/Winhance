using System.ComponentModel;

namespace Winhance.UI.Features.Common.Controls;

// The observation half of SettingBoundControl, kept off the UserControl so that it can be tested: the headless
// test host throws from UserControl's constructor, so logic left on the control itself is reachable only by
// running the app.
//
// SectionOverviewItemViewModel, SettingsGroup and BaseSettingsFeatureViewModel hand-roll this same
// subscribe/swap/detach shape inline and should keep doing so. They are plain view models the test host can
// construct, so the logic is already reachable there and extracting it would buy nothing.
public sealed class SettingObserver
{
    private readonly Action _refresh;
    private readonly Func<string, bool> _refreshesOn;
    private INotifyPropertyChanged? _observed;

    public SettingObserver(Action refresh, Func<string, bool> refreshesOn)
    {
        _refresh = refresh;
        _refreshesOn = refreshesOn;
    }

    public void Observe(INotifyPropertyChanged? source)
    {
        Detach();
        _observed = source;
        if (_observed is { } vm)
            vm.PropertyChanged += OnPropertyChanged;
        _refresh();
    }

    public void Detach()
    {
        if (_observed is { } vm)
            vm.PropertyChanged -= OnPropertyChanged;
        _observed = null;
    }

    // A null PropertyName means "everything", which the view model raises on a language change.
    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null || _refreshesOn(e.PropertyName))
            _refresh();
    }
}
