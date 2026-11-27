using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;

namespace Winhance.UI.Features.Common.Helpers;

// A link inside running text. A HyperlinkButton is its own box and breaks the line it sits in.
internal static class InlineLink
{
    public static Hyperlink Create(string text, Action? onClick = null, string? toolTip = null)
    {
        var link = new Hyperlink { UnderlineStyle = UnderlineStyle.None };
        link.Inlines.Add(new Run { Text = text });
        if (onClick is not null)
            link.Click += (_, _) => onClick();
        if (toolTip is not null)
        {
            ToolTipService.SetToolTip(link, toolTip);
            AutomationProperties.SetHelpText(link, toolTip);
        }
        return link;
    }
}
