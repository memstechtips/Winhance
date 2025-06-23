using System.Collections.Generic;

namespace Winhance.Core.Features.SoftwareApps.Models;

/// <summary>
/// Represents a catalog of external applications that can be installed.
/// </summary>
public class ExternalAppCatalog
{
    /// <summary>
    /// Gets or sets the collection of installable external applications.
    /// </summary>
    public IReadOnlyList<AppInfo> ExternalApps { get; init; } = new List<AppInfo>();

    /// <summary>
    /// Creates a default external app catalog with predefined installable apps.
    /// </summary>
    /// <returns>A new ExternalAppCatalog instance with default apps.</returns>
    public static ExternalAppCatalog CreateDefault()
    {
        return new ExternalAppCatalog { ExternalApps = CreateDefaultExternalApps() };
    }

    private static IReadOnlyList<AppInfo> CreateDefaultExternalApps()
    {
        return new List<AppInfo>
        {
            // Browsers
            new AppInfo
            {
                Name = "Microsoft Edge WebView",
                Description = "WebView2 runtime for Windows applications",
                PackageName = "Microsoft.EdgeWebView2Runtime",
                Category = "Browsers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Thorium",
                Description = "Chromium-based browser with enhanced privacy features",
                PackageName = "Alex313031.Thorium",
                Category = "Browsers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Thorium AVX2",
                Description = "Chromium-based browser with enhanced privacy features",
                PackageName = "Alex313031.Thorium.AVX2",
                Category = "Browsers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Mercury",
                Description = "Compiler optimized, private Firefox fork",
                PackageName = "Alex313031.Mercury",
                Category = "Browsers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Firefox",
                Description = "Popular web browser known for privacy and customization",
                PackageName = "Mozilla.Firefox",
                Category = "Browsers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Chrome",
                Description = "Google's web browser with sync and extension support",
                PackageName = "Google.Chrome",
                Category = "Browsers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Ungoogled Chromium",
                Description = "Chromium-based browser with privacy enhancements",
                PackageName = "Eloston.Ungoogled-Chromium",
                Category = "Browsers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Brave",
                Description = "Privacy-focused browser with built-in ad blocking",
                PackageName = "Brave.Brave",
                Category = "Browsers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Opera",
                Description = "Feature-rich web browser with built-in VPN and ad blocker",
                PackageName = "Opera.Opera",
                Category = "Browsers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Opera GX",
                Description = "Gaming-oriented version of Opera with unique features",
                PackageName = "Opera.OperaGX",
                Category = "Browsers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Arc Browser",
                Description = "Innovative browser with a focus on design and user experience",
                PackageName = "TheBrowserCompany.Arc",
                Category = "Browsers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Tor Browser",
                Description = "Privacy-focused browser that routes traffic through the Tor network",
                PackageName = "TorProject.TorBrowser",
                Category = "Browsers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Vivaldi",
                Description = "Highly customizable browser with a focus on user control",
                PackageName = "Vivaldi.Vivaldi",
                Category = "Browsers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Waterfox",
                Description = "Firefox-based browser with a focus on privacy and customization",
                PackageName = "Waterfox.Waterfox",
                Category = "Browsers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Zen Browser",
                Description = "Privacy-focused browser with built-in ad blocking",
                PackageName = "Zen-Team.Zen-Browser",
                Category = "Browsers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Mullvad Browser",
                Description =
                    "Privacy-focused browser designed to minimize tracking and fingerprints",
                PackageName = "MullvadVPN.MullvadBrowser",
                Category = "Browsers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Pale Moon Browser",
                Description =
                    "Open Source, Goanna-based web browser focusing on efficiency and customization",
                PackageName = "MoonchildProductions.PaleMoon",
                Category = "Browsers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Maxthon Browser",
                Description = "Privacy focused browser with built-in ad blocking and VPN",
                PackageName = "Maxthon.Maxthon",
                Category = "Browsers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Floorp",
                Description = "Privacy focused browser with strong tracking protection",
                PackageName = "Ablaze.Floorp",
                Category = "Browsers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "DuckDuckGo",
                Description = "Privacy-focused search engine with a browser extension",
                PackageName = "DuckDuckGo.DesktopBrowser",
                Category = "Browsers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            // Document Viewers
            new AppInfo
            {
                Name = "LibreOffice",
                Description = "Free and open-source office suite",
                PackageName = "TheDocumentFoundation.LibreOffice",
                Category = "Document Viewers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "ONLYOFFICE Desktop Editors",
                Description = "100% open-source free alternative to Microsoft Office",
                PackageName = "ONLYOFFICE.DesktopEditors",
                Category = "Document Viewers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Foxit Reader",
                Description = "Lightweight PDF reader with advanced features",
                PackageName = "Foxit.FoxitReader",
                Category = "Document Viewers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "SumatraPDF",
                Description =
                    "PDF, eBook (epub, mobi), comic book (cbz/cbr), DjVu, XPS, CHM, image viewer for Windows",
                PackageName = "SumatraPDF.SumatraPDF",
                Category = "Document Viewers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "OpenOffice",
                Description =
                    "Discontinued open-source office suite. Active successor projects is LibreOffice",
                PackageName = "Apache.OpenOffice",
                Category = "Document Viewers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Adobe Acrobat Reader DC",
                Description = "PDF reader and editor",
                PackageName = "XPDP273C0XHQH2",
                Category = "Document Viewers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Evernote",
                Description = "Note-taking app",
                PackageName = "Evernote.Evernote",
                Category = "Document Viewers",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            // Online Storage
            new AppInfo
            {
                Name = "Google Drive",
                Description = "Cloud storage and file synchronization service",
                PackageName = "Google.GoogleDrive",
                Category = "Online Storage",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Dropbox",
                Description =
                    "File hosting service that offers cloud storage, file synchronization, personal cloud",
                PackageName = "Dropbox.Dropbox",
                Category = "Online Storage",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "SugarSync",
                Description =
                    "Automatically access and share your photos, videos, and files in any folder",
                PackageName = "IPVanish.SugarSync",
                Category = "Online Storage",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "NextCloud",
                Description =
                    "Access, share and protect your files, calendars, contacts, communication & more at home and in your organization",
                PackageName = "Nextcloud.NextcloudDesktop",
                Category = "Online Storage",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Proton Drive",
                Description = "Secure cloud storage with end-to-end encryption",
                PackageName = "Proton.ProtonDrive",
                Category = "Online Storage",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            // Development Apps
            new AppInfo
            {
                Name = "Python 3.13",
                Description = "Python programming language",
                PackageName = "Python.Python.3.13",
                Category = "Development Apps",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Notepad++",
                Description = "Free source code editor and Notepad replacement",
                PackageName = "Notepad++.Notepad++",
                Category = "Development Apps",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "WinSCP",
                Description = "Free SFTP, SCP, Amazon S3, WebDAV, and FTP client",
                PackageName = "WinSCP.WinSCP",
                Category = "Development Apps",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "PuTTY",
                Description = "Free SSH and telnet client",
                PackageName = "PuTTY.PuTTY",
                Category = "Development Apps",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "WinMerge",
                Description = "Open source differencing and merging tool",
                PackageName = "WinMerge.WinMerge",
                Category = "Development Apps",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Eclipse",
                Description = "Java IDE and development platform",
                PackageName = "EclipseFoundation.EclipseIDEforJavaDevelopers",
                Category = "Development Apps",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Visual Studio Code",
                Description = "Code editor with support for development operations",
                PackageName = "Microsoft.VisualStudioCode",
                Category = "Development Apps",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Git",
                Description = "Distributed version control system",
                PackageName = "Git.Git",
                Category = "Development Apps",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "GitHub Desktop",
                Description = "GitHub desktop client",
                PackageName = "GitHub.GitHubDesktop",
                Category = "Development Apps",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "AutoHotkey",
                Description = "Scripting language for desktop automation",
                PackageName = "AutoHotkey.AutoHotkey",
                Category = "Development Apps",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Windsurf",
                Description = "AI Code Editor",
                PackageName = "Codeium.Windsurf",
                Category = "Development Apps",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Cursor",
                Description = "AI Code Editor",
                PackageName = "Anysphere.Cursor",
                Category = "Development Apps",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            // Multimedia (Audio & Video)
            new AppInfo
            {
                Name = "VLC",
                Description = "Open-source multimedia player and framework",
                PackageName = "VideoLAN.VLC",
                Category = "Multimedia (Audio & Video)",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "iTunes",
                Description = "Media player and library",
                PackageName = "Apple.iTunes",
                Category = "Multimedia (Audio & Video)",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "AIMP",
                Description = "Audio player with support for various formats",
                PackageName = "AIMP.AIMP",
                Category = "Multimedia (Audio & Video)",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "foobar2000",
                Description = "Advanced audio player for Windows",
                PackageName = "PeterPawlowski.foobar2000",
                Category = "Multimedia (Audio & Video)",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "MusicBee",
                Description = "Music manager and player",
                PackageName = "9P4CLT2RJ1RS",
                Category = "Multimedia (Audio & Video)",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Audacity",
                Description = "Audio editor and recorder",
                PackageName = "Audacity.Audacity",
                Category = "Multimedia (Audio & Video)",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "GOM",
                Description = "Media player for Windows",
                PackageName = "GOMLab.GOMPlayer",
                Category = "Multimedia (Audio & Video)",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Spotify",
                Description = "Music streaming service",
                PackageName = "Spotify.Spotify",
                Category = "Multimedia (Audio & Video)",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "MediaMonkey",
                Description = "Media manager and player",
                PackageName = "VentisMedia.MediaMonkey.5",
                Category = "Multimedia (Audio & Video)",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "HandBrake",
                Description = "Open-source video transcoder",
                PackageName = "HandBrake.HandBrake",
                Category = "Multimedia (Audio & Video)",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "OBS Studio",
                Description =
                    "Free and open source software for video recording and live streaming",
                PackageName = "OBSProject.OBSStudio",
                Category = "Multimedia (Audio & Video)",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Streamlabs OBS",
                Description = "Streaming software built on OBS with additional features for streamers",
                PackageName = "Streamlabs.StreamlabsOBS",
                Category = "Multimedia (Audio & Video)",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "MPC-BE",
                Description = "Media Player Classic - Black Edition",
                PackageName = "MPC-BE.MPC-BE",
                Category = "Multimedia (Audio & Video)",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "K-Lite Codec Pack (Mega)",
                Description = "Collection of codecs and related tools",
                PackageName = "CodecGuide.K-LiteCodecPack.Mega",
                Category = "Multimedia (Audio & Video)",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "CapCut",
                Description = "Video editor",
                PackageName = "ByteDance.CapCut",
                Category = "Multimedia (Audio & Video)",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "PotPlayer",
                Description = "Comprehensive multimedia player for Windows",
                PackageName = "Daum.PotPlayer",
                Category = "Multimedia (Audio & Video)",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            // Imaging
            new AppInfo
            {
                Name = "IrfanView",
                Description = "Fast and compact image viewer and converter",
                PackageName = "IrfanSkiljan.IrfanView",
                Category = "Imaging",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Krita",
                Description = "Digital painting and illustration software",
                PackageName = "KDE.Krita",
                Category = "Imaging",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Blender",
                Description = "3D creation suite",
                PackageName = "BlenderFoundation.Blender",
                Category = "Imaging",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Paint.NET",
                Description = "Image and photo editing software",
                PackageName = "dotPDN.PaintDotNet",
                Category = "Imaging",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "GIMP",
                Description = "GNU Image Manipulation Program",
                PackageName = "GIMP.GIMP.3",
                Category = "Imaging",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "XnViewMP",
                Description = "Image viewer, browser and converter",
                PackageName = "XnSoft.XnViewMP",
                Category = "Imaging",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "XnView Classic",
                Description = "Image viewer, browser and converter (Classic Version)",
                PackageName = "XnSoft.XnView.Classic",
                Category = "Imaging",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Inkscape",
                Description = "Vector graphics editor",
                PackageName = "Inkscape.Inkscape",
                Category = "Imaging",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Greenshot",
                Description = "Screenshot tool with annotation features",
                PackageName = "Greenshot.Greenshot",
                Category = "Imaging",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "ShareX",
                Description = "Screen capture, file sharing and productivity tool",
                PackageName = "ShareX.ShareX",
                Category = "Imaging",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Flameshot",
                Description = "Powerful yet simple to use screenshot software",
                PackageName = "Flameshot.Flameshot",
                Category = "Imaging",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "FastStone",
                Description = "Image browser, converter and editor",
                PackageName = "FastStone.Viewer",
                Category = "Imaging",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            // Compression
            new AppInfo
            {
                Name = "7-Zip",
                Description = "Open-source file archiver with a high compression ratio",
                PackageName = "7zip.7zip",
                Category = "Compression",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "WinRAR",
                Description = "File archiver with a high compression ratio",
                PackageName = "RARLab.WinRAR",
                Category = "Compression",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "PeaZip",
                Description =
                    "Free file archiver utility. Open and extract RAR, TAR, ZIP files and more",
                PackageName = "Giorgiotani.Peazip",
                Category = "Compression",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "NanaZip",
                Description =
                    "Open source fork of 7-zip intended for the modern Windows experience",
                PackageName = "M2Team.NanaZip",
                Category = "Compression",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            // Messaging, Email & Calendar
            new AppInfo
            {
                Name = "Telegram",
                Description = "Instant messaging and voice calling app",
                PackageName = "Telegram.TelegramDesktop",
                Category = "Messaging, Email & Calendar",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Whatsapp",
                Description = "Instant messaging and voice calling app",
                PackageName = "9NKSQGP7F2NH",
                Category = "Messaging, Email & Calendar",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Zoom",
                Description = "Video conferencing and messaging platform",
                PackageName = "Zoom.Zoom",
                Category = "Messaging, Email & Calendar",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Discord",
                Description = "Voice, video and text communication service",
                PackageName = "Discord.Discord",
                Category = "Messaging, Email & Calendar",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Pidgin",
                Description = "Multi-protocol instant messaging client",
                PackageName = "Pidgin.Pidgin",
                Category = "Messaging, Email & Calendar",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Thunderbird",
                Description = "Free email application",
                PackageName = "Mozilla.Thunderbird",
                Category = "Messaging, Email & Calendar",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "eMClient",
                Description = "Email client with calendar, tasks, and chat",
                PackageName = "eMClient.eMClient",
                Category = "Messaging, Email & Calendar",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Proton Mail",
                Description = "Secure email service with end-to-end encryption",
                PackageName = "Proton.ProtonMail",
                Category = "Messaging, Email & Calendar",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Trillian",
                Description = "Instant messaging application",
                PackageName = "CeruleanStudios.Trillian",
                Category = "Messaging, Email & Calendar",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            // File & Disk Management
            new AppInfo
            {
                Name = "WinDirStat",
                Description = "Disk usage statistics viewer and cleanup tool",
                PackageName = "WinDirStat.WinDirStat",
                Category = "File & Disk Management",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "WizTree",
                Description = "Disk space analyzer with extremely fast scanning",
                PackageName = "AntibodySoftware.WizTree",
                Category = "File & Disk Management",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "TreeSize Free",
                Description = "Disk space manager",
                PackageName = "JAMSoftware.TreeSize.Free",
                Category = "File & Disk Management",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Everything",
                Description = "Locate files and folders by name instantly",
                PackageName = "voidtools.Everything",
                Category = "File & Disk Management",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "TeraCopy",
                Description = "Copy files faster and more securely",
                PackageName = "CodeSector.TeraCopy",
                Category = "File & Disk Management",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "File Converter",
                Description = "Batch file converter for Windows",
                PackageName = "AdrienAllard.FileConverter",
                Category = "File & Disk Management",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Crystal Disk Info",
                Description = "Hard drive health monitoring utility",
                PackageName = "WsSolInfor.CrystalDiskInfo",
                Category = "File & Disk Management",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Bulk Rename Utility",
                Description = "File renaming software for Windows",
                PackageName = "TGRMNSoftware.BulkRenameUtility",
                Category = "File & Disk Management",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "IObit Unlocker",
                Description = "Tool to unlock files that are in use by other processes",
                PackageName = "IObit.IObitUnlocker",
                Category = "File & Disk Management",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Ventoy",
                Description = "Open source tool to create bootable USB drive for ISO files",
                PackageName = "Ventoy.Ventoy",
                Category = "File & Disk Management",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Volume2",
                Description = "Advanced Windows volume control",
                PackageName = "irzyxa.Volume2Portable",
                Category = "File & Disk Management",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            // Remote Access
            new AppInfo
            {
                Name = "RustDesk",
                Description = "Fast Open-Source Remote Access and Support Software",
                PackageName = "RustDesk.RustDesk",
                Category = "Remote Access",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Input Leap",
                Description = "Open-source KVM software for sharing mouse and keyboard between computers",
                PackageName = "input-leap.input-leap",
                Category = "Remote Access",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "AnyDesk",
                Description = "Remote desktop software for remote access and support",
                PackageName = "AnyDesk.AnyDesk",
                Category = "Remote Access",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "TeamViewer 15",
                Description =
                    "Remote control, desktop sharing, online meetings, web conferencing and file transfer",
                PackageName = "TeamViewer.TeamViewer",
                Category = "Remote Access",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "RealVNC Server",
                Description = "Remote access software",
                PackageName = "RealVNC.VNCServer",
                Category = "Remote Access",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "RealVNC Viewer",
                Description = "Remote access software",
                PackageName = "RealVNC.VNCViewer",
                Category = "Remote Access",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Chrome Remote Desktop Host",
                Description = "Remote access to your computer through Chrome browser",
                PackageName = "Google.ChromeRemoteDesktopHost",
                Category = "Remote Access",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            // Optical Disc Tools
            new AppInfo
            {
                Name = "ImgBurn",
                Description = "Lightweight CD / DVD / HD DVD / Blu-ray burning application",
                PackageName = "LIGHTNINGUK.ImgBurn",
                Category = "Optical Disc Tools",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "AnyBurn",
                Description = "Lightweight CD/DVD/Blu-ray burning software",
                PackageName = "PowerSoftware.AnyBurn",
                Category = "Optical Disc Tools",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            // Other Utilities
            new AppInfo
            {
                Name = "Snappy Driver Installer Origin",
                Description = "Driver installer and updater",
                PackageName = "GlennDelahoy.SnappyDriverInstallerOrigin",
                Category = "Other Utilities",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
                        new AppInfo
            {
                Name = "Wise Registry Cleaner",
                Description = "Registry cleaning and optimization tool",
                PackageName = "XPDLS1XBTXVPP4",
                Category = "Other Utilities",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "UniGetUI",
                Description =
                    "Universal package manager interface supporting WinGet, Chocolatey, and more",
                PackageName = "MartiCliment.UniGetUI",
                Category = "Other Utilities",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Google Earth",
                Description = "3D representation of Earth based on satellite imagery",
                PackageName = "Google.GoogleEarthPro",
                Category = "Other Utilities",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "NV Access",
                Description = "Screen reader for blind and vision impaired users",
                PackageName = "NVAccess.NVDA",
                Category = "Other Utilities",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Revo Uninstaller",
                Description = "Uninstaller with advanced features",
                PackageName = "RevoUninstaller.RevoUninstaller",
                Category = "Other Utilities",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Bulk Crap Uninstaller",
                Description = "Free and open-source program uninstaller with advanced features",
                PackageName = "Klocman.BulkCrapUninstaller",
                Category = "Other Utilities",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Text Grab",
                Description = "Tool for extracting text from images and screenshots",
                PackageName = "JosephFinney.Text-Grab",
                Category = "Other Utilities",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Glary Utilities",
                Description = "All-in-one PC care utility",
                PackageName = "Glarysoft.GlaryUtilities",
                Category = "Other Utilities",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Buzz",
                Description = "AI video & audio transcription tool",
                PackageName = "ChidiWilliams.Buzz",
                Category = "Other Utilities",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "PowerToys",
                Description = "Windows system utilities to maximize productivity",
                PackageName = "Microsoft.PowerToys",
                Category = "Other Utilities",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            // Customization Utilities
            new AppInfo
            {
                Name = "Nilesoft Shell",
                Description = "Windows context menu customization tool",
                PackageName = "Nilesoft.Shell",
                Category = "Customization Utilities",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "StartAllBack",
                Description = "Windows 11 Start menu and taskbar customization",
                PackageName = "StartIsBack.StartAllBack",
                Category = "Customization Utilities",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Open-Shell",
                Description = "Classic style Start Menu for Windows",
                PackageName = "Open-Shell.Open-Shell-Menu",
                Category = "Customization Utilities",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Windhawk",
                Description = "Customization platform for Windows",
                PackageName = "RamenSoftware.Windhawk",
                Category = "Customization Utilities",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Lively Wallpaper",
                Description = "Free and open-source animated desktop wallpaper application",
                PackageName = "rocksdanister.LivelyWallpaper",
                Category = "Customization Utilities",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Sucrose Wallpaper Engine",
                Description = "Free and open-source animated desktop wallpaper application",
                PackageName = "Taiizor.SucroseWallpaperEngine",
                Category = "Customization Utilities",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Rainmeter",
                Description = "Desktop customization tool for Windows",
                PackageName = "Rainmeter.Rainmeter",
                Category = "Customization Utilities",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "ExplorerPatcher",
                Description = "Utility that enhances the Windows Explorer experience",
                PackageName = "valinet.ExplorerPatcher",
                Category = "Customization Utilities",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            // Gaming
            new AppInfo
            {
                Name = "Steam",
                Description = "Digital distribution platform for PC gaming",
                PackageName = "Valve.Steam",
                Category = "Gaming",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Epic Games Launcher",
                Description = "Digital distribution platform for PC gaming",
                PackageName = "EpicGames.EpicGamesLauncher",
                Category = "Gaming",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "EA Desktop App",
                Description = "Digital distribution platform for PC gaming",
                PackageName = "ElectronicArts.EADesktop",
                Category = "Gaming",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Ubisoft Connect",
                Description = "Digital distribution platform for PC gaming",
                PackageName = "Ubisoft.Connect",
                Category = "Gaming",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Battle.net",
                Description = "Digital distribution platform for PC gaming",
                PackageName = "Blizzard.BattleNet",
                Category = "Gaming",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            // Privacy & Security
            new AppInfo
            {
                Name = "Malwarebytes",
                Description = "Anti-malware software for Windows",
                PackageName = "Malwarebytes.Malwarebytes",
                Category = "Privacy & Security",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Malwarebytes AdwCleaner",
                Description = "Adware removal tool for Windows",
                PackageName = "Malwarebytes.AdwCleaner",
                Category = "Privacy & Security",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "SUPERAntiSpyware",
                Description = "Anti-spyware software for Windows",
                PackageName = "SUPERAntiSpyware.SUPERAntiSpyware",
                Category = "Privacy & Security",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "ProtonVPN",
                Description = "Secure and private VPN service",
                PackageName = "Proton.ProtonVPN",
                Category = "Privacy & Security",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "KeePass 2",
                Description = "Free, open source, light-weight password manager",
                PackageName = "DominikReichl.KeePass",
                Category = "Privacy & Security",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Proton Pass",
                Description = "Secure password manager with end-to-end encryption",
                PackageName = "Proton.ProtonPass",
                Category = "Privacy & Security",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Bitwarden",
                Description = "Open source password manager",
                PackageName = "Bitwarden.Bitwarden",
                Category = "Privacy & Security",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "KeePassXC",
                Description = "Cross-platform secure password manager",
                PackageName = "KeePassXCTeam.KeePassXC",
                Category = "Privacy & Security",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
            new AppInfo
            {
                Name = "Tailscale",
                Description = "Zero config VPN for building secure networks",
                PackageName = "Tailscale.Tailscale",
                Category = "Privacy & Security",
                IsCustomInstall = true,
                Type = AppType.StandardApp,
            },
        };
    }
}
