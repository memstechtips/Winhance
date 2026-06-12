using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.SoftwareApps.Models;

public static partial class ExternalAppDefinitions
{
    public static class Multimedia
    {
        public static ItemGroup GetMultimedia()
        {
            return new ItemGroup
            {
                Name = "Multimedia (Audio & Video)",
                FeatureId = FeatureIds.ExternalApps,
                Items = new List<ItemDefinition>
                {
                    new ItemDefinition
                    {
                        Id = "external-app-vlc",
                        Name = "VLC media player",
                        Description = "Open-source multimedia player and framework",
                        GroupName = "Multimedia (Audio & Video)",
                        AppxPackageName = ["VideoLAN.VLC"],
                        WinGetPackageId = ["VideoLAN.VLC"],
                        ChocoPackageId = "vlc",
                        MsStoreId = "9NBLGGH4VVNH",
                        WebsiteUrl = "https://www.videolan.org/vlc/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-itunes",
                        Name = "iTunes",
                        Description = "Apple's music and media library; legacy iPhone and iPad sync tool",
                        GroupName = "Multimedia (Audio & Video)",
                        AppxPackageName = ["AppleInc.iTunes"],
                        WinGetPackageId = ["Apple.iTunes"],
                        ChocoPackageId = "itunes",
                        MsStoreId = "9PB2MZ1ZMB1S",
                        WebsiteUrl = "https://www.apple.com/itunes/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-aimp",
                        Name = "AIMP",
                        Description = "Audio player with support for various formats",
                        GroupName = "Multimedia (Audio & Video)",
                        AppxPackageName = ["25018ArtemIzmaylov.AIMP"],
                        WinGetPackageId = ["AIMP.AIMP"],
                        ChocoPackageId = "aimp",
                        MsStoreId = "9PCLLLH15SMT",
                        WebsiteUrl = "https://www.aimp.ru/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-foobar2000",
                        Name = "foobar2000",
                        Description = "Advanced audio player for Windows",
                        RegistryDisplayName = "foobar2000 {version} ({arch})",
                        GroupName = "Multimedia (Audio & Video)",
                        AppxPackageName = ["Resolute.foobar2000"],
                        WinGetPackageId = ["PeterPawlowski.foobar2000"],
                        ChocoPackageId = "foobar2000",
                        MsStoreId = "9PDJ8X9SPF2K",
                        WebsiteUrl = "https://www.foobar2000.org/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-musicbee",
                        Name = "MusicBee",
                        Description = "Music manager and player",
                        GroupName = "Multimedia (Audio & Video)",
                        AppxPackageName = ["50072StevenMayall.MusicBee"],
                        MsStoreId = "9P4CLT2RJ1RS",
                        ChocoPackageId = "musicbee",
                        WebsiteUrl = "https://www.getmusicbee.com/",
                        // Icon resolved via MS Store CDN (Layer 2a). No trusted catalog URL.
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-audacity",
                        Name = "Audacity",
                        Description = "Audio editor and recorder",
                        RegistryDisplayName = "Audacity {version}",
                        GroupName = "Multimedia (Audio & Video)",
                        WinGetPackageId = ["Audacity.Audacity"],
                        ChocoPackageId = "audacity",
                        MsStoreId = "XP8K0J757HHRDW",
                        WebsiteUrl = "https://www.audacityteam.org/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-gom-player",
                        Name = "GOM Player",
                        Description = "Media player for Windows",
                        GroupName = "Multimedia (Audio & Video)",
                        WinGetPackageId = ["GOMLab.GOMPlayer"],
                        ChocoPackageId = "gom-player",
                        MsStoreId = "XP8LKPZT4X0Z0P",
                        WebsiteUrl = "https://www.gomlab.com/",
                        // Icon resolved via MS Store CDN (Layer 2a). No trusted catalog URL.
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-spotify",
                        Name = "Spotify",
                        Description = "Music streaming service",
                        GroupName = "Multimedia (Audio & Video)",
                        AppxPackageName = ["SpotifyAB.SpotifyMusic"],
                        WinGetPackageId = ["Spotify.Spotify"],
                        ChocoPackageId = "spotify",
                        MsStoreId = "9NCBCSZSJRSB",
                        WebsiteUrl = "https://www.spotify.com/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-mediamonkey",
                        Name = "MediaMonkey",
                        Description = "Media manager and player",
                        RegistryDisplayName = "MediaMonkey {version}",
                        GroupName = "Multimedia (Audio & Video)",
                        WinGetPackageId = ["VentisMedia.MediaMonkey.5"],
                        ChocoPackageId = "mediamonkey",
                        WebsiteUrl = "https://www.mediamonkey.com/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-handbrake",
                        Name = "HandBrake",
                        Description = "Open-source video transcoder",
                        GroupName = "Multimedia (Audio & Video)",
                        WinGetPackageId = ["HandBrake.HandBrake"],
                        ChocoPackageId = "handbrake",
                        WebsiteUrl = "https://handbrake.fr/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-obs-studio",
                        Name = "OBS Studio",
                        Description = "Free and open source software for video recording and live streaming",
                        GroupName = "Multimedia (Audio & Video)",
                        WinGetPackageId = ["OBSProject.OBSStudio"],
                        ChocoPackageId = "obs-studio",
                        WebsiteUrl = "https://obsproject.com/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-streamlabs-obs",
                        Name = "Streamlabs OBS",
                        Description = "Streaming software built on OBS with additional features for streamers",
                        RegistryDisplayName = "Streamlabs Desktop {version}",
                        GroupName = "Multimedia (Audio & Video)",
                        ChocoPackageId = "streamlabs-obs",
                        WebsiteUrl = "https://streamlabs.com/",
                        // Vendor only ships SVG/WebP and the Wikimedia render is a
                        // narrow wordmark. Embed the on-page Streamlabs Desktop mark
                        // (base64) so the resolver decodes it via the data: branch.
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-mpc-be",
                        Name = "MPC-BE",
                        Description = "Lightweight Media Player Classic fork for video and audio playback",
                        RegistryDisplayName = "MPC-BE {arch} {version}",
                        GroupName = "Multimedia (Audio & Video)",
                        AppxPackageName = ["HaukeGtze.77535DB761F2"],
                        WinGetPackageId = ["MPC-BE.MPC-BE"],
                        ChocoPackageId = "mpc-be",
                        MsStoreId = "9PD88QB3BGKN",
                        WebsiteUrl = "https://sourceforge.net/projects/mpcbe/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-k-lite-codec-pack",
                        Name = "K-Lite Codec Pack (Mega)",
                        Description = "Codec bundle for playing video formats Windows can't open natively",
                        RegistryDisplayName = "K-Lite Mega Codec Pack {version}",
                        GroupName = "Multimedia (Audio & Video)",
                        WinGetPackageId = ["CodecGuide.K-LiteCodecPack.Mega"],
                        ChocoPackageId = "k-litecodecpackmega",
                        WebsiteUrl = "https://codecguide.com/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-capcut",
                        Name = "CapCut",
                        Description = "ByteDance's video editor with templates, effects, and AI tools",
                        GroupName = "Multimedia (Audio & Video)",
                        WinGetPackageId = ["ByteDance.CapCut"],
                        MsStoreId = "XP9KN75RRB9NHS",
                        WebsiteUrl = "https://www.capcut.com/",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://www.capcut.com/activity/download_pc",
                        },
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-potplayer",
                        Name = "PotPlayer64",
                        Description = "Comprehensive multimedia player for Windows",
                        GroupName = "Multimedia (Audio & Video)",
                        WinGetPackageId = ["Daum.PotPlayer"],
                        ChocoPackageId = "potplayer",
                        MsStoreId = "XP8BSBGQW2DKS0",
                        WebsiteUrl = "https://potplayer.tv/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-kdenlive",
                        Name = "kdenlive",
                        Description = "Free and open-source video editing software",
                        GroupName = "Multimedia (Audio & Video)",
                        WinGetPackageId = ["KDE.Kdenlive"],
                        ChocoPackageId = "kdenlive",
                        WebsiteUrl = "https://kdenlive.org/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-mediainfo-gui",
                        Name = "MediaInfo",
                        Description = "Technical information display tool for multimedia files",
                        GroupName = "Multimedia (Audio & Video)",
                        AppxPackageName = ["MediaArea.net.MediaInfo"],
                        WinGetPackageId = ["MediaArea.MediaInfo.GUI"],
                        ChocoPackageId = "mediainfo",
                        MsStoreId = "9NK81654HHV5",
                        WebsiteUrl = "https://mediaarea.net/en/MediaInfo",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-freac",
                        Name = "fre:ac - free audio converter",
                        Description = "Free audio converter and CD ripper",
                        GroupName = "Multimedia (Audio & Video)",
                        AppxPackageName = ["17479thefreacproject.freac-freeaudioconverter"],
                        MsStoreId = "9P1XD8ZQJ7JD",
                        ChocoPackageId = "freac",
                        WebsiteUrl = "https://www.freac.org/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-smplayer",
                        Name = "SMPlayer",
                        Description = "Media Player with built-in codecs that can play virtually all video and audio formats",
                        GroupName = "Multimedia (Audio & Video)",
                        AppxPackageName = ["55144RicardoVillalba.SMPlayerMediaPlayer"],
                        WinGetPackageId = ["SMPlayer.SMPlayer"],
                        ChocoPackageId = "smplayer",
                        MsStoreId = "9N80Q5M0QXW6",
                        WebsiteUrl = "https://www.smplayer.info/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-shotcut",
                        Name = "Shotcut",
                        Description = "Free, open-source, cross-platform video editor",
                        RegistryDisplayName = "Shotcut",
                        GroupName = "Multimedia (Audio & Video)",
                        WinGetPackageId = ["Meltytech.Shotcut"],
                        ChocoPackageId = "Shotcut",
                        MsStoreId = "9PLNFFL3P6LR",
                        WebsiteUrl = "https://www.shotcut.org/",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-losslesscut",
                        Name = "LosslessCut",
                        Description = "Cross-platform FFmpeg GUI for fast, lossless video/audio trimming",
                        RegistryDisplayName = "LosslessCut",
                        GroupName = "Multimedia (Audio & Video)",
                        AppxPackageName = ["57275mifi.no.LosslessCut"],
                        WinGetPackageId = ["ch.LosslessCut"],
                        ChocoPackageId = "lossless-cut",
                        MsStoreId = "9P30LSR4705L",
                        WebsiteUrl = "https://github.com/mifi/lossless-cut",
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-fxsound",
                        Name = "FxSound",
                        Description = "Audio enhancer for boosting sound quality on Windows",
                        RegistryDisplayName = "FxSound",
                        GroupName = "Multimedia (Audio & Video)",
                        WinGetPackageId = ["FxSound.FxSound"],
                        ChocoPackageId = "fxsound",
                        MsStoreId = "XP8JK4TBQ03LZ4",
                        WebsiteUrl = "https://www.fxsound.com/",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://github.com/fxsound2/fxsound-app/releases/download/latest/fxsound_setup.exe",
                        },
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-volume2",
                        Name = "Volume2",
                        Description = "Advanced Windows volume control",
                        RegistryDisplayName = "Volume2 {version}",
                        GroupName = "Multimedia (Audio & Video)",
                        WinGetPackageId = ["irzyxa.Volume2Portable"],
                        ChocoPackageId = "volume2",
                        WebsiteUrl = "https://github.com/irzyxa/Volume2",
                    }
                }
            };
        }
    }
}
