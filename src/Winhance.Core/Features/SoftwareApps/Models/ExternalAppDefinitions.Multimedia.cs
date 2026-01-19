using System.Collections.Generic;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.SoftwareApps.Models
{
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
                            Name = "VLC",
                            Description = "Open-source multimedia player and framework",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "VideoLAN.VLC",
                            WebsiteUrl = "https://www.videolan.org/vlc/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-itunes",
                            Name = "iTunes",
                            Description = "Media player and library",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "Apple.iTunes",
                            WebsiteUrl = "https://www.apple.com/itunes/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-aimp",
                            Name = "AIMP",
                            Description = "Audio player with support for various formats",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "AIMP.AIMP",
                            WebsiteUrl = "https://www.aimp.ru/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-foobar2000",
                            Name = "foobar2000",
                            Description = "Advanced audio player for Windows",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "PeterPawlowski.foobar2000",
                            WebsiteUrl = "https://www.foobar2000.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-musicbee",
                            Name = "MusicBee",
                            Description = "Music manager and player",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "9P4CLT2RJ1RS",
                            WebsiteUrl = "https://www.getmusicbee.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-audacity",
                            Name = "Audacity",
                            Description = "Audio editor and recorder",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "Audacity.Audacity",
                            WebsiteUrl = "https://www.audacityteam.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-gom-player",
                            Name = "GOM",
                            Description = "Media player for Windows",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "GOMLab.GOMPlayer",
                            WebsiteUrl = "https://www.gomlab.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-spotify",
                            Name = "Spotify",
                            Description = "Music streaming service",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "Spotify.Spotify",
                            WebsiteUrl = "https://www.spotify.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-mediamonkey",
                            Name = "MediaMonkey",
                            Description = "Media manager and player",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "VentisMedia.MediaMonkey.5",
                            WebsiteUrl = "https://www.mediamonkey.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-handbrake",
                            Name = "HandBrake",
                            Description = "Open-source video transcoder",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "HandBrake.HandBrake",
                            WebsiteUrl = "https://handbrake.fr/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-obs-studio",
                            Name = "OBS Studio",
                            Description = "Free and open source software for video recording and live streaming",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "OBSProject.OBSStudio",
                            WebsiteUrl = "https://obsproject.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-streamlabs-obs",
                            Name = "Streamlabs OBS",
                            Description = "Streaming software built on OBS with additional features for streamers",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "Streamlabs.StreamlabsOBS",
                            WebsiteUrl = "https://streamlabs.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-mpc-be",
                            Name = "MPC-BE",
                            Description = "Media Player Classic - Black Edition",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "MPC-BE.MPC-BE",
                            WebsiteUrl = "https://sourceforge.net/projects/mpcbe/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-k-lite-codec-pack",
                            Name = "K-Lite Codec Pack (Mega)",
                            Description = "Collection of codecs and related tools",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "CodecGuide.K-LiteCodecPack.Mega",
                            WebsiteUrl = "https://codecguide.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-capcut",
                            Name = "CapCut",
                            Description = "Video editor",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "ByteDance.CapCut",
                            WebsiteUrl = "https://www.capcut.com/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-potplayer",
                            Name = "PotPlayer",
                            Description = "Comprehensive multimedia player for Windows",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "Daum.PotPlayer",
                            WebsiteUrl = "https://potplayer.daum.net/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-kdenlive",
                            Name = "Kdenlive",
                            Description = "Free and open-source video editing software",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "KDE.Kdenlive",
                            WebsiteUrl = "https://kdenlive.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-mediainfo-gui",
                            Name = "MediaInfo GUI",
                            Description = "Technical information display tool for multimedia files",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "MediaArea.MediaInfo.GUI",
                            WebsiteUrl = "https://mediaarea.net/en/MediaInfo"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-freac",
                            Name = "fre:ac",
                            Description = "Free audio converter and CD ripper",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "9P1XD8ZQJ7JD",
                            WebsiteUrl = "https://www.freac.org/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-smplayer",
                            Name = "SMPlayer",
                            Description = "Media Player with built-in codecs that can play virtually all video and audio formats",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "SMPlayer.SMPlayer",
                            WebsiteUrl = "https://www.smplayer.info/"
                        },
                        new ItemDefinition
                        {
                            Id = "external-app-volume2",
                            Name = "Volume2",
                            Description = "Advanced Windows volume control",
                            GroupName = "Multimedia (Audio & Video)",
                            WinGetPackageId = "irzyxa.Volume2Portable",
                            WebsiteUrl = "https://github.com/irzyxa/Volume2"
                        }
                    }
                };
            }
        }
    }
}