using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>
/// Renders a single <see cref="ComboBoxDisplayOption"/> inside a ComboBox ItemTemplate.
/// Applies the Recommended / Default pill style to the open dropdown rows only —
/// when the control discovers at load time that it is NOT hosted inside a
/// <see cref="ComboBoxItem"/> (i.e. it is being used as the ComboBox selection box),
/// the pill style is cleared and the tooltip removed so the closed selection box
/// stays plain. This satisfies the B3 spec requirement that selected options never
/// display a pill background in the collapsed control.
/// </summary>
public sealed partial class ComboBoxOptionPill : UserControl
{
    public ComboBoxOptionPill()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (args.NewValue is ComboBoxDisplayOption option)
        {
            PillText.Text = option.DisplayText;
            // Subscribe to future DisplayText changes (e.g. language switch).
            option.PropertyChanged -= Option_PropertyChanged;
            option.PropertyChanged += Option_PropertyChanged;
        }
        else
        {
            PillText.Text = string.Empty;
        }
        ApplyVisualState();
    }

    private void Option_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not ComboBoxDisplayOption option) return;
        if (e.PropertyName == nameof(ComboBoxDisplayOption.DisplayText))
        {
            PillText.Text = option.DisplayText;
        }
        else if (e.PropertyName == nameof(ComboBoxDisplayOption.ShowPill))
        {
            ApplyVisualState();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ApplyVisualState();
    }

    /// <summary>
    /// Decide whether to paint the pill. Requirements:
    ///  1. DataContext is a ComboBoxDisplayOption,
    ///  2. the option is Recommended or Default,
    ///  3. ShowPill (i.e. ShowInfoBadges) is on,
    ///  4. we are rendered INSIDE a ComboBoxItem container — i.e. inside the OPEN dropdown.
    /// Otherwise clear the style so the Border renders transparently.
    /// </summary>
    private void ApplyVisualState()
    {
        if (DataContext is not ComboBoxDisplayOption option)
        {
            ClearPill();
            return;
        }

        if (option.IsSubjectivePreference)
        {
            // Winhance has no opinion — no pill in the open dropdown, even on the IsDefault option.
            ClearPill();
            return;
        }

        if (!option.ShowPill || (!option.IsRecommended && !option.IsDefault))
        {
            ClearPill();
            return;
        }

        if (!IsInsideComboBoxItem())
        {
            // Rendered in the ComboBox selection box — keep the closed state plain.
            ClearPill();
            return;
        }

        // Recommended wins tiebreak when both flags are set.
        var styleKey = option.IsRecommended
            ? "ComboBoxOptionRecommendedPillStyle"
            : "ComboBoxOptionDefaultPillStyle";

        if (Application.Current.Resources.TryGetValue(styleKey, out var styleObj) && styleObj is Style style)
        {
            PillBorder.Style = style;
        }
        else
        {
            ClearPill();
            return;
        }

        var tooltipKey = (option.IsRecommended, option.IsDefault) switch
        {
            (true, true) => "InfoBadge_ComboBox_RecommendedAlsoDefault_Tooltip",
            (true, false) => "InfoBadge_ComboBox_Recommended_Tooltip",
            (false, true) => "InfoBadge_ComboBox_WindowsDefault_Tooltip",
            _ => null
        };

        string? tooltip = null;
        if (tooltipKey != null)
        {
            var localization = App.Services?.GetService<ILocalizationService>();
            tooltip = localization?.GetString(tooltipKey) ?? tooltipKey;
        }
        ToolTipService.SetToolTip(PillBorder, tooltip);
    }

    private void ClearPill()
    {
        PillBorder.ClearValue(Border.StyleProperty);
        ToolTipService.SetToolTip(PillBorder, null);
    }

    private bool IsInsideComboBoxItem()
    {
        DependencyObject? current = this;
        while (current != null)
        {
            if (current is ComboBoxItem)
            {
                return true;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }
}
