// ============================================================================
// ONYX Launcher — Cyberpunk Game Library & Platform Hub
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
using System;
using System.Collections.Generic;

namespace Onyx.Avalonia.Services
{
    public enum AppLanguage
    {
        Turkish = 0,
        English = 1,
        German = 2,
        Bulgarian = 3,
        Spanish = 4,
        Dutch = 5,
        French = 6,
        Russian = 7
    }

    public static class LocalizationService
    {
        public static AppLanguage CurrentLanguage { get; set; } = AppLanguage.Turkish;

        private static readonly Dictionary<string, (string TR, string EN)> _strings = new()
        {
            // Top Bar
            { "SearchPlaceholder", ("Oyunlarda veya platformlarda ara...", "Search games or platforms...") },
            { "AllGames", ("Tüm Oyunlar", "All Games") },
            { "Favorites", ("Favoriler", "Favorites") },
            { "Scan", ("Tara", "Scan") },
            { "Add", ("Ekle", "Add") },
            { "Settings", ("Ayarlar", "Settings") },
            { "Scanning", ("Taranıyor...", "Scanning...") },

            // Vitrin
            { "LastOpened", ("EN SON AÇILAN", "LAST OPENED") },
            { "RecentGames", ("SON OYNANANLAR", "RECENT GAMES") },
            { "PlayNow", ("HEMEN OYNA", "PLAY NOW") },
            { "SelectGame", ("Oyun Seçin", "Select a Game") },
            { "Installed", ("Yüklü", "Installed") },

            // Game Card & Modal
            { "Play", ("Oyna", "Play") },
            { "GameTitle", ("OYUN BAŞLIĞI", "GAME TITLE") },
            { "CustomLogoMedia", ("Özel Oyun Logosu & Medya", "Custom Game Logo & Media") },
            { "ChangeLogo", ("Logo Değiştir", "Change Logo") },
            { "SystemLogo", ("Sistem Logosu", "System Logo") },
            { "OnlineLogo", ("Açık Logo", "Online Logo") },
            { "LaunchArgs", ("BAŞLATMA PARAMETRELERİ (ARGS)", "LAUNCH ARGUMENTS (ARGS)") },
            { "QuickLaunch", ("HIZLI BAŞLATMA / KISAYOL KOMUTU", "QUICK LAUNCH / SHORTCUT COMMAND") },
            { "Folder", ("Klasör", "Folder") },
            { "DesktopShortcut", ("Masaüstü Kısayolu", "Desktop Shortcut") },
            { "Remove", ("Kaldır", "Remove") },
            { "Save", ("Kaydet", "Save") },
            { "ReadyToPlay", ("Kütüphanenizde oynamaya hazır", "Ready to play in your library") },

            // Settings
            { "AppSettings", ("Uygulama Ayarları", "Application Settings") },
            { "AppSettingsSubtitle", ("ONYX Launcher sistem tercihlerini, dil ve arayüz ölçeklendirmesini yapılandırın.", "Configure ONYX Launcher system preferences, language, and UI scaling.") },
            { "LanguageRegion", ("Dil ve Bölge / Language & Region", "Language & Region") },
            { "SelectLanguage", ("Görüntüleme Dili", "Display Language") },
            { "UiScaling", ("Arayüz ve Görünüm Ölçeklendirmesi", "UI & Display Scaling") },
            { "UiScalingDesc", ("Arayüzdeki tüm metin, kart ve butonların boyutunu ekranınıza göre ayarlayın", "Adjust the scale of all text, cards, and buttons for your display") },
            { "SystemConfig", ("Sistem Yapılandırması", "System Configuration") },
            { "Autostart", ("Windows ile Başlat", "Start with Windows") },
            { "MinimizeTray", ("Kapatıldığında Sistem Tepsisine Küçült", "Minimize to System Tray on Close") },
            { "AutoScan", ("Otomatik Kısayol Taraması", "Automatic Shortcut Scanning") },
            { "Metin2Detect", ("Metin2 Sunucu Tespiti", "Metin2 Server Detection") },
            { "OnlineArtworkConfig", ("Çevrimiçi Oyun Medya ve Logo Yapılandırması", "Online Game Media & Logo Configuration") },
            { "DownloadOnlineLogos", ("İnternetten Orijinal Logoları İndir (HD/4K)", "Download Original HD/4K Logos from Internet") },
            { "RefreshAllLogos", ("Tüm Logoları Çevrimiçi Yenile", "Refresh All Logos Online") },
            { "RefreshLogosBtn", ("Logoları Yenile", "Refresh Logos") },
            { "AiEngine", ("Yapay Zeka Oyun Bulucu Motoru", "AI Game Finder Engine") },
            { "AiDetection", ("Yapay Zeka Oyun Tespiti", "AI Game Detection") },
            { "Back", ("Geri Dön", "Back") },
            { "SaveSettings", ("Ayarları Kaydet", "Save Settings") }
        };

        public static string Get(string key)
        {
            if (_strings.TryGetValue(key, out var pair))
            {
                return CurrentLanguage == AppLanguage.Turkish ? pair.TR : pair.EN;
            }
            return key;
        }
    }
}
