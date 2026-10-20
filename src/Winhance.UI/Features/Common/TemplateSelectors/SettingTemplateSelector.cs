using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Enums;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.TemplateSelectors;

public partial class SettingTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ToggleTemplate { get; set; }
    public DataTemplate? CheckBoxTemplate { get; set; }
    public DataTemplate? SelectionTemplate { get; set; }
    public DataTemplate? NumericTemplate { get; set; }
    public DataTemplate? ActionTemplate { get; set; }
    public DataTemplate? TextBoxTemplate { get; set; }
    public DataTemplate? ListTemplate { get; set; }
    public DataTemplate? TileSelectionTemplate { get; set; }
    // ONE template each: the on-battery column is bound to HasBattery inside the template rather than split into
    // Dual/SingleAC variants, which is what let the two halves drift apart.
    public DataTemplate? PowerSelectionTemplate { get; set; }
    public DataTemplate? PowerNumericTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        if (item is SettingItemViewModel vm)
        {
            if (vm.SupportsSeparateACDC)
            {
                if (vm.InputType == InputType.Selection)
                    return PowerSelectionTemplate;
                if (vm.InputType == InputType.NumericRange)
                    return PowerNumericTemplate;
            }

            if (vm.ShowsOptionTiles)
                return TileSelectionTemplate
                    ?? throw new InvalidOperationException($"No template is wired for the tiles of '{vm.SettingId}'.");

            var template = vm.InputType switch
            {
                InputType.Toggle => ToggleTemplate,
                InputType.CheckBox => CheckBoxTemplate,
                InputType.Selection => SelectionTemplate,
                InputType.NumericRange => NumericTemplate,
                InputType.Action => ActionTemplate,
                InputType.TextBox => TextBoxTemplate,
                InputType.List => ListTemplate,
                _ => throw new InvalidOperationException(
                    $"Setting '{vm.SettingId}' is a {vm.InputType}, which the settings list has no card for."),
            };

            return template ?? throw new InvalidOperationException($"No template is wired for {vm.InputType}.");
        }

        return base.SelectTemplateCore(item);
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        return SelectTemplateCore(item);
    }
}
