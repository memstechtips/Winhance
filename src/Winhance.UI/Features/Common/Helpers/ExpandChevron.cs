using FluentIcons.Common;

namespace Winhance.UI.Features.Common.Helpers;

internal static class ExpandChevron
{
    public static Icon For(bool isExpanded) => isExpanded ? Icon.ChevronUp : Icon.ChevronDown;
}
