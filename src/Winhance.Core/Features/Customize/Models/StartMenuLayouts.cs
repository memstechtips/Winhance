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

    /// <summary>
    /// Gets the Windows 11 Start Menu binary certificate template.
    /// This is a base64 encoded representation of the start2.bin file.
    /// </summary>
    public static string Windows11StartBinCertificate => @"-----BEGIN CERTIFICATE-----
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
TSz1QZdkum5Y3uC7wc7paXh1rm11nwluCC7jiA==
-----END CERTIFICATE-----";
}
