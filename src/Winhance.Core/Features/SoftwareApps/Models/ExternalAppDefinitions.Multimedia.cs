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
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/3/38/VLC_icon.png",
                        ],
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
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/d/df/ITunes_logo.svg/250px-ITunes_logo.svg.png",
                        ],
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
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/0/08/AIMP3_Logo.png",
                        ],
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
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/c/cf/Foobar2000_logo_black.png",
                        ],
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
                        IconSources = [
                            "https://raw.githubusercontent.com/audacity/audacity/master/images/AudacityLogo.png",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/f/f6/Audacity_Logo.svg/250px-Audacity_Logo.svg.png",
                        ],
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
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/1/19/Spotify_logo_without_text.svg/250px-Spotify_logo_without_text.svg.png",
                        ],
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
                        IconSources = [
                            "https://www.mediamonkey.com/assets/img/logo.png",
                        ],
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
                        IconSources = [
                            "https://raw.githubusercontent.com/HandBrake/HandBrake/master/win/CS/HandBrakeWPF/handbrakepineapple.ico",
                            "https://raw.githubusercontent.com/HandBrake/HandBrake/master/graphics/Logo/HandBrake-v1-128x128.png",
                        ],
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
                        IconSources = [
                            "https://obsproject.com/assets/images/new_icon_small-r.png",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/d/d3/OBS_Studio_Logo.svg/250px-OBS_Studio_Logo.svg.png",
                        ],
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
                        IconSources = [
                            "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAABwAAAAbCAMAAABY1h8eAAAAPFBMVEVHcEyA9dKA9dKA9dKA9tKA9dKA9dKF/tpjvaRIjHuA9dIBCBQAAAEgQDwrVE1Xp5F24sM1Z1wJFh1t0rQAnD66AAAAC3RSTlMAE5Da/29t////686eBCMAAACvSURBVHgBldJVAoNADEXRkbSZNA/f/14b3O1Cvg7OOMuHSJti8K7rQ4d9WvvSSV+7Jp3mXaAx3mJwcaQkaYPR/QZSINuc++uRBciLko6QCyDx3PpMNSPJs74q8QITcqYaqrXttkF4QhYUNvUcFmeWQlzpEpsZrR6Bfo4QBWU2J9hw3o6+RpWyhs3xAyn6Aa1RoFMoeImW5GPz54sj8qLxfwY6LVyvocvVd71uL1b8HzIqFClzDZl3AAAAAElFTkSuQmCC",
                        ],
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
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/0/03/MPC-BE.logo.1.png",
                        ],
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
                        IconSources = [
                            "https://www.codecguide.com/mpc_logo.png",
                        ],
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
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/1/1c/Capcut-icon.svg/250px-Capcut-icon.svg.png",
                        ],
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
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/e/e0/PotPlayer_logo_%282017%29.png",
                        ],
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
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/7/74/Kdenlive-logo-blank.png",
                        ],
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
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/1/19/MediaInfo_Logo.svg/250px-MediaInfo_Logo.svg.png",
                        ],
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
                        IconSources = [
                            "https://raw.githubusercontent.com/enzo1982/freac/master/icons/freac-64x64.png",
                        ],
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
                        IconSources = [
                            "https://raw.githubusercontent.com/smplayer-dev/smplayer/master/icons/smplayer_icon256.png",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/2/2f/SMPlayer_icon.svg/250px-SMPlayer_icon.svg.png",
                        ],
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
                        IconSources = [
                            "https://raw.githubusercontent.com/mltframework/shotcut/master/icons/shotcut-logo-64.png",
                        ],
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
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/1/16/LosslessCut_icon.svg/250px-LosslessCut_icon.svg.png",
                        ],
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
                        IconSources = [
                            "https://raw.githubusercontent.com/fxsound2/fxsound-app/main/fxsound/Project/icon.ico",
                            "https://raw.githubusercontent.com/fxsound2/fxsound-app/main/fxsound/Images/fxsound_large.png",
                        ],
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
                        IconSources = [
                            "https://raw.githubusercontent.com/irzyxa/Volume2/master/Assets/MainIcon-PNGs/256.png",
                        ],
                    }
                }
            };
        }
    }
}
