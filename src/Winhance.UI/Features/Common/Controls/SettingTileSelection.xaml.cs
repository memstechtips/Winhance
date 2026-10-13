using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Winhance.Core.Features.Common.Catalog;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

public enum SettingTilePart
{
    Tiles,

    Browse,
}

public sealed partial class SettingTileSelection : SettingBoundControl
{
    public SettingTileSelection()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty PartProperty = DependencyProperty.Register(
        nameof(Part), typeof(SettingTilePart), typeof(SettingTileSelection),
        new PropertyMetadata(SettingTilePart.Tiles, RefreshOnChange));

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

    protected override bool RefreshesOn(string propertyName)
        => propertyName is nameof(SettingItemViewModel.Tiles)
            or nameof(SettingItemViewModel.SelectedValue)
            or nameof(SettingItemViewModel.Name);

    protected override void Refresh()
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
}
