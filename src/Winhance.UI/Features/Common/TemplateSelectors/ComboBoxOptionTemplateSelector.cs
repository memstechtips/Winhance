using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Winhance.UI.Features.Common.TemplateSelectors;

// A ComboBox draws its closed box from the same item template as its dropdown rows. The status dot, the Active badge
// and the delete button belong on the rows only, so the closed box (any container that is not a ComboBoxItem) gets
// the plain text template.
public partial class ComboBoxOptionTemplateSelector : DataTemplateSelector
{
    public DataTemplate? RowTemplate { get; set; }
    public DataTemplate? SelectionTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item) => SelectionTemplate;

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => container is ComboBoxItem ? RowTemplate : SelectionTemplate;
}
