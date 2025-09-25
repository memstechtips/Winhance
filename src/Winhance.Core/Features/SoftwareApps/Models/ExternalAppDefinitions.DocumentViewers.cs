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
                            Category = "Document Viewers"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-onlyoffice",
                            Name = "ONLYOFFICE Desktop Editors",
                            Description = "100% open-source free alternative to Microsoft Office",
                            GroupName = "Document Viewers",
                            WinGetPackageId = "ONLYOFFICE.DesktopEditors",
                            Category = "Document Viewers"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-foxit-reader",
                            Name = "Foxit Reader",
                            Description = "Lightweight PDF reader with advanced features",
                            GroupName = "Document Viewers",
                            WinGetPackageId = "Foxit.FoxitReader",
                            Category = "Document Viewers"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-sumatra-pdf",
                            Name = "SumatraPDF",
                            Description = "PDF, eBook (epub, mobi), comic book (cbz/cbr), DjVu, XPS, CHM, image viewer for Windows",
                            GroupName = "Document Viewers",
                            WinGetPackageId = "SumatraPDF.SumatraPDF",
                            Category = "Document Viewers"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-openoffice",
                            Name = "OpenOffice",
                            Description = "Discontinued open-source office suite. Active successor projects is LibreOffice",
                            GroupName = "Document Viewers",
                            WinGetPackageId = "Apache.OpenOffice",
                            Category = "Document Viewers"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-adobe-reader",
                            Name = "Adobe Acrobat Reader DC",
                            Description = "PDF reader and editor",
                            GroupName = "Document Viewers",
                            WinGetPackageId = "XPDP273C0XHQH2",
                            Category = "Document Viewers"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-evernote",
                            Name = "Evernote",
                            Description = "Note-taking app",
                            GroupName = "Document Viewers",
                            WinGetPackageId = "Evernote.Evernote",
                            Category = "Document Viewers"
                        }
                    }
                };
            }
        }
    }
}