using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Catalog;
using Winhance.UI.Features.Common.ViewModels;

namespace Winhance.UI.Features.Common.TemplateSelectors;

public partial class ListFieldTemplateSelector : DataTemplateSelector
{
    public DataTemplate? TextTemplate { get; set; }
    public DataTemplate? PasswordTemplate { get; set; }
    public DataTemplate? SelectionTemplate { get; set; }
    public DataTemplate? CheckBoxTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        if (item is not ListFieldViewModel field)
            return base.SelectTemplateCore(item);

        var template = field.Kind switch
        {
            FieldKind.Text => TextTemplate,
            FieldKind.Password => PasswordTemplate,
            FieldKind.Selection => SelectionTemplate,
            FieldKind.CheckBox => CheckBoxTemplate,
            _ => throw new InvalidOperationException(
                $"Field '{field.Key}' is a {field.Kind}, which a list row has no control for."),
        };

        return template ?? throw new InvalidOperationException($"No template is wired for {field.Kind}.");
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
