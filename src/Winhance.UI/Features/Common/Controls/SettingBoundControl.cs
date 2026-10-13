using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

// A card input control projects its SettingItemViewModel into flat properties and re-projects when the view model
// changes. Its XAML root names this type, which is why it cannot be generic (see SectionPage).
public abstract class SettingBoundControl : UserControl, INotifyPropertyChanged
{
    public static readonly DependencyProperty SettingProperty = DependencyProperty.Register(
        nameof(Setting), typeof(SettingItemViewModel), typeof(SettingBoundControl),
        new PropertyMetadata(null, OnSettingChanged));

    private readonly SettingObserver _observer;

    protected SettingBoundControl()
    {
        // Safe despite naming overridables in a constructor: these are delegates, not calls, and the observer only
        // invokes them once the Setting property is first set.
        _observer = new SettingObserver(Refresh, RefreshesOn);
        Unloaded += (_, _) => _observer.Detach();
    }

    public SettingItemViewModel? Setting
    {
        get => (SettingItemViewModel?)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    // x:Bind OneWay subscribes here. Without it the compiler emits WMC1506 ("OneWay bindings require at least one
    // of their steps to support raising notifications") and the bindings refresh only when something calls
    // Bindings.Update() by hand, which fails silently when forgotten.
    public event PropertyChangedEventHandler? PropertyChanged;

    protected abstract void Refresh();

    // An override names the properties that move the control's rendering; without one it refreshes on every change.
    protected virtual bool RefreshesOn(string propertyName) => true;

    protected static void RefreshOnChange(DependencyObject d, DependencyPropertyChangedEventArgs _)
        => ((SettingBoundControl)d).Refresh();

    protected void Notify(params string[] names)
    {
        var handler = PropertyChanged;
        if (handler is null)
            return;
        foreach (var name in names)
            handler(this, new PropertyChangedEventArgs(name));
    }

    private static void OnSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SettingBoundControl)d;
        control._observer.Observe(e.NewValue as SettingItemViewModel);
        // Nested controls bind x:Bind Setting, OneWay to this inherited DP; this covers the compiler tracking it
        // through PropertyChanged rather than the DP callback.
        control.Notify(nameof(Setting));
    }
}
