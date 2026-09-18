using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace Winhance.UI.Features.Common.ViewModels;

// Brush and Thumbnail are built on first read, which the binding does on the UI thread.
public sealed partial class OptionTileViewModel : ObservableObject
{
    // Twice the 96px tile, enough for a 200% display; a 4K picture decoded at full size is the whole cost of the row.
    private const int ThumbnailDecodeWidth = 192;

    private readonly Action<OptionTileViewModel> _chosen;
    private SolidColorBrush? _brush;
    private BitmapImage? _thumbnail;

    public OptionTileViewModel(string value, string label, bool isSelected, Action<OptionTileViewModel> chosen)
    {
        Value = value;
        Label = label;
        IsSelected = isSelected;
        _chosen = chosen;
    }

    public string Value { get; }

    public string Label { get; }

    public SolidColorBrush? Brush => _brush ??= ColorFromHex(Value) is { } color ? new SolidColorBrush(color) : null;

    public BitmapImage? Thumbnail => _thumbnail ??= Uri.TryCreate(Value, UriKind.Absolute, out var uri)
        ? new BitmapImage { DecodePixelWidth = ThumbnailDecodeWidth, UriSource = uri }
        : null;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public void Choose() => _chosen(this);

    internal static Windows.UI.Color? ColorFromHex(string? hex)
    {
        var digits = hex?.TrimStart('#');
        if (digits is not { Length: 6 }
            || !byte.TryParse(digits.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            || !byte.TryParse(digits.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            || !byte.TryParse(digits.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            return null;
        return Windows.UI.Color.FromArgb(255, r, g, b);
    }
}
