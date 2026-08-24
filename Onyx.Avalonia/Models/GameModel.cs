// ============================================================================
// ONYX Launcher — Cyberpunk Game Library & Platform Hub
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Onyx.Avalonia.Models
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

    public class GameItem
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

        public bool IsFavorite { get; set; }

        [JsonIgnore]
        public string FavoriteColor => IsFavorite ? "#FBBF24" : "#64748B";

        [JsonPropertyName("custom_logo_path")]
        public string? CustomLogoPath { get; set; }

        [JsonPropertyName("prefer_online_logo")]
        public bool PreferOnlineLogo { get; set; } = true;

        [JsonPropertyName("launch_args")]
        public string? LaunchArgs { get; set; }

        [JsonPropertyName("quick_launch_command")]
        public string? QuickLaunchCommand { get; set; }

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

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public void NotifyIconChanged()
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(GameIcon)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(HasGameIcon)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(FavoriteColor)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsFavorite)));
        }

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

        public string PlatformLogoPath => Platform?.ToLowerInvariant() switch
        {
            "steam" => "avares://Onyx.Avalonia/Assets/steam.svg",
            "epic" => "avares://Onyx.Avalonia/Assets/epic.svg",
            "ea" => "avares://Onyx.Avalonia/Assets/ea.svg",
            "minecraft" => "avares://Onyx.Avalonia/Assets/minecraft.svg",
            "riot" => "avares://Onyx.Avalonia/Assets/riot.svg",
            "battlenet" or "battle_net" => "avares://Onyx.Avalonia/Assets/battlenet.svg",
            "gog" => "avares://Onyx.Avalonia/Assets/gog.svg",
            "ubisoft" => "avares://Onyx.Avalonia/Assets/ubisoft.svg",
            "rockstar" => "avares://Onyx.Avalonia/Assets/rockstar.svg",
            "xbox" => "avares://Onyx.Avalonia/Assets/xbox.svg",
            "amazon" => "avares://Onyx.Avalonia/Assets/amazon.svg",
            "metin2" => "avares://Onyx.Avalonia/Assets/metin2.svg",
            _ => "avares://Onyx.Avalonia/Assets/local.svg"
        };
    }

    public class RunningAppItem
    {
        public string Name { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public global::Avalonia.Media.Imaging.Bitmap? Icon { get; set; }
        public bool HasIcon => Icon != null;
    }
}
