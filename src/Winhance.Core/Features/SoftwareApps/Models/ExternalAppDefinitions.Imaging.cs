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
                        Name = "IrfanView64",
                        Description = "Fast and compact image viewer and converter",
                        GroupName = "Imaging",
                        AppxPackageName = ["30067IrfanSkiljanIrfanVie.IrfanView64"],
                        WinGetPackageId = ["IrfanSkiljan.IrfanView"],
                        ChocoPackageId = "irfanview",
                        MsStoreId = "9PJZ3BTL5PV6",
                        WebsiteUrl = "https://www.irfanview.com/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/4/4c/IrfanView_Logo2.svg/250px-IrfanView_Logo2.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-krita",
                        Name = "Krita",
                        Description = "Digital painting and illustration software",
                        RegistryDisplayName = "Krita ({arch}) {version}",
                        GroupName = "Imaging",
                        AppxPackageName = ["49800KritaProject.Krita"],
                        WinGetPackageId = ["KDE.Krita"],
                        ChocoPackageId = "krita",
                        MsStoreId = "9N6X57ZGRW96",
                        WebsiteUrl = "https://krita.org/",
                        IconSources = [
                            "https://raw.githubusercontent.com/KDE/krita/master/krita/pics/branding/krita.ico",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/7/73/Calligrakrita-base.svg/250px-Calligrakrita-base.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-blender",
                        Name = "Blender",
                        Description = "3D modeling, animation, rendering, and video editing suite",
                        GroupName = "Imaging",
                        AppxPackageName = ["BlenderFoundation.Blender"],
                        WinGetPackageId = ["BlenderFoundation.Blender"],
                        ChocoPackageId = "blender",
                        MsStoreId = "9PP3C07GTVRH",
                        WebsiteUrl = "https://www.blender.org/",
                        IconSources = [
                            "https://raw.githubusercontent.com/blender/blender/main/release/windows/icons/winblender.ico",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3c/Logo_Blender.svg/250px-Logo_Blender.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-paint-net",
                        Name = "Paint.NET",
                        Description = "Image and photo editing software",
                        GroupName = "Imaging",
                        AppxPackageName = ["dotPDNLLC.paint.net"],
                        WinGetPackageId = ["dotPDN.PaintDotNet"],
                        ChocoPackageId = "paint.net",
                        MsStoreId = "9NBHCS1LX4R0",
                        WebsiteUrl = "https://www.getpaint.net/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/c/c4/Pain.net_logo.jpg",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-gimp",
                        Name = "GIMP",
                        Description = "Open-source raster image editor with layers, masks, and plugins",
                        RegistryDisplayName = "GIMP {version}",
                        GroupName = "Imaging",
                        WinGetPackageId = ["GIMP.GIMP.3"],
                        ChocoPackageId = "gimp",
                        WebsiteUrl = "https://www.gimp.org/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/6/67/The_GIMP_icon_-_v3.0.svg/250px-The_GIMP_icon_-_v3.0.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-xnviewmp",
                        Name = "XnViewMP",
                        Description = "Image viewer, browser and converter",
                        RegistryDisplayName = "XnView MP ({arch})",
                        GroupName = "Imaging",
                        WinGetPackageId = ["XnSoft.XnViewMP"],
                        ChocoPackageId = "xnviewmp",
                        WebsiteUrl = "https://www.xnview.com/en/xnviewmp/",
                        IconSources = [
                            "https://www.xnview.com/img/app-xnviewmp-512.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-xnview-classic",
                        Name = "XnView",
                        Description = "Image viewer, browser and converter (Classic Version)",
                        RegistryDisplayName = "XnView",
                        GroupName = "Imaging",
                        WinGetPackageId = ["XnSoft.XnView.Classic"],
                        ChocoPackageId = "xnview",
                        WebsiteUrl = "https://www.xnview.com/en/xnview/",
                        IconSources = [
                            "https://www.xnview.com/img/app-xnview-512.png",
                            "https://upload.wikimedia.org/wikipedia/en/7/7e/XnView_logo.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-inkscape",
                        Name = "Inkscape",
                        Description = "Open-source SVG vector graphics editor",
                        GroupName = "Imaging",
                        AppxPackageName = ["25415Inkscape.Inkscape"],
                        WinGetPackageId = ["Inkscape.Inkscape"],
                        ChocoPackageId = "inkscape",
                        MsStoreId = "9PD9BHGLFC7H",
                        WebsiteUrl = "https://inkscape.org/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/0/0d/Inkscape_Logo.svg/250px-Inkscape_Logo.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-greenshot",
                        Name = "Greenshot",
                        Description = "Screenshot tool with annotation features",
                        RegistryDisplayName = "Greenshot {version}",
                        GroupName = "Imaging",
                        WinGetPackageId = ["Greenshot.Greenshot"],
                        ChocoPackageId = "greenshot",
                        WebsiteUrl = "https://getgreenshot.org/",
                        IconSources = [
                            "https://raw.githubusercontent.com/greenshot/greenshot/main/src/Greenshot/icons/applicationIcon/90.png",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/1/12/Greenshot_logo.svg/250px-Greenshot_logo.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-sharex",
                        Name = "ShareX",
                        Description = "Screen capture, file sharing and productivity tool",
                        GroupName = "Imaging",
                        AppxPackageName = ["19568ShareX.ShareX"],
                        WinGetPackageId = ["ShareX.ShareX"],
                        ChocoPackageId = "sharex",
                        MsStoreId = "9NBLGGH4Z1SP",
                        WebsiteUrl = "https://getsharex.com/",
                        IconSources = [
                            "https://getsharex.com/img/ShareX_Icon.ico",
                            "https://raw.githubusercontent.com/ShareX/ShareX/master/ShareX/Resources/application-icon-large.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-flameshot",
                        Name = "Flameshot",
                        Description = "Powerful yet simple to use screenshot software",
                        GroupName = "Imaging",
                        WinGetPackageId = ["Flameshot.Flameshot"],
                        ChocoPackageId = "flameshot",
                        WebsiteUrl = "https://flameshot.org/",
                        IconSources = [
                            "https://raw.githubusercontent.com/flameshot-org/flameshot/master/data/img/app/org.flameshot.Flameshot-1024.png",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/f/f6/Flameshot_logo.svg/250px-Flameshot_logo.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-faststone",
                        Name = "FastStone Image Viewer",
                        Description = "Image browser, converter and editor",
                        GroupName = "Imaging",
                        WinGetPackageId = ["FastStone.Viewer"],
                        ChocoPackageId = "fsviewer",
                        WebsiteUrl = "https://www.faststone.org/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/d/d8/FSViewer_logo.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-imageglass",
                        Name = "ImageGlass",
                        Description = "Lightweight, versatile image viewer",
                        GroupName = "Imaging",
                        RegistryDisplayName = "ImageGlass",
                        WinGetPackageId = ["DuongDieuPhap.ImageGlass"],
                        MsStoreId = "9N33VZK3C7TH",
                        ChocoPackageId = "imageglass",
                        WebsiteUrl = "https://imageglass.org/",
                        IconSources = [
                            "https://raw.githubusercontent.com/d2phap/ImageGlass/develop/Assets/Logo/2023/logo512.png",
                            "https://raw.githubusercontent.com/d2phap/ImageGlass/develop/Assets/Logo/2023/icon256.ico",
                        ],
                    }
                }
            };
        }
    }
}
