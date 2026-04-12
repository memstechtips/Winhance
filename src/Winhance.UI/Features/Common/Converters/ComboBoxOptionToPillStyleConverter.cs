using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Converters;

/// <summary>
/// Resolves the pill <see cref="Style"/> used to highlight a <see cref="ComboBoxDisplayOption"/>
/// in the open dropdown. Returns:
/// <list type="bullet">
///   <item><c>ComboBoxOptionRecommendedPillStyle</c> when the option is Recommended (tiebreak wins when both flags are set)</item>
///   <item><c>ComboBoxOptionDefaultPillStyle</c> when the option is Default only</item>
///   <item><c>null</c> when pill display is disabled or the option carries neither flag — the default transparent Border is used.</item>
/// </list>
/// Style resources are looked up from <see cref="Application.Current"/> to keep the converter lightweight.
/// </summary>
public partial class ComboBoxOptionToPillStyleConverter : IValueConverter
{
    internal const string RecommendedStyleKey = "ComboBoxOptionRecommendedPillStyle";
    internal const string DefaultStyleKey = "ComboBoxOptionDefaultPillStyle";

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not ComboBoxDisplayOption option)
        {
            return null;
        }

        if (!option.ShowPill)
        {
            return null;
        }

        string? key = option switch
        {
            { IsRecommended: true } => RecommendedStyleKey, // Recommended wins the tiebreak when both flags are set.
            { IsDefault: true } => DefaultStyleKey,
            _ => null
        };

        if (key is null)
        {
            return null;
        }

        return Application.Current.Resources.TryGetValue(key, out var style) ? style as Style : null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotImplementedException();
}
