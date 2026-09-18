using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Catalog;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

public enum SettingTilePart
{
    Tiles,

    Browse,
}

public sealed partial class SettingTileSelection : UserControl, INotifyPropertyChanged
{
    public SettingTileSelection()
    {
        InitializeComponent();
        Unloaded += (_, _) => Detach();
    }

    public static readonly DependencyProperty SettingProperty = DependencyProperty.Register(
        nameof(Setting), typeof(SettingItemViewModel), typeof(SettingTileSelection),
        new PropertyMetadata(null, OnSettingChanged));

    public SettingItemViewModel? Setting
    {
        get => (SettingItemViewModel?)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    public static readonly DependencyProperty PartProperty = DependencyProperty.Register(
        nameof(Part), typeof(SettingTilePart), typeof(SettingTileSelection),
        new PropertyMetadata(SettingTilePart.Tiles, (d, _) => ((SettingTileSelection)d).Refresh()));

    public SettingTilePart Part
    {
        get => (SettingTilePart)GetValue(PartProperty);
        set => SetValue(PartProperty, value);
    }

    public ObservableCollection<OptionTileViewModel>? Tiles { get; private set; }

    public string BrowseLabel { get; private set; } = string.Empty;

    public string SettingName { get; private set; } = string.Empty;

    public Visibility PictureVisibility { get; private set; } = Visibility.Collapsed;

    public Visibility ColorVisibility { get; private set; } = Visibility.Collapsed;

    public Visibility TilesVisibility { get; private set; } = Visibility.Visible;

    public Visibility BrowseVisibility { get; private set; } = Visibility.Collapsed;

    // The flyout's picker writes here on every drag frame; only the colour it is left on reaches the card.
    public Windows.UI.Color PickedColor { get; set; } = Windows.UI.Color.FromArgb(255, 0, 0, 0);

    private SettingItemViewModel? _observed;

    private static void OnSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SettingTileSelection)d;
        control.Detach();
        control._observed = e.NewValue as SettingItemViewModel;
        if (control._observed is { } vm)
            vm.PropertyChanged += control.OnSettingPropertyChanged;
        control.Refresh();
    }

    private void Detach()
    {
        if (_observed is { } vm)
            vm.PropertyChanged -= OnSettingPropertyChanged;
        _observed = null;
    }

    private void OnSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null
            or nameof(SettingItemViewModel.Tiles)
            or nameof(SettingItemViewModel.SelectedValue)
            or nameof(SettingItemViewModel.Name))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        if (Setting is not { } vm)
            return;

        Tiles = vm.Tiles;
        BrowseLabel = vm.BrowseLabel;
        SettingName = vm.Name;

        var kind = vm.Setting?.Display.Tiles ?? OptionTiles.None;
        PictureVisibility = kind == OptionTiles.Pictures ? Visibility.Visible : Visibility.Collapsed;
        ColorVisibility = kind == OptionTiles.Colors ? Visibility.Visible : Visibility.Collapsed;
        TilesVisibility = Part == SettingTilePart.Tiles ? Visibility.Visible : Visibility.Collapsed;
        BrowseVisibility = Part == SettingTilePart.Browse ? Visibility.Visible : Visibility.Collapsed;

        if (kind == OptionTiles.Colors && OptionTileViewModel.ColorFromHex(vm.SelectedValue as string) is { } current)
            PickedColor = current;

        Notify(nameof(Tiles), nameof(BrowseLabel), nameof(SettingName),
            nameof(PictureVisibility), nameof(ColorVisibility), nameof(TilesVisibility), nameof(BrowseVisibility),
            nameof(PickedColor));
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e) => Setting?.BrowseForOption();

    private void OnColorFlyoutClosed(object? sender, object e) => Setting?.PickColor(PickedColor);

    // x:Bind OneWay subscribes here. Without it the compiler emits WMC1506 and the bindings refresh only when
    // something calls Bindings.Update() by hand, which fails silently when forgotten.

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify(params string[] names)
    {
        var handler = PropertyChanged;
        if (handler is null)
            return;
        foreach (var name in names)
            handler(this, new PropertyChangedEventArgs(name));
    }
}
