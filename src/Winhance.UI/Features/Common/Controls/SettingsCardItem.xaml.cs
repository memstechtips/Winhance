using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>
/// A reusable UserControl that displays a SettingItemViewModel in a SettingsCard
/// with the appropriate control based on the InputType.
/// </summary>
public sealed partial class SettingsCardItem : UserControl
{
    public static readonly DependencyProperty SettingProperty =
        DependencyProperty.Register(
            nameof(Setting),
            typeof(SettingItemViewModel),
            typeof(SettingsCardItem),
            new PropertyMetadata(null));

    public SettingItemViewModel? Setting
    {
        get => (SettingItemViewModel?)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    public SettingsCardItem()
    {
        this.InitializeComponent();
    }
}
