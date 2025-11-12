using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models
{
    public static partial class ExternalAppDefinitions
    {
        public static class DocumentViewers
        {
            public static ItemGroup GetDocumentViewers()
            {
                return new ItemGroup
                {
                    Name = "Document Viewers",
                    FeatureId = FeatureIds.ExternalApps,
                    Items = new List<ItemDefinition>
                    {
                        new ItemDefinition
                        {
                            Id = "external-app-libreoffice",
                            Name = "LibreOffice",
                            Description = "Free and open-source office suite",
                            GroupName = "Document Viewers",
                            WinGetPackageId = "TheDocumentFoundation.LibreOffice",
                            Category = "Document Viewers",
                            WebsiteUrl = "https://www.libreoffice.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-onlyoffice",
                            Name = "ONLYOFFICE Desktop Editors",
                            Description = "100% open-source free alternative to Microsoft Office",
                            GroupName = "Document Viewers",
                            WinGetPackageId = "ONLYOFFICE.DesktopEditors",
                            Category = "Document Viewers",
                            WebsiteUrl = "https://www.onlyoffice.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-foxit-reader",
                            Name = "Foxit Reader",
                            Description = "Lightweight PDF reader with advanced features",
                            GroupName = "Document Viewers",
                            WinGetPackageId = "Foxit.FoxitReader.Inno",
                            Category = "Document Viewers",
                            WebsiteUrl = "https://www.foxit.com/pdf-reader/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-sumatra-pdf",
                            Name = "SumatraPDF",
                            Description = "PDF, eBook (epub, mobi), comic book (cbz/cbr), DjVu, XPS, CHM, image viewer for Windows",
                            GroupName = "Document Viewers",
                            WinGetPackageId = "SumatraPDF.SumatraPDF",
                            Category = "Document Viewers",
                            WebsiteUrl = "https://www.sumatrapdfreader.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-openoffice",
                            Name = "OpenOffice",
                            Description = "Discontinued open-source office suite. Active successor projects is LibreOffice",
                            GroupName = "Document Viewers",
                            WinGetPackageId = "Apache.OpenOffice",
                            Category = "Document Viewers",
                            WebsiteUrl = "https://www.openoffice.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-adobe-reader",
                            Name = "Adobe Acrobat Reader DC",
                            Description = "PDF reader and editor",
                            GroupName = "Document Viewers",
                            WinGetPackageId = "XPDP273C0XHQH2",
                            Category = "Document Viewers",
                            WebsiteUrl = "https://www.adobe.com/acrobat/pdf-reader.html"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-evernote",
                            Name = "Evernote",
                            Description = "Note-taking app",
                            GroupName = "Document Viewers",
                            WinGetPackageId = "Evernote.Evernote",
                            Category = "Document Viewers",
                            WebsiteUrl = "https://evernote.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-cherrytree",
                            Name = "CherryTree",
                            Description = "Hierarchical note taking application with rich text and syntax highlighting",
                            GroupName = "Document Viewers",
                            WinGetPackageId = "Giuspen.Cherrytree",
                            Category = "Document Viewers",
                            WebsiteUrl = "https://www.giuspen.net/cherrytree/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-okular",
                            Name = "Okular",
                            Description = "Universal document viewer supporting PDF, eBook, and more",
                            GroupName = "Document Viewers",
                            WinGetPackageId = "KDE.Okular",
                            Category = "Document Viewers",
                            WebsiteUrl = "https://okular.kde.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-pdf24-creator",
                            Name = "PDF24 Creator",
                            Description = "Free PDF creator and converter",
                            GroupName = "Document Viewers",
                            WinGetPackageId = "geeksoftwareGmbH.PDF24Creator",
                            Category = "Document Viewers",
                            WebsiteUrl = "https://www.pdf24.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-microsoft-365",
                            Name = "Microsoft 365",
                            Description = "Microsoft Office productivity suite",
                            GroupName = "Document Viewers",
                            WinGetPackageId = "Microsoft.Office",
                            Category = "Document Viewers",
                            WebsiteUrl = "https://www.microsoft.com/microsoft-365"
                        }
                    }
                };
            }
        }
    }
}