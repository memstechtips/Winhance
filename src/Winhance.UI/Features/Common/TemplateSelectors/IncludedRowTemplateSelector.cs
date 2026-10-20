using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.Autounattend.ViewModels;

namespace Winhance.UI.Features.Common.TemplateSelectors;

public partial class IncludedRowTemplateSelector : DataTemplateSelector
{
    public DataTemplate? FeatureTemplate { get; set; }
    public DataTemplate? ExternalAppsTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        var template = item switch
        {
            ExternalAppsRowViewModel => ExternalAppsTemplate,
            IncludedRowViewModel => FeatureTemplate,
            _ => throw new InvalidOperationException($"A summary row has no template for {item}."),
        };

        return template ?? throw new InvalidOperationException($"No template is wired for {item.GetType().Name}.");
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
