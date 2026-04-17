using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models;

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
                        RegistryDisplayName = "LibreOffice {version}",
                        GroupName = "Document Viewers",
                        WinGetPackageId = ["TheDocumentFoundation.LibreOffice"],
                        ChocoPackageId = "libreoffice-fresh",
                        WebsiteUrl = "https://www.libreoffice.org/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-onlyoffice",
                        Name = "ONLYOFFICE Desktop Editors",
                        Description = "100% open-source free alternative to Microsoft Office",
                        RegistryDisplayName = "ONLYOFFICE {version} ({arch})",
                        GroupName = "Document Viewers",
                        WinGetPackageId = ["ONLYOFFICE.DesktopEditors"],
                        ChocoPackageId = "onlyoffice",
                        WebsiteUrl = "https://www.onlyoffice.com/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-pdfgear",
                        Name = "PDFgear",
                        Description = "Read, edit, convert, merge, and sign PDF files across devices, for completely free and without signing up.",
                        GroupName = "Document Viewers",
                        WinGetPackageId = ["PDFgear.PDFgear"],
                        ChocoPackageId = "pdfgear",
                        WebsiteUrl = "https://www.pdfgear.com/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-foxit-reader",
                        Name = "Foxit PDF Reader",
                        Description = "Lightweight PDF reader with advanced features",
                        GroupName = "Document Viewers",
                        MsStoreId = "XPFCG5NRKXQPKT", // MS Store package
                        ChocoPackageId = "foxitreader",
                        WebsiteUrl = "https://www.foxit.com/pdf-reader/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-sumatra-pdf",
                        Name = "SumatraPDF",
                        Description = "PDF, eBook (epub, mobi), comic book (cbz/cbr), DjVu, XPS, CHM, image viewer for Windows",
                        GroupName = "Document Viewers",
                        WinGetPackageId = ["SumatraPDF.SumatraPDF"],
                        ChocoPackageId = "sumatrapdf",
                        WebsiteUrl = "https://www.sumatrapdfreader.org/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-openoffice",
                        Name = "OpenOffice",
                        Description = "Discontinued open-source office suite. Active successor projects is LibreOffice",
                        RegistryDisplayName = "OpenOffice {version}",
                        GroupName = "Document Viewers",
                        WinGetPackageId = ["Apache.OpenOffice"],
                        ChocoPackageId = "openoffice",
                        WebsiteUrl = "https://www.openoffice.org/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-adobe-reader",
                        Name = "Adobe Acrobat Reader DC",
                        Description = "PDF reader and editor",
                        RegistryDisplayName = "Adobe Acrobat ({arch})",
                        GroupName = "Document Viewers",
                        MsStoreId = "XPDP273C0XHQH2", // MS Store package
                        ChocoPackageId = "adobereader",
                        WebsiteUrl = "https://www.adobe.com/acrobat/pdf-reader.html"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-evernote",
                        Name = "Evernote",
                        Description = "Note-taking app",
                        RegistryDisplayName = "Evernote {version}",
                        GroupName = "Document Viewers",
                        WinGetPackageId = ["Evernote.Evernote"],
                        ChocoPackageId = "evernote",
                        WebsiteUrl = "https://evernote.com/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-cherrytree",
                        Name = "CherryTree",
                        Description = "Hierarchical note taking application with rich text and syntax highlighting",
                        RegistryDisplayName = "CherryTree version {version}",
                        GroupName = "Document Viewers",
                        WinGetPackageId = ["Giuspen.Cherrytree"],
                        ChocoPackageId = "cherrytree",
                        WebsiteUrl = "https://www.giuspen.net/cherrytree/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-okular",
                        Name = "Okular",
                        Description = "Universal document viewer supporting PDF, eBook, and more",
                        GroupName = "Document Viewers",
                        AppxPackageName = ["KDEe.V.Okular"],
                        WinGetPackageId = ["KDE.Okular", "KDE.Okular.AppX"],
                        ChocoPackageId = "okular",
                        MsStoreId = "9N41MSQ1WNM8",
                        WebsiteUrl = "https://okular.kde.org/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-pdf24-creator",
                        Name = "PDF24 Creator",
                        Description = "Free PDF creator and converter",
                        GroupName = "Document Viewers",
                        WinGetPackageId = ["geeksoftwareGmbH.PDF24Creator"],
                        ChocoPackageId = "pdf24",
                        WebsiteUrl = "https://www.pdf24.org/"
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-microsoft-365",
                        Name = "Microsoft 365",
                        Description = "Microsoft Office productivity suite",
                        RegistrySubKeyName = "O365ProPlusRetail - {locale}",
                        RegistryDisplayName = "Microsoft 365 Apps for enterprise - {locale}",
                        GroupName = "Document Viewers",
                        WinGetPackageId = ["Microsoft.Office"],
                        ChocoPackageId = "office365business",
                        WebsiteUrl = "https://www.microsoft.com/microsoft-365"
                    }
                }
            };
        }
    }
}
