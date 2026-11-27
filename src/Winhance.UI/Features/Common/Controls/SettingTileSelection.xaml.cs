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

public sealed partial class SettingTileSelection : SettingBoundControl
{
    public SettingTileSelection()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty PartProperty = DependencyProperty.Register(
        nameof(Part), typeof(SettingTilePart), typeof(SettingTileSelection),
        new PropertyMetadata(SettingTilePart.Tiles));

    public SettingTilePart Part
    {
        get => (SettingTilePart)GetValue(PartProperty);
        set => SetValue(PartProperty, value);
    }

    public static bool ShowsTiles(SettingTilePart part) => part == SettingTilePart.Tiles;

    public static bool ShowsBrowse(SettingTilePart part) => part == SettingTilePart.Browse;

    public static bool IsPictures(SettingItemViewModel? setting) => setting?.Setting?.Display.Tiles == OptionTiles.Pictures;

    public static bool IsColors(SettingItemViewModel? setting) => setting?.Setting?.Display.Tiles == OptionTiles.Colors;

    public static Windows.UI.Color ColorFor(object? selectedValue)
        => OptionTileViewModel.ColorFromHex(selectedValue as string) ?? Windows.UI.Color.FromArgb(255, 0, 0, 0);

    private void OnBrowseClick(object sender, RoutedEventArgs e) => Setting?.BrowseForOption();

    // The picker moves on every drag frame; only the colour it is left on reaches the card.
    private void OnColorFlyoutClosed(object? sender, object e)
    {
        if (sender is Flyout { Content: ColorPicker picker })
            Setting?.PickColor(picker.Color);
    }
}
