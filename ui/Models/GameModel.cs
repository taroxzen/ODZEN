// ============================================================================
// ODZEN — Cybernetic Gaming Platform
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Odzen.Avalonia.Models
{
    public class GameLaunchInfo
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "executable";

        [JsonPropertyName("path")]
        public string? Path { get; set; }

        [JsonPropertyName("uri")]
        public string? Uri { get; set; }

        [JsonPropertyName("args")]
        public List<string>? Args { get; set; }

        [JsonPropertyName("cwd")]
        public string? Cwd { get; set; }
    }

    public partial class GameItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("platform")]
        public string Platform { get; set; } = string.Empty;

        [JsonPropertyName("platform_name")]
        public string PlatformName { get; set; } = string.Empty;

        [JsonPropertyName("install_path")]
        public string? InstallPath { get; set; }

        [JsonPropertyName("executable")]
        public string? Executable { get; set; }

        [JsonPropertyName("launch")]
        public GameLaunchInfo? Launch { get; set; }

        [JsonPropertyName("store_id")]
        public string? StoreId { get; set; }

        [JsonPropertyName("size_bytes")]
        public ulong? SizeBytes { get; set; }

        [JsonPropertyName("last_played_ms")]
        public ulong? LastPlayedMs { get; set; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new();

        [JsonPropertyName("custom")]
        public bool IsCustom { get; set; }

        private bool _isFavorite;
        [JsonPropertyName("is_favorite")]
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (SetProperty(ref _isFavorite, value))
                {
                    OnPropertyChanged(nameof(FavoriteColor));
                }
            }
        }

        [JsonIgnore]
        public string FavoriteColor => IsFavorite ? "#FBBF24" : "#64748B";

        private string? _customLogoPath;
        [JsonPropertyName("custom_logo_path")]
        public string? CustomLogoPath
        {
            get => _customLogoPath;
            set
            {
                if (SetProperty(ref _customLogoPath, value))
                {
                    OnPropertyChanged(nameof(GameIcon));
                    OnPropertyChanged(nameof(HasGameIcon));
                }
            }
        }

        private bool _preferOnlineLogo = true;
        [JsonPropertyName("prefer_online_logo")]
        public bool PreferOnlineLogo
        {
            get => _preferOnlineLogo;
            set
            {
                if (SetProperty(ref _preferOnlineLogo, value))
                {
                    OnPropertyChanged(nameof(GameIcon));
                    OnPropertyChanged(nameof(HasGameIcon));
                }
            }
        }

        [JsonPropertyName("launch_args")]
        public string? LaunchArgs { get; set; }

        [JsonPropertyName("quick_launch_command")]
        public string? QuickLaunchCommand { get; set; }

        private string? _publisher;
        [JsonPropertyName("publisher")]
        public string? Publisher
        {
            get => _publisher;
            set => SetProperty(ref _publisher, value);
        }

        [JsonIgnore]
        public global::Avalonia.Media.Imaging.Bitmap? GameIcon
        {
            get
            {
                if (!string.IsNullOrEmpty(CustomLogoPath) && System.IO.File.Exists(CustomLogoPath))
                {
                    try
                    {
                        using var stream = System.IO.File.OpenRead(CustomLogoPath);
                        return new global::Avalonia.Media.Imaging.Bitmap(stream);
                    }
                    catch { }
                }

                if (!PreferOnlineLogo)
                {
                    var localBmp = Services.ArtworkPipelineService.ResolveLocalSystemArtwork(Executable, InstallPath, StoreId, Platform, Id, Name);
                    if (localBmp != null) return localBmp;
                }

                return Services.ArtworkPipelineService.ResolveArtwork(Executable, InstallPath, StoreId, Platform, Id, Name);
            }
        }

        [JsonIgnore]
        public bool HasGameIcon => GameIcon != null;

        public void NotifyIconChanged()
        {
            OnPropertyChanged(nameof(GameIcon));
            OnPropertyChanged(nameof(HasGameIcon));
            OnPropertyChanged(nameof(FavoriteColor));
            OnPropertyChanged(nameof(IsFavorite));
        }

        [JsonIgnore]
        public string FormattedSize
        {
            get
            {
                if (!SizeBytes.HasValue || SizeBytes.Value == 0) return "";
                double gb = SizeBytes.Value / (1024.0 * 1024.0 * 1024.0);
                if (gb >= 1.0) return $"{gb:F1} GB";
                double mb = SizeBytes.Value / (1024.0 * 1024.0);
                return $"{mb:F0} MB";
            }
        }

        [JsonIgnore]
        public string PlatformLogoPath => Platform?.ToLowerInvariant() switch
        {
            "steam" => "avares://ODZEN/Assets/steam.svg",
            "epic" => "avares://ODZEN/Assets/epic.svg",
            "ea" => "avares://ODZEN/Assets/ea.svg",
            "minecraft" => "avares://ODZEN/Assets/minecraft.svg",
            "riot" => "avares://ODZEN/Assets/riot.svg",
            "battlenet" or "battle_net" => "avares://ODZEN/Assets/battlenet.svg",
            "gog" => "avares://ODZEN/Assets/gog.svg",
            "ubisoft" => "avares://ODZEN/Assets/ubisoft.svg",
            "rockstar" => "avares://ODZEN/Assets/rockstar.svg",
            "xbox" => "avares://ODZEN/Assets/xbox.svg",
            "amazon" => "avares://ODZEN/Assets/amazon.svg",
            "metin2" => "avares://ODZEN/Assets/metin2.svg",
            _ => "avares://ODZEN/Assets/local.svg"
        };
    }

    public class RunningAppItem
    {
        public string Name { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        [JsonIgnore]
        public global::Avalonia.Media.Imaging.Bitmap? Icon { get; set; }
        [JsonIgnore]
        public bool HasIcon => Icon != null;
    }

    public class OfflineLibraryData
    {
        [JsonPropertyName("all_games")]
        public List<GameItem> AllGames { get; set; } = new();

        [JsonPropertyName("favorites")]
        public List<string> Favorites { get; set; } = new();

        [JsonPropertyName("custom_games")]
        public List<GameItem> CustomGames { get; set; } = new();

        [JsonPropertyName("recent_games")]
        public List<string> RecentGames { get; set; } = new();

        [JsonPropertyName("updated_at")]
        public string UpdatedAt { get; set; } = "";
    }
}
