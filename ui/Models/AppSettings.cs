// ============================================================================
// ODZEN — Cybernetic Gaming Platform
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
using System.Collections.Generic;

namespace Odzen.Avalonia.Models
{
    public class AppSettings
    {
        public string SelectedLanguage { get; set; } = "tr";
        public double UiScale { get; set; } = 1.0;
        public bool MinimizeToTrayOnClose { get; set; } = true;
        public bool AutoScan { get; set; } = false;
        public bool Metin2 { get; set; } = true;
        public bool SmartDetection { get; set; } = true;
        public bool DownloadOnlineLogos { get; set; } = true;
        public bool UseSteamSource { get; set; } = true;
        public bool UseWikimediaSource { get; set; } = true;
        public bool UseSteamGridDbSource { get; set; } = true;
        public string SteamGridDbApiKey { get; set; } = "";
        public bool ShowShowcase { get; set; } = true;
        public bool ShowMusicButton { get; set; } = true;
        public bool ShowDiscordButton { get; set; } = true;
        public int CpuThreshold { get; set; } = 10;
        public int GpuThreshold { get; set; } = 15;
        public bool AutostartWithWindows { get; set; } = false;
        public List<string> CustomScanFolders { get; set; } = new();
    }
}
