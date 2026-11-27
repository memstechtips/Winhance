using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.UI.Features.Settings.ViewModels;

namespace Winhance.UI.Features.Settings;

public partial class BackgroundRowTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ModeTemplate { get; set; }
    public DataTemplate? EmptyListTemplate { get; set; }
    public DataTemplate? FolderSafeTemplate { get; set; }
    public DataTemplate? FolderUnsafeTemplate { get; set; }
    public DataTemplate? CategoryTemplate { get; set; }
    public DataTemplate? TaskTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        var template = item switch
        {
            ModeRow => ModeTemplate,
            EmptyListRow => EmptyListTemplate,
            FolderSafeRow => FolderSafeTemplate,
            FolderUnsafeRow => FolderUnsafeTemplate,
            CategoryRow => CategoryTemplate,
            TaskRow => TaskTemplate,
            _ => throw new InvalidOperationException($"No template for the row type {item.GetType().Name}."),
        };

        return template ?? throw new InvalidOperationException($"No template is wired for {item.GetType().Name}.");
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
        => SelectTemplateCore(item);
}
