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
                        WinGetPackageId = ["VideoLAN.VLC"],
                        ChocoPackageId = "vlc",
                        WebsiteUrl = "https://www.videolan.org/vlc/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/3/38/VLC_icon.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-itunes",
                        Name = "iTunes",
                        Description = "Media player and library",
                        GroupName = "Multimedia (Audio & Video)",
                        WinGetPackageId = ["Apple.iTunes"],
                        ChocoPackageId = "itunes",
                        WebsiteUrl = "https://www.apple.com/itunes/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/d/df/ITunes_logo.svg/256px-ITunes_logo.svg.png",
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
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/a/a6/AIMP_logo.svg/256px-AIMP_logo.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-foobar2000",
                        Name = "foobar2000",
                        Description = "Advanced audio player for Windows",
                        RegistryDisplayName = "foobar2000 {version} ({arch})",
                        GroupName = "Multimedia (Audio & Video)",
                        WinGetPackageId = ["PeterPawlowski.foobar2000"],
                        ChocoPackageId = "foobar2000",
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
                        WebsiteUrl = "https://www.audacityteam.org/",
                        IconSources = [
                            "https://raw.githubusercontent.com/audacity/audacity/master/images/AudacityLogo.png",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/f/f6/Audacity_Logo.svg/256px-Audacity_Logo.svg.png",
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
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/1/19/Spotify_logo_without_text.svg/256px-Spotify_logo_without_text.svg.png",
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
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/d/d3/OBS_Studio_Logo.svg/256px-OBS_Studio_Logo.svg.png",
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
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/f/f3/Streamlabs_logo.svg/256px-Streamlabs_logo.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-mpc-be",
                        Name = "MPC-BE",
                        Description = "Media Player Classic - Black Edition",
                        RegistryDisplayName = "MPC-BE {arch} {version}",
                        GroupName = "Multimedia (Audio & Video)",
                        WinGetPackageId = ["MPC-BE.MPC-BE"],
                        ChocoPackageId = "mpc-be",
                        WebsiteUrl = "https://sourceforge.net/projects/mpcbe/",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/0/03/MPC-BE.logo.1.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-k-lite-codec-pack",
                        Name = "K-Lite Codec Pack (Mega)",
                        Description = "Collection of codecs and related tools",
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
                        Description = "Video editor",
                        GroupName = "Multimedia (Audio & Video)",
                        WinGetPackageId = ["ByteDance.CapCut"],
                        MsStoreId = "XP9KN75RRB9NHS",
                        WebsiteUrl = "https://www.capcut.com/",
                        ExternalApp = new ExternalAppMetadata
                        {
                            DownloadUrl = "https://www.capcut.com/activity/download_pc",
                        },
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/1/1c/Capcut-icon.svg/256px-Capcut-icon.svg.png",
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
                            "https://raw.githubusercontent.com/KDE/kdenlive/master/data/pics/kdenlive-logo.png",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c6/Kdenlive-logo.svg/256px-Kdenlive-logo.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-mediainfo-gui",
                        Name = "MediaInfo",
                        Description = "Technical information display tool for multimedia files",
                        GroupName = "Multimedia (Audio & Video)",
                        WinGetPackageId = ["MediaArea.MediaInfo.GUI"],
                        ChocoPackageId = "mediainfo",
                        WebsiteUrl = "https://mediaarea.net/en/MediaInfo",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/1/19/MediaInfo_Logo.svg/256px-MediaInfo_Logo.svg.png",
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
                        WinGetPackageId = ["SMPlayer.SMPlayer"],
                        ChocoPackageId = "smplayer",
                        WebsiteUrl = "https://www.smplayer.info/",
                        IconSources = [
                            "https://raw.githubusercontent.com/smplayer-dev/smplayer/master/icons/smplayer_icon256.png",
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/2/2f/SMPlayer_icon.svg/256px-SMPlayer_icon.svg.png",
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
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/8/81/Shotcut_logo.svg/256px-Shotcut_logo.svg.png",
                        ],
                    },
                    new ItemDefinition
                    {
                        Id = "external-app-losslesscut",
                        Name = "LosslessCut",
                        Description = "Cross-platform FFmpeg GUI for fast, lossless video/audio trimming",
                        RegistryDisplayName = "LosslessCut",
                        GroupName = "Multimedia (Audio & Video)",
                        WinGetPackageId = ["ch.LosslessCut"],
                        ChocoPackageId = "lossless-cut",
                        WebsiteUrl = "https://github.com/mifi/lossless-cut",
                        IconSources = [
                            "https://upload.wikimedia.org/wikipedia/commons/thumb/1/16/LosslessCut_icon.svg/256px-LosslessCut_icon.svg.png",
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
