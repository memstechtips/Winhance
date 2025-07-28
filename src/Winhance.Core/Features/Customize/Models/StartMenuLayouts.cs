using System;

namespace Winhance.Core.Features.Customize.Models;

/// <summary>
/// Contains layout templates for Windows 10 and Windows 11 Start Menus.
/// </summary>
public static class StartMenuLayouts
{
    /// <summary>
    /// Gets the Windows 10 Start Menu layout XML template.
    /// </summary>
    public static string Windows10Layout => @"<?xml version=""1.0"" encoding=""utf-8""?>
<LayoutModificationTemplate xmlns:defaultlayout=""http://schemas.microsoft.com/Start/2014/FullDefaultLayout"" xmlns:start=""http://schemas.microsoft.com/Start/2014/StartLayout"" Version=""1"" xmlns:taskbar=""http://schemas.microsoft.com/Start/2014/TaskbarLayout"" xmlns=""http://schemas.microsoft.com/Start/2014/LayoutModification"">
    <LayoutOptions StartTileGroupCellWidth=""6"" />
    <DefaultLayoutOverride>
        <StartLayoutCollection>
            <defaultlayout:StartLayout GroupCellWidth=""6"" />
        </StartLayoutCollection>
    </DefaultLayoutOverride>
</LayoutModificationTemplate>";
}
