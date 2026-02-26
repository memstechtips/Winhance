using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models;

public static partial class ExternalAppDefinitions
{
    public static class Imaging
    {
        public static ItemGroup GetImaging()
        {
            return new ItemGroup
            {
                Name = "Imaging",
                FeatureId = FeatureIds.ExternalApps,
                Items = new List<ItemDefinition>
                {
                    new ItemDefinition
                    {
                        Id = "external-app-irfanview",
                        Name = "IrfanView",
                        Description = "Fast and compact image viewer and converter",
                        GroupName = "Imaging",
                        WinGetPackageId = ["IrfanSkiljan.IrfanView"],
                        ChocoPackageId = "irfanview",
                        WebsiteUrl = "https://www.irfanview.com/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-krita",
                        Name = "Krita",
                        Description = "Digital painting and illustration software",
                        GroupName = "Imaging",
                        WinGetPackageId = ["KDE.Krita"],
                        ChocoPackageId = "krita",
                        WebsiteUrl = "https://krita.org/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-blender",
                        Name = "Blender",
                        Description = "3D creation suite",
                        GroupName = "Imaging",
                        WinGetPackageId = ["BlenderFoundation.Blender"],
                        ChocoPackageId = "blender",
                        WebsiteUrl = "https://www.blender.org/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-paint-net",
                        Name = "Paint.NET",
                        Description = "Image and photo editing software",
                        GroupName = "Imaging",
                        WinGetPackageId = ["dotPDN.PaintDotNet"],
                        ChocoPackageId = "paint.net",
                        WebsiteUrl = "https://www.getpaint.net/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-gimp",
                        Name = "GIMP",
                        Description = "GNU Image Manipulation Program",
                        GroupName = "Imaging",
                        WinGetPackageId = ["GIMP.GIMP.3"],
                        ChocoPackageId = "gimp",
                        WebsiteUrl = "https://www.gimp.org/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-xnviewmp",
                        Name = "XnViewMP",
                        Description = "Image viewer, browser and converter",
                        GroupName = "Imaging",
                        WinGetPackageId = ["XnSoft.XnViewMP"],
                        ChocoPackageId = "xnviewmp",
                        WebsiteUrl = "https://www.xnview.com/en/xnviewmp/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-xnview-classic",
                        Name = "XnView Classic",
                        Description = "Image viewer, browser and converter (Classic Version)",
                        GroupName = "Imaging",
                        WinGetPackageId = ["XnSoft.XnView.Classic"],
                        ChocoPackageId = "xnview",
                        WebsiteUrl = "https://www.xnview.com/en/xnview/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-inkscape",
                        Name = "Inkscape",
                        Description = "Vector graphics editor",
                        GroupName = "Imaging",
                        WinGetPackageId = ["Inkscape.Inkscape"],
                        ChocoPackageId = "inkscape",
                        WebsiteUrl = "https://inkscape.org/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-greenshot",
                        Name = "Greenshot",
                        Description = "Screenshot tool with annotation features",
                        GroupName = "Imaging",
                        WinGetPackageId = ["Greenshot.Greenshot"],
                        ChocoPackageId = "greenshot",
                        WebsiteUrl = "https://getgreenshot.org/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-sharex",
                        Name = "ShareX",
                        Description = "Screen capture, file sharing and productivity tool",
                        GroupName = "Imaging",
                        WinGetPackageId = ["ShareX.ShareX"],
                        ChocoPackageId = "sharex",
                        WebsiteUrl = "https://getsharex.com/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-flameshot",
                        Name = "Flameshot",
                        Description = "Powerful yet simple to use screenshot software",
                        GroupName = "Imaging",
                        WinGetPackageId = ["Flameshot.Flameshot"],
                        ChocoPackageId = "flameshot",
                        WebsiteUrl = "https://flameshot.org/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-faststone",
                        Name = "FastStone",
                        Description = "Image browser, converter and editor",
                        GroupName = "Imaging",
                        WinGetPackageId = ["FastStone.Viewer"],
                        ChocoPackageId = "fsviewer",
                        WebsiteUrl = "https://www.faststone.org/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-imageglass",
                        Name = "ImageGlass",
                        Description = "Lightweight, versatile image viewer",
                        GroupName = "Imaging",
                        WinGetPackageId = ["DuongDieuPhap.ImageGlass"],
                        ChocoPackageId = "imageglass",
                        WebsiteUrl = "https://imageglass.org/"
                    }
                }
            };
        }
    }
}
