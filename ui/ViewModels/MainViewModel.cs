// ============================================================================
// ODZEN — Cybernetic Gaming Platform
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Odzen.Avalonia.Models;
using Odzen.Avalonia.Services;

namespace Odzen.Avalonia.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly GameScannerService _scannerService;
        private readonly DispatcherTimer _aiDetectorTimer;
        private readonly DispatcherTimer _autoScanTimer;
        private readonly DispatcherTimer _toastTimer;
        private readonly HashSet<string> _knownProcessNames = new(StringComparer.OrdinalIgnoreCase);
        private static readonly CultureInfo TrCulture = new("tr-TR");

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string _selectedPlatform = "all";

        [ObservableProperty]
        private string _currentSectionTitle = "Tüm Oyunlar";

        [ObservableProperty]
        private string _currentSectionSubtitle = "Sistemde ve kütüphanelerde tespit edilen oyunlar listeleniyor.";

        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private bool _isSettingsOpen;

        [ObservableProperty]
        private GameItem? _selectedGame;

        [ObservableProperty]
        private bool _isGameDetailOpen;

        [ObservableProperty]
        private double _uiScale = 1.0;

        [ObservableProperty]
        private string _uiScaleText = "%100";

        [ObservableProperty]
        private double _pendingUiScale = 1.0;

        [ObservableProperty]
        private string _pendingUiScaleText = "%100";

        [ObservableProperty]
        private bool _showShowcase = true;

        [ObservableProperty]
        private bool _showMusicButton = true;

        [ObservableProperty]
        private bool _showDiscordButton = true;

        [ObservableProperty]
        private int _selectedLanguageIndex = 0;

        // DYNAMIC LOCALIZATION STRINGS
        [ObservableProperty] private string _txtSearchWatermark = "Oyunlarda veya platformlarda ara...";
        [ObservableProperty] private string _txtAllGames = "Tüm Oyunlar";
        [ObservableProperty] private string _txtFavorites = "Favoriler";
        [ObservableProperty] private string _txtLocalGames = "Yerel Oyunlar";
        [ObservableProperty] private string _txtScan = "Tara";
        [ObservableProperty] private string _txtAdd = "Ekle";
        [ObservableProperty] private string _txtSettings = "Ayarlar";
        [ObservableProperty] private string _txtScanning = "Taranıyor...";
        [ObservableProperty] private string _txtLastOpened = "EN SON AÇILAN";
        [ObservableProperty] private string _txtPlayNow = "HEMEN OYNA";
        [ObservableProperty] private string _txtRecentGames = "SON OYNANANLAR";
        [ObservableProperty] private string _txtSelectGame = "Oyun Seçin";
        [ObservableProperty] private string _txtInstalled = "Yüklü";
        [ObservableProperty] private string _txtPlay = "Oyna";
        [ObservableProperty] private string _txtGameTitle = "OYUN BAŞLIĞI";
        [ObservableProperty] private string _txtReadyToPlay = "Kütüphanenizde oynamaya hazır";
        [ObservableProperty] private string _txtCustomLogoMedia = "Özel Oyun Logosu & Medya";
        [ObservableProperty] private string _txtChangeLogo = "Logo Değiştir";
        [ObservableProperty] private string _txtSystemLogo = "Sistem Logosu";
        [ObservableProperty] private string _txtOnlineLogo = "Online Logo";
        [ObservableProperty] private string _txtLaunchArgs = "BAŞLATMA PARAMETRELERİ (ARGS)";
        [ObservableProperty] private string _txtQuickLaunch = "HIZLI BAŞLATMA / KISAYOL KOMUTU";
        [ObservableProperty] private string _txtFolder = "Klasör";
        [ObservableProperty] private string _txtDesktopShortcut = "Masaüstü Kısayolu";
        [ObservableProperty] private string _txtRemove = "Kaldır";
        [ObservableProperty] private string _txtSave = "Kaydet";
        [ObservableProperty] private string _txtAppSettings = "Uygulama Ayarları";
        [ObservableProperty] private string _txtAppSettingsSubtitle = "ODZEN sistem tercihlerini ve kütüphane davranışlarını yapılandırın.";
        [ObservableProperty] private string _txtLanguageChange = "Dil Değiştirme";
        [ObservableProperty] private string _txtLanguageDesc = "Uygulama arayüz dilini seçin";
        [ObservableProperty] private string _txtUiScaleTitle = "Arayüz ve Görünüm Ölçeklendirmesi";
        [ObservableProperty] private string _txtUiScaleDesc = "Arayüzdeki tüm kart, metin ve menülerin boyutunu ekranınıza göre ayarlayın";
        [ObservableProperty] private string _txtApplyScale = "⚡ Arayüz Ölçeğini Uygula";
        [ObservableProperty] private string _txtShowcaseTitle = "Vitrin ve Görünüm Tercihleri";
        [ObservableProperty] private string _txtShowShowcaseOpt = "Vitrin Bölümünü Göster";
        [ObservableProperty] private string _txtShowShowcaseDesc = "Ana sayfadaki en son açılan oyun vitrini panelini gösterir/gizler";
        [ObservableProperty] private string _txtShowMusicOpt = "Müzik Butonlarını Göster";
        [ObservableProperty] private string _txtShowMusicDesc = "Üst çubuktaki Spotify, YouTube Music, Apple Music vb. müzik servis butonlarını gösterir/gizler";
        [ObservableProperty] private string _txtShowDiscordOpt = "Discord Butonunu Göster";
        [ObservableProperty] private string _txtShowDiscordDesc = "Üst çubuktaki Discord hızlı başlatma butonunu gösterir/gizler";
        [ObservableProperty] private string _txtSystemConfig = "Sistem Yapılandırması";
        [ObservableProperty] private string _txtAutostart = "Windows ile Başlat";
        [ObservableProperty] private string _txtAutostartDesc = "Windows açılışında arka planda sessiz çalışır (Kayıt Defteri Aktif)";
        [ObservableProperty] private string _txtMinimizeTray = "Kapatıldığında Sistem Tepsisine Küçült";
        [ObservableProperty] private string _txtMinimizeTrayDesc = "Sağ üstteki ✕ butonuna basıldığında uygulamayı kapatmak yerine sistem tepsisine gizler";
        [ObservableProperty] private string _txtAutoScan = "Otomatik Kısayol Taraması";
        [ObservableProperty] private string _txtAutoScanDesc = "Masaüstü ve Başlat menüsünü sürekli izler";
        [ObservableProperty] private string _txtMetin2Detect = "Metin2 Sunucu Tespiti";
        [ObservableProperty] private string _txtMetin2DetectDesc = "Metin2 sunucuları algılandığında platformlarda göster";
        [ObservableProperty] private string _txtOnlineMediaConfig = "Çevrimiçi Oyun Medya ve Logo Yapılandırması";
        [ObservableProperty] private string _txtDownloadOnlineLogos = "İnternetten Orijinal Logoları İndir (HD/4K)";
        [ObservableProperty] private string _txtDownloadOnlineLogosDesc = "Steam Store ve açık medya ağlarından şeffaf 4K logoları bir kez indirip bilgisayara kaydeder";
        [ObservableProperty] private string _txtRefreshAllLogos = "Tüm Logoları Çevrimiçi Yenile";
        [ObservableProperty] private string _txtRefreshAllLogosDesc = "Kütüphanedeki tüm oyunlar için açık sunuculardan en güncel logoları sıfırdan indirir";
        [ObservableProperty] private string _txtRefreshLogosBtn = "Tümünü Yenile";
        [ObservableProperty] private string _txtDownloadMissingLogos = "Yalnızca Eksikleri İndir";
        [ObservableProperty] private string _txtOpenLogoFolder = "Klasörü Aç";
        [ObservableProperty] private string _txtClearLogoCache = "Önbelleği Temizle";
        [ObservableProperty] private string _txtSourcesTitle = "Arama Kaynakları:";
        [ObservableProperty] private string _txtSteamGridDbKeyPlaceholder = "SteamGridDB API Anahtarı (İsteğe bağlı)...";
        [ObservableProperty] private string _logoCacheSizeText = "0 B";
        [ObservableProperty] private bool _useSteamSource = true;
        [ObservableProperty] private bool _useWikimediaSource = true;
        [ObservableProperty] private bool _useSteamGridDbSource = true;
        [ObservableProperty] private string _steamGridDbApiKey = "";
        [ObservableProperty] private bool _isLogoDownloading = false;
        [ObservableProperty] private double _logoDownloadProgress = 0;
        [ObservableProperty] private string _logoDownloadStatusText = "";
        [ObservableProperty] private bool _isLogoPickerOpen = false;
        [ObservableProperty] private string _logoPickerSearchQuery = "";
        [ObservableProperty] private string _logoPickerCustomUrl = "";
        [ObservableProperty] private bool _isLogoPickerLoading = false;
        [ObservableProperty] private string _txtSearchAndPickLogo = "Çevrimiçi Ara & Seç";
        [ObservableProperty] private string _txtLogoPickerTitle = "Çevrimiçi Logo Arama & Seçim Paneli";
        [ObservableProperty] private string _txtPasteLogoUrl = "Görsel Bağlantısı (URL) Yapıştır";
        [ObservableProperty] private string _txtApplyUrl = "Uygula";
        [ObservableProperty] private string _txtCandidatesFound = "Bulunan Logo Adayları";
        [ObservableProperty] private string _txtNoCandidates = "Uygun logo bulunamadı. Lütfen arama terimini değiştirin veya doğrudan bir görsel URL'si yapıştırın.";
        public ObservableCollection<LogoCandidate> LogoCandidates { get; } = new();
        [ObservableProperty] private string _txtAiEngine = "Otomatik Oyun Algılama";
        [ObservableProperty] private string _txtAiDetection = "Çalışan Oyunları Algıla";
        [ObservableProperty] private string _txtAiDetectionDesc = "Arka planda yeni bir oyun açıldığında bildirim gösterir ve kütüphanenize tek tıkla eklemenizi sağlar.";
        [ObservableProperty] private string _txtGpuThreshold = "GPU Kullanım Eşiği (%5 Adımlı)";
        [ObservableProperty] private string _txtCpuThreshold = "İşlemci (CPU) Kullanım Eşiği (%5 Adımlı)";
        [ObservableProperty] private string _txtBack = "Geri Dön";
        [ObservableProperty] private string _txtSaveSettings = "Kapat / Geri Dön";
        [ObservableProperty] private string _txtResetSettings = "Ayarları Sıfırla";
        [ObservableProperty] private string _txtAddGameTitle = "Oyun & Uygulama Ekle";
        [ObservableProperty] private string _txtRunningAppsTitle = "Açık Uygulamalar & Pencereler";
        [ObservableProperty] private string _txtManualAddTitle = "Manuel Oyun Ekle";
        [ObservableProperty] private string _txtPlatformLaunchers = "Platformlar";
        [ObservableProperty] private string _txtPlatformLaunchersSubtitle = "Bilgisayarınızda yüklü resmi oyun istemcilerini ve mağazalarını doğrudan başlatın.";
        [ObservableProperty] private string _txtBackToLibrary = "Kütüphaneye Dön";
        [ObservableProperty] private string _txtRefresh = "Yenile";
        [ObservableProperty] private string _txtBrowseExe = "Bilgisayardan .exe / Kısayol Seç";
        [ObservableProperty] private string _txtFilePath = "DOSYA YOLU";
        [ObservableProperty] private string _txtLaunchArgsOptional = "BAŞLATMA PARAMETRELERİ (OPSİYONEL)";
        [ObservableProperty] private string _txtAddToLibraryBtn = "Kütüphaneye Ekle";
        [ObservableProperty] private string _txtAiPromptTitle = "Yeni Oyun Algılandı";
        [ObservableProperty] private string _txtYesAdd = "Kütüphaneye Ekle";
        [ObservableProperty] private string _txtNoDismiss = "Yoksay";
        [ObservableProperty] private string _txtLaunch = "Başlat";
        [ObservableProperty] private string _txtDeveloperCredits = "Geliştirici & Telif";
        [ObservableProperty] private string _txtVisitGitHub = "GitHub Profilini Aç";

        [ObservableProperty] private bool _isAddGameModalOpen;
        [ObservableProperty] private bool _isPlatformsViewOpen;
        [ObservableProperty] private string _manualGameName = "";
        [ObservableProperty] private string _manualGamePath = "";
        [ObservableProperty] private string _manualGameArgs = "";
        public ObservableCollection<RunningAppItem> RunningApps { get; } = new();

        // Smart Game Detection Prompt (Evet / Hayır Bildirimi)
        [ObservableProperty] private bool _isAiDetectionPromptOpen;
        [ObservableProperty] private string _detectedGameName = "";
        [ObservableProperty] private string _detectedGameExe = "";
        [ObservableProperty] private string _detectedGameProcessName = "";

        [ObservableProperty]
        private int _allGamesCount;

        [ObservableProperty]
        private int _favGamesCount;

        [ObservableProperty]
        private string _toastMessage = string.Empty;

        [ObservableProperty]
        private bool _isToastVisible = false;

        // SETTINGS
        private double _gpuThreshold = 80;
        public double GpuThreshold
        {
            get => _gpuThreshold;
            set
            {
                double snapped = Math.Round(value / 5.0) * 5.0;
                if (snapped < 50) snapped = 50;
                if (snapped > 95) snapped = 95;
                if (SetProperty(ref _gpuThreshold, snapped))
                {
                    OnPropertyChanged(nameof(GpuThresholdText));
                    SaveCurrentSettings();
                }
            }
        }
        public string GpuThresholdText => $"{GpuThreshold:F0}%";

        private double _cpuThreshold = 30;
        public double CpuThreshold
        {
            get => _cpuThreshold;
            set
            {
                double snapped = Math.Round(value / 5.0) * 5.0;
                if (snapped < 15) snapped = 15;
                if (snapped > 80) snapped = 80;
                if (SetProperty(ref _cpuThreshold, snapped))
                {
                    OnPropertyChanged(nameof(CpuThresholdText));
                    SaveCurrentSettings();
                }
            }
        }
        public string CpuThresholdText => $"{CpuThreshold:F0}%";

        private bool _autostart = false;
        public bool Autostart
        {
            get => _autostart;
            set
            {
                if (SetProperty(ref _autostart, value))
                {
                    ApplyAutostartRegistry(value);
                    SaveCurrentSettings();
                }
            }
        }

        private bool _fetchLogosFromInternet = true;
        public bool FetchLogosFromInternet
        {
            get => _fetchLogosFromInternet;
            set
            {
                if (SetProperty(ref _fetchLogosFromInternet, value))
                {
                    ArtworkPipelineService.IsOnlineDownloadEnabled = value;
                    ArtworkPipelineService.ClearCache();
                    ApplyFilter();
                    SaveCurrentSettings();
                    ShowToastNotification(value 
                        ? "🌐 İnternetten Orijinal Logolar İndiriliyor..." 
                        : "💾 İnternet indirmesi kapatıldı, yerel logolar kullanılıyor.");
                }
            }
        }

        [ObservableProperty]
        private bool _minimizeToTrayOnClose = true;

        [ObservableProperty]
        private bool _autoScan = false;

        [ObservableProperty]
        private bool _metin2 = true;

        [ObservableProperty]
        private bool _aiDetection = true;

        public ObservableCollection<GameItem> AllGames { get; } = new();
        public ObservableCollection<GameItem> CustomGames { get; } = new();
        public ObservableCollection<GameItem> FilteredGames { get; } = new();
        public ObservableCollection<GameItem> RecentGames { get; } = new();

        public MainViewModel()
        {
            _scannerService = new GameScannerService();

            _aiDetectorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3500) };
            _aiDetectorTimer.Tick += (s, e) => RunAiGameDetector();
            _aiDetectorTimer.Start();

            _autoScanTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _autoScanTimer.Tick += (s, e) =>
            {
                if (AutoScan) RunAutoShortcutScan();
            };
            _autoScanTimer.Start();

            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _toastTimer.Tick += (s, e) =>
            {
                IsToastVisible = false;
                _toastTimer.Stop();
            };

            LoadSavedSettings();
            CheckAutostartStatus();
            _ = InitializeGamesAsync();
        }

        private void LoadSavedSettings()
        {
            var s = SettingsService.LoadSettings();
            _selectedLanguageIndex = Math.Clamp(GetLanguageIndex(s.SelectedLanguage), 0, 7);
            _uiScale = s.UiScale;
            _pendingUiScale = s.UiScale;
            _pendingUiScaleText = $"%{(int)Math.Round(s.UiScale * 100)}";
            _uiScaleText = $"%{(int)Math.Round(s.UiScale * 100)}";
            _showShowcase = s.ShowShowcase;
            _showMusicButton = s.ShowMusicButton;
            _showDiscordButton = s.ShowDiscordButton;
            _minimizeToTrayOnClose = s.MinimizeToTrayOnClose;
            _autoScan = s.AutoScan;
            _metin2 = s.Metin2;
            _aiDetection = s.SmartDetection;
            _fetchLogosFromInternet = s.DownloadOnlineLogos;
            _cpuThreshold = s.CpuThreshold;
            _gpuThreshold = s.GpuThreshold;
            _autostart = s.AutostartWithWindows;
            _useSteamSource = s.UseSteamSource;
            _useWikimediaSource = s.UseWikimediaSource;
            _useSteamGridDbSource = s.UseSteamGridDbSource;
            _steamGridDbApiKey = s.SteamGridDbApiKey ?? "";
            OpenArtworkPipelineEngine.EnableSteamSource = s.UseSteamSource;
            OpenArtworkPipelineEngine.EnableWikimediaSource = s.UseWikimediaSource;
            OpenArtworkPipelineEngine.EnableSteamGridDbSource = s.UseSteamGridDbSource;
            SteamGridDBLogoEngine.CustomApiKey = s.SteamGridDbApiKey;
            UpdateLogoCacheSize();
            ArtworkPipelineService.IsOnlineDownloadEnabled = s.DownloadOnlineLogos;
        }

        public string ClearCacheButtonText => $"{TxtClearLogoCache} ({LogoCacheSizeText})";
        public string LogoDownloadPercentText => $"%{(int)Math.Round(LogoDownloadProgress)}";

        partial void OnLogoCacheSizeTextChanged(string value) => OnPropertyChanged(nameof(ClearCacheButtonText));
        partial void OnTxtClearLogoCacheChanged(string value) => OnPropertyChanged(nameof(ClearCacheButtonText));
        partial void OnLogoDownloadProgressChanged(double value) => OnPropertyChanged(nameof(LogoDownloadPercentText));

        partial void OnSteamGridDbApiKeyChanged(string value)
        {
            SteamGridDBLogoEngine.CustomApiKey = value;
            SaveCurrentSettings();
        }

        partial void OnUseSteamSourceChanged(bool value)
        {
            OpenArtworkPipelineEngine.EnableSteamSource = value;
            SaveCurrentSettings();
        }

        partial void OnUseWikimediaSourceChanged(bool value)
        {
            OpenArtworkPipelineEngine.EnableWikimediaSource = value;
            SaveCurrentSettings();
        }

        partial void OnUseSteamGridDbSourceChanged(bool value)
        {
            OpenArtworkPipelineEngine.EnableSteamGridDbSource = value;
            SaveCurrentSettings();
        }

        public void SaveCurrentSettings()
        {
            var s = new AppSettings
            {
                SelectedLanguage = GetLanguageCode(SelectedLanguageIndex),
                UiScale = UiScale,
                ShowShowcase = ShowShowcase,
                ShowMusicButton = ShowMusicButton,
                ShowDiscordButton = ShowDiscordButton,
                MinimizeToTrayOnClose = MinimizeToTrayOnClose,
                AutoScan = AutoScan,
                Metin2 = Metin2,
                SmartDetection = AiDetection,
                DownloadOnlineLogos = FetchLogosFromInternet,
                UseSteamSource = UseSteamSource,
                UseWikimediaSource = UseWikimediaSource,
                UseSteamGridDbSource = UseSteamGridDbSource,
                SteamGridDbApiKey = SteamGridDbApiKey,
                CpuThreshold = (int)CpuThreshold,
                GpuThreshold = (int)GpuThreshold,
                AutostartWithWindows = Autostart
            };
            SettingsService.SaveSettings(s);
        }

        private static int GetLanguageIndex(string? code) => code?.ToLowerInvariant() switch
        {
            "en" => 1,
            "de" => 2,
            "bg" => 3,
            "es" => 4,
            "nl" => 5,
            "fr" => 6,
            "ru" => 7,
            _ => 0
        };

        private static string GetLanguageCode(int index) => index switch
        {
            1 => "en",
            2 => "de",
            3 => "bg",
            4 => "es",
            5 => "nl",
            6 => "fr",
            7 => "ru",
            _ => "tr"
        };

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnIsSettingsOpenChanged(bool value) => OnPropertyChanged(nameof(ShowMainGamesView));
        partial void OnIsPlatformsViewOpenChanged(bool value) => OnPropertyChanged(nameof(ShowMainGamesView));
        public bool ShowMainGamesView => !IsSettingsOpen && !IsPlatformsViewOpen;

        partial void OnShowShowcaseChanged(bool value) => SaveCurrentSettings();
        partial void OnShowMusicButtonChanged(bool value) => SaveCurrentSettings();
        partial void OnShowDiscordButtonChanged(bool value) => SaveCurrentSettings();

        partial void OnMinimizeToTrayOnCloseChanged(bool value) => SaveCurrentSettings();
        partial void OnAutoScanChanged(bool value)
        {
            SaveCurrentSettings();
            if (value) RunAutoShortcutScan();
        }
        partial void OnMetin2Changed(bool value)
        {
            SaveCurrentSettings();
            ApplyFilter();
        }
        partial void OnAiDetectionChanged(bool value) => SaveCurrentSettings();

        partial void OnSelectedPlatformChanged(string value)
        {
            IsPlatformsViewOpen = false;
            IsSettingsOpen = false;
            UpdateCategoryInfo();
            ApplyFilter();
        }

        private void RunAutoShortcutScan()
        {
            if (!AutoScan) return;
            try
            {
                var newShortcuts = GameScannerService.ScanShortcuts();
                bool addedAny = false;
                foreach (var sc in newShortcuts)
                {
                    if (AllGames.Any(g => g.Name.Equals(sc.Name, StringComparison.OrdinalIgnoreCase) ||
                                         (!string.IsNullOrEmpty(g.Executable) && g.Executable.Equals(sc.Executable, StringComparison.OrdinalIgnoreCase))))
                    {
                        continue;
                    }

                    AllGames.Add(sc);
                    CustomGames.Add(sc);
                    addedAny = true;
                }

                if (addedAny)
                {
                    var recentIds = RecentGames.Select(g => g.Id).ToList();
                    _scannerService.SaveOfflineData(AllGames.ToList(), CustomGames.ToList(), recentIds);
                    UpdateCounters();
                    ApplyFilter();
                }
            }
            catch { }
        }

        private void RunAiGameDetector()
        {
            if (!AiDetection || IsAiDetectionPromptOpen) return;

            try
            {
                var processes = Process.GetProcesses();
                foreach (var proc in processes)
                {
                    try
                    {
                        string pName = proc.ProcessName.ToLowerInvariant();
                        if (_knownProcessNames.Contains(pName)) continue;

                        string title = !string.IsNullOrWhiteSpace(proc.MainWindowTitle)
                            ? proc.MainWindowTitle.Trim()
                            : "";

                        string exe = "";
                        try { exe = proc.MainModule?.FileName ?? ""; } catch { }

                        // 1. Arka plan sistem servislerini, yardımcı araçları ve host süreçlerini kesinlikle engelle
                        if (IsSystemOrUtilityProcess(pName, title, exe))
                        {
                            continue;
                        }

                        bool hasValidWindow = proc.MainWindowHandle != IntPtr.Zero && !string.IsNullOrWhiteSpace(title) && title.Length >= 2;
                        bool matchesSignature = IsGameProcess(pName, Metin2);
                        bool isInGameDirectory = IsInKnownGameDirectory(exe);

                        // Süreç yalnızca bilinen imza veya geçerli bir oyun penceresi içeriyorsa kabul edilir
                        if (matchesSignature || (hasValidWindow && isInGameDirectory) || (hasValidWindow && IsLikelyGameTitle(title, pName)))
                        {
                            _knownProcessNames.Add(pName);

                            string displayTitle = !string.IsNullOrWhiteSpace(title)
                                ? title
                                : char.ToUpper(pName[0]) + pName.Substring(1);

                            // Eğer oyun zaten kütüphanede varsa doğrudan senkronize et
                            var existing = AllGames.FirstOrDefault(g => 
                                g.Name.Equals(displayTitle, StringComparison.OrdinalIgnoreCase) || 
                                (!string.IsNullOrEmpty(exe) && g.Executable != null && g.Executable.Equals(exe, StringComparison.OrdinalIgnoreCase)));

                            if (existing != null)
                            {
                                MarkGameAsRecent(existing);
                                ShowToastNotification(SelectedLanguageIndex == 0 
                                    ? $"🎮 {existing.Name} çalıştırıldı ve kütüphane güncellendi."
                                    : $"🎮 {existing.Name} running, library updated.");
                            }
                            else
                            {
                                // Yeni oyun: Kullanıcıya şık sağ alt onay kartı aç
                                DetectedGameName = displayTitle;
                                DetectedGameExe = exe;
                                DetectedGameProcessName = proc.ProcessName;
                                IsAiDetectionPromptOpen = true;

                                // Windows Sağ Alt Sistem Bildirimi
                                WindowsNotificationService.ShowGameDetectedNotification(LocalizationService.CurrentLanguage, displayTitle);
                            }
                            break;
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        [RelayCommand]
        public void ConfirmAddDetectedGame()
        {
            if (string.IsNullOrWhiteSpace(DetectedGameName)) return;

            var newGame = new GameItem
            {
                Id = "custom_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Name = DetectedGameName,
                Platform = "local",
                PlatformName = "Yerel Oyun",
                Executable = string.IsNullOrWhiteSpace(DetectedGameExe) ? null : DetectedGameExe,
                InstallPath = !string.IsNullOrWhiteSpace(DetectedGameExe) ? Path.GetDirectoryName(DetectedGameExe) : null,
                Launch = new GameLaunchInfo
                {
                    Type = "executable",
                    Path = DetectedGameExe
                }
            };

            AllGames.Add(newGame);
            CustomGames.Add(newGame);
            MarkGameAsRecent(newGame);

            var recentIds = RecentGames.Select(g => g.Id).ToList();
            _scannerService.SaveOfflineData(AllGames.ToList(), CustomGames.ToList(), recentIds);
            UpdateCounters();
            ApplyFilter();

            IsAiDetectionPromptOpen = false;
            ShowToastNotification(SelectedLanguageIndex == 0 
                ? $"✅ {DetectedGameName} kütüphanenize eklendi." 
                : $"✅ {DetectedGameName} added to library.");
        }

        [RelayCommand]
        public void DismissDetectedGame()
        {
            string name = DetectedGameName;
            IsAiDetectionPromptOpen = false;
            if (!string.IsNullOrWhiteSpace(name))
            {
                ShowToastNotification(SelectedLanguageIndex == 0 
                    ? $"✕ {name} yoksayıldı." 
                    : $"✕ {name} ignored.");
            }
        }

        private static bool IsGameProcess(string processName, bool allowMetin2)
        {
            if (!allowMetin2 && (processName.Contains("metin2") || processName.Contains("rinamt2") || processName.Contains("elitemt2")))
            {
                return false;
            }

            string[] gameSignatures = { "valorant", "fc26", "fifa", "fortnite", "marvel", "r6siege", "rainbowsix", "projectzomboid", "rinamt2", "metin2", "discovery", "gta_sa", "gta5", "donutcounty", "minecraft", "csgo", "cs2", "pubg", "dota2", "leagueclient", "overwatch", "apex", "genshin", "starrail", "cyberpunk2077", "witcher3", "eldenring" };
            return gameSignatures.Any(sig => processName.Contains(sig));
        }

        private static bool IsSystemOrUtilityProcess(string processName, string title, string exePath)
        {
            string p = processName.ToLowerInvariant();
            string exe = (exePath ?? "").ToLowerInvariant();

            string[] systemKeywords = {
                "service", "host", "helper", "agent", "broker", "daemon", "driver", "update",
                "installer", "setup", "overlay", "anticheat", "crashreport", "runtime", "server",
                "shell", "system", "client", "wrapper", "tray", "bridge", "manager", "sync",
                "rebound", "svchost", "runtimebroker", "sihost", "taskhostw", "shellexperiencehost",
                "startmenuexperiencehost", "searchhost", "searchapp", "textinputhost", "securityhealth",
                "applicationframehost", "systemsettings", "ctfmon", "smartscreen", "lockapp",
                "compkgsrv", "mousocoreworker", "tiworker", "trustedinstaller", "rundll32",
                "conhost", "dwm", "audiodg", "spoolsv", "wmi", "lsass", "csrss", "smss", "wininit",
                "winlogon", "explorer", "devenv", "rider", "code", "sublime", "notepad", "calculator",
                "chrome", "firefox", "msedge", "opera", "brave", "vivaldi", "spotify", "discord",
                "telegram", "whatsapp", "slack", "steam", "epicgames", "origin", "eadesktop",
                "battlenet", "riotclient", "gog", "ubisoft", "geforce", "radeon", "armoury",
                "icue", "razer", "steelseries", "logitech", "ghub", "obs64", "streamlabs",
                "vlc", "mpc", "qbittorrent", "torrent", "idm", "winrar", "7z", "everything",
                "odzen", "onyx", "terminal", "powershell", "cmd", "powershell_ise"
            };

            foreach (var kw in systemKeywords)
            {
                if (p.Contains(kw)) return true;
                if (!string.IsNullOrEmpty(exe) && Path.GetFileNameWithoutExtension(exe).ToLowerInvariant().Contains(kw)) return true;
            }

            if (!string.IsNullOrEmpty(exe))
            {
                if (exe.Contains(@"\windows\system32\") || 
                    exe.Contains(@"\windows\syswow64\") ||
                    exe.Contains(@"\windows\systemapps\") ||
                    exe.Contains(@"\windows\immersivecontrolpanel\"))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInKnownGameDirectory(string exePath)
        {
            if (string.IsNullOrEmpty(exePath)) return false;
            string low = exePath.ToLowerInvariant();
            return low.Contains(@"\steamapps\") ||
                   low.Contains(@"\epic games\") ||
                   low.Contains(@"\riot games\") ||
                   low.Contains(@"\ubisoft\") ||
                   low.Contains(@"\gog galaxy\") ||
                   low.Contains(@"\gog games\") ||
                   low.Contains(@"\ea games\") ||
                   low.Contains(@"\origin games\") ||
                   low.Contains(@"\xboxgames\") ||
                   low.Contains(@"\games\");
        }

        private static bool IsLikelyGameTitle(string title, string processName)
        {
            if (string.IsNullOrWhiteSpace(title) || title.Length < 3) return false;
            string low = title.ToLowerInvariant();
            if (low.Contains("settings") || low.Contains("ayar") || low.Contains("microsoft") || 
                low.Contains("windows") || low.Contains("manager") || low.Contains("task") ||
                low.Contains("document") || low.Contains("file") || low.Contains("editor"))
            {
                return false;
            }
            return true;
        }

        public void ShowToastNotification(string message)
        {
            ToastMessage = message;
            IsToastVisible = true;
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        [RelayCommand]
        public void CloseToast() => IsToastVisible = false;

        private void CheckAutostartStatus()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
                _autostart = key?.GetValue("ODZEN") != null;
                OnPropertyChanged(nameof(Autostart));
            }
            catch { }
        }

        private static void ApplyAutostartRegistry(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;

                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (enable && !string.IsNullOrEmpty(exePath))
                {
                    key.SetValue("ODZEN", $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue("ODZEN", false);
                }
            }
            catch { }
        }

        private async Task InitializeGamesAsync()
        {
            try
            {
                var (offlineAll, offlineCustom, recentIds) = _scannerService.LoadOfflineLibrary();
                if (offlineAll != null && offlineAll.Count > 0)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        AllGames.Clear();
                        foreach (var g in offlineAll.OrderBy(x => x.Name, StringComparer.Create(TrCulture, true)))
                            AllGames.Add(g);

                        CustomGames.Clear();
                        if (offlineCustom != null)
                        {
                            foreach (var cg in offlineCustom)
                                CustomGames.Add(cg);
                        }

                        RecentGames.Clear();
                        if (recentIds != null && recentIds.Count > 0)
                        {
                            foreach (var id in recentIds)
                            {
                                var found = AllGames.FirstOrDefault(g => g.Id == id);
                                if (found != null && !RecentGames.Contains(found)) RecentGames.Add(found);
                            }
                        }

                        SelectedGame = RecentGames.FirstOrDefault() ?? AllGames.FirstOrDefault();
                        UpdateCounters();
                        ApplyFilter();
                    });
                }
                else
                {
                    await ScanGamesAsync();
                }
            }
            catch { }
        }

        [RelayCommand]
        public void ResetSettingsToDefault()
        {
            SelectedLanguageIndex = 0;
            UiScale = 1.0;
            PendingUiScale = 1.0;
            PendingUiScaleText = "%100";
            ShowShowcase = true;
            Autostart = false;
            MinimizeToTrayOnClose = false;
            AutoScan = false;
            Metin2 = true;
            FetchLogosFromInternet = true;
            AiDetection = true;
            GpuThreshold = 75;
            CpuThreshold = 40;
            SaveCurrentSettings();
            ShowToastNotification(SelectedLanguageIndex == 0 ? "⚙️ Tüm uygulama ayarları fabrika varsayılanına sıfırlandı." : "⚙️ All settings have been reset to factory defaults.");
        }

        [RelayCommand]
        public void OpenAddGameModal()
        {
            IsAddGameModalOpen = true;
            ManualGameName = "";
            ManualGamePath = "";
            ManualGameArgs = "";
            ScanRunningWindows();
        }

        [RelayCommand]
        public void CloseAddGameModal()
        {
            IsAddGameModalOpen = false;
        }

        public void ScanRunningWindows()
        {
            RunningApps.Clear();
            try
            {
                var processes = Process.GetProcesses();
                var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var p in processes)
                {
                    try
                    {
                        if (p.Id <= 4) continue;
                        if (string.IsNullOrWhiteSpace(p.MainWindowTitle)) continue;

                        string exePath = "";
                        try { exePath = p.MainModule?.FileName ?? ""; } catch { }

                        if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) continue;
                        if (seenPaths.Contains(exePath)) continue;
                        seenPaths.Add(exePath);

                        string title = p.MainWindowTitle.Trim();
                        string pName = p.ProcessName;

                        if (pName.Equals("explorer", StringComparison.OrdinalIgnoreCase) ||
                            pName.Equals("Taskmgr", StringComparison.OrdinalIgnoreCase) ||
                            pName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase))
                            continue;

                        global::Avalonia.Media.Imaging.Bitmap? iconBmp = null;
                        try
                        {
                            using var ico = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                            if (ico != null)
                            {
                                using var bmp = ico.ToBitmap();
                                using var ms = new MemoryStream();
                                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                                ms.Position = 0;
                                iconBmp = new global::Avalonia.Media.Imaging.Bitmap(ms);
                            }
                        }
                        catch { }

                        RunningApps.Add(new RunningAppItem
                        {
                            Name = title,
                            ProcessName = pName,
                            ExecutablePath = exePath,
                            Icon = iconBmp
                        });
                    }
                    catch { }
                }
            }
            catch { }
        }

        [RelayCommand]
        public void AddRunningApp(RunningAppItem? app)
        {
            if (app == null) return;
            if (string.IsNullOrEmpty(app.ExecutablePath)) return;

            var newGame = new GameItem
            {
                Id = $"custom_{Guid.NewGuid():N}",
                Name = !string.IsNullOrWhiteSpace(app.Name) ? app.Name : app.ProcessName,
                Platform = "local",
                PlatformName = "Yerel Oyun",
                Executable = app.ExecutablePath,
                InstallPath = Path.GetDirectoryName(app.ExecutablePath),
                IsCustom = true
            };

            AllGames.Add(newGame);
            CustomGames.Add(newGame);
            MarkGameAsRecent(newGame);
            UpdateCounters();
            ApplyFilter();

            var recentIds = RecentGames.Select(g => g.Id).ToList();
            _scannerService.SaveOfflineData(AllGames.ToList(), CustomGames.ToList(), recentIds);

            IsAddGameModalOpen = false;
            ShowToastNotification($"✅ Açık Uygulama Kütüphaneye Eklendi: {newGame.Name}");
        }

        [RelayCommand]
        public async Task BrowseManualGameAsync()
        {
            try
            {
                if (global::Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
                {
                    var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
                    if (topLevel != null)
                    {
                        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                        {
                            Title = "Oyun / Uygulama Dosyası Seçin (.exe / .lnk)",
                            AllowMultiple = false,
                            FileTypeFilter = new List<FilePickerFileType>
                            {
                                new("Çalıştırılabilir Dosyalar") { Patterns = new[] { "*.exe", "*.lnk", "*.bat" } }
                            }
                        });

                        if (files.Count > 0)
                        {
                            var file = files[0];
                            ManualGamePath = file.Path.LocalPath;
                            if (string.IsNullOrWhiteSpace(ManualGameName))
                            {
                                ManualGameName = Path.GetFileNameWithoutExtension(ManualGamePath);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        [RelayCommand]
        public void SaveManualGame()
        {
            if (string.IsNullOrWhiteSpace(ManualGamePath))
            {
                ShowToastNotification("⚠️ Lütfen önce bir çalıştırılabilir dosya (.exe) seçin.");
                return;
            }

            string name = string.IsNullOrWhiteSpace(ManualGameName)
                ? Path.GetFileNameWithoutExtension(ManualGamePath)
                : ManualGameName;

            var newGame = new GameItem
            {
                Id = $"custom_{Guid.NewGuid():N}",
                Name = name,
                Platform = "local",
                PlatformName = "Yerel Oyun",
                Executable = ManualGamePath,
                InstallPath = Path.GetDirectoryName(ManualGamePath),
                LaunchArgs = ManualGameArgs,
                IsCustom = true
            };

            AllGames.Add(newGame);
            CustomGames.Add(newGame);
            MarkGameAsRecent(newGame);
            UpdateCounters();
            ApplyFilter();

            var recentIds = RecentGames.Select(g => g.Id).ToList();
            _scannerService.SaveOfflineData(AllGames.ToList(), CustomGames.ToList(), recentIds);

            IsAddGameModalOpen = false;
            ShowToastNotification($"✅ Özel Oyun Başarıyla Eklendi: {newGame.Name}");
        }

        [RelayCommand]
        public void AddCustomGame()
        {
            OpenAddGameModal();
        }

        [RelayCommand]
        public void RemoveCustomGame(GameItem? game)
        {
            if (game == null) return;

            AllGames.Remove(game);
            CustomGames.Remove(game);
            RecentGames.Remove(game);

            if (SelectedGame == game)
            {
                SelectedGame = AllGames.FirstOrDefault();
            }

            var recentIds = RecentGames.Select(g => g.Id).ToList();
            _scannerService.SaveOfflineData(AllGames.ToList(), CustomGames.ToList(), recentIds);
            UpdateCounters();
            ApplyFilter();

            ShowToastNotification($"🗑️ {game.Name} kütüphaneden kaldırıldı.");
        }

        [RelayCommand]
        public async Task ScanGamesAsync()
        {
            IsScanning = true;
            try
            {
                var games = await _scannerService.ScanGamesAsync();
                var savedCustoms = _scannerService.LoadCustomGames();
                var recentIds = _scannerService.LoadRecentGameIds();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AllGames.Clear();
                    foreach (var g in games.OrderBy(x => x.Name, StringComparer.Create(TrCulture, true)))
                    {
                        AllGames.Add(g);
                    }

                    // Load custom games
                    CustomGames.Clear();
                    foreach (var c in savedCustoms)
                    {
                        CustomGames.Add(c);
                        if (!AllGames.Any(g => g.Id == c.Id))
                        {
                            AllGames.Add(c);
                        }
                    }

                    // Load recent 5 games
                    RecentGames.Clear();
                    foreach (var rId in recentIds)
                    {
                        var matched = AllGames.FirstOrDefault(g => g.Id == rId);
                        if (matched != null && !RecentGames.Contains(matched))
                        {
                            RecentGames.Add(matched);
                        }
                    }

                    // Fill recent games up to 5 if empty
                    if (RecentGames.Count == 0)
                    {
                        foreach (var g in AllGames.Take(5))
                        {
                            RecentGames.Add(g);
                        }
                    }

                    // Showcase the most recent game
                    if (RecentGames.Count > 0)
                    {
                        SelectedGame = RecentGames[0];
                    }
                    else if (AllGames.Count > 0)
                    {
                        SelectedGame = AllGames[0];
                    }

                    UpdateCounters();
                    ApplyFilter();

                    var currentRecentIds = RecentGames.Select(g => g.Id).ToList();
                    _scannerService.SaveOfflineData(AllGames.ToList(), CustomGames.ToList(), currentRecentIds);
                });
            }
            catch { }
            finally
            {
                IsScanning = false;
            }
        }

        private void MarkGameAsRecent(GameItem game)
        {
            SelectedGame = game;

            if (RecentGames.Contains(game))
            {
                RecentGames.Remove(game);
            }
            RecentGames.Insert(0, game);

            while (RecentGames.Count > 5)
            {
                RecentGames.RemoveAt(RecentGames.Count - 1);
            }

            var recentIds = RecentGames.Select(g => g.Id).ToList();
            _scannerService.SaveOfflineData(AllGames.ToList(), CustomGames.ToList(), recentIds);
        }

        private void ApplyFilter()
        {
            FilteredGames.Clear();
            var query = SearchText.Trim();
            var matchedList = new List<GameItem>();

            foreach (var game in AllGames)
            {
                if (!Metin2 && game.Platform.Equals("metin2", StringComparison.OrdinalIgnoreCase)) continue;

                if (!string.IsNullOrEmpty(query))
                {
                    bool matchName = TrCulture.CompareInfo.IndexOf(game.Name, query, CompareOptions.IgnoreCase) >= 0;
                    bool matchPlat = TrCulture.CompareInfo.IndexOf(game.PlatformName, query, CompareOptions.IgnoreCase) >= 0;
                    if (!matchName && !matchPlat) continue;
                }

                if (SelectedPlatform == "all")
                {
                    matchedList.Add(game);
                }
                else if (SelectedPlatform == "favorites" && game.IsFavorite)
                {
                    matchedList.Add(game);
                }
                else if (game.Platform.Equals(SelectedPlatform, StringComparison.OrdinalIgnoreCase) ||
                        (SelectedPlatform == "battlenet" && game.Platform == "battle_net"))
                {
                    matchedList.Add(game);
                }
            }

            foreach (var g in matchedList.OrderBy(x => x.Name, StringComparer.Create(TrCulture, true)))
            {
                FilteredGames.Add(g);
            }
        }

        private void UpdateCounters()
        {
            AllGamesCount = AllGames.Count;
            FavGamesCount = AllGames.Count(g => g.IsFavorite);
        }

        private void UpdateCategoryInfo()
        {
            CurrentSectionTitle = SelectedPlatform switch
            {
                "all" => "Tüm Oyunlar",
                "favorites" => "Favori Oyunlar",
                "steam" => "Steam Kütüphanesi",
                "epic" => "Epic Games Kütüphanesi",
                "ea" => "EA App Oyunları",
                "minecraft" => "Minecraft",
                "riot" => "Riot Games",
                "battlenet" or "battle_net" => "Battle.net",
                "gog" => "GOG Galaxy",
                "ubisoft" => "Ubisoft Connect",
                "rockstar" => "Rockstar Games",
                "xbox" => "XBOX & Game Pass",
                "amazon" => "Amazon Games",
                "metin2" => "Metin2 Sunucuları",
                "local" => "Yerel Oyunlar",
                _ => "Oyunlar"
            };

            CurrentSectionSubtitle = SelectedPlatform switch
            {
                "favorites" => "Yıldızladığınız favori oyunlarınız listeleniyor.",
                "all" => "Sistemde ve kütüphanelerde tespit edilen oyunlar listeleniyor.",
                _ => $"{CurrentSectionTitle} altında tespit edilen oyunlar."
            };
        }

        [RelayCommand]
        public void SelectPlatform(string platform)
        {
            SelectedPlatform = platform;
            IsSettingsOpen = false;
        }

        [RelayCommand]
        public void SelectGame(GameItem? game)
        {
            if (game != null)
            {
                MarkGameAsRecent(game);
            }
        }

        [RelayCommand]
        public void ToggleFavorite(GameItem? game)
        {
            if (game == null) return;
            game.IsFavorite = !game.IsFavorite;
            var recentIds = RecentGames.Select(g => g.Id).ToList();
            _scannerService.SaveOfflineData(AllGames.ToList(), CustomGames.ToList(), recentIds);
            UpdateCounters();
            ApplyFilter();
        }

        [RelayCommand]
        public void LaunchGame(GameItem? game)
        {
            var target = game ?? SelectedGame;
            if (target == null) return;
            
            MarkGameAsRecent(target);
            var (success, message) = _scannerService.LaunchGame(target);
            ShowToastNotification(message);
        }

        public void UpdateLogoCacheSize()
        {
            try
            {
                long totalBytes = 0;
                string artworkLogos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ODZEN", "artwork", "logos");
                string sgdbLogos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ODZEN", "steamgriddb_logos");

                if (Directory.Exists(artworkLogos))
                {
                    var di = new DirectoryInfo(artworkLogos);
                    foreach (var fi in di.EnumerateFiles()) totalBytes += fi.Length;
                }
                if (Directory.Exists(sgdbLogos))
                {
                    var di = new DirectoryInfo(sgdbLogos);
                    foreach (var fi in di.EnumerateFiles()) totalBytes += fi.Length;
                }

                if (totalBytes < 1024)
                    LogoCacheSizeText = $"{totalBytes} B";
                else if (totalBytes < 1024 * 1024)
                    LogoCacheSizeText = $"{(totalBytes / 1024.0):F1} KB";
                else
                    LogoCacheSizeText = $"{(totalBytes / (1024.0 * 1024.0)):F1} MB";
            }
            catch
            {
                LogoCacheSizeText = "0 B";
            }
        }

        [RelayCommand]
        public void OpenLogoFolder()
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ODZEN", "artwork", "logos");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowToastNotification($"Klasör açılamadı: {ex.Message}");
            }
        }

        [RelayCommand]
        public void ClearLogoCache()
        {
            try
            {
                string artworkLogos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ODZEN", "artwork", "logos");
                string sgdbLogos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ODZEN", "steamgriddb_logos");

                int count = 0;
                if (Directory.Exists(artworkLogos))
                {
                    foreach (var f in Directory.GetFiles(artworkLogos))
                    {
                        try { File.Delete(f); count++; } catch { }
                    }
                }
                if (Directory.Exists(sgdbLogos))
                {
                    foreach (var f in Directory.GetFiles(sgdbLogos))
                    {
                        try { File.Delete(f); count++; } catch { }
                    }
                }

                ArtworkPipelineService.ClearCache();
                foreach (var g in AllGames)
                {
                    g.NotifyIconChanged();
                }
                ApplyFilter();
                UpdateLogoCacheSize();
                ShowToastNotification(SelectedLanguageIndex == 0 ? $"🗑️ Tüm logo önbelleği temizlendi ({count} dosya)." : $"🗑️ All logo cache cleared ({count} files).");
            }
            catch (Exception ex)
            {
                ShowToastNotification($"Hata: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task DownloadMissingLogosAsync()
        {
            if (IsLogoDownloading) return;
            var missing = AllGames.Where(g => !OpenArtworkPipelineEngine.HasLogo(g.Id) && !SteamGridDBLogoEngine.HasLogo(g.Id)).ToList();

            if (missing.Count == 0)
            {
                ShowToastNotification(SelectedLanguageIndex == 0 ? "✅ Kütüphanedeki tüm oyunların logoları zaten hazır!" : "✅ All library games already have logos!");
                return;
            }

            IsLogoDownloading = true;
            LogoDownloadProgress = 0;
            LogoDownloadStatusText = SelectedLanguageIndex == 0 ? $"Eksik logolar taranıyor (0/{missing.Count})..." : $"Scanning missing logos (0/{missing.Count})...";

            ArtworkPipelineService.ClearCache();

            for (int i = 0; i < missing.Count; i++)
            {
                var g = missing[i];
                LogoDownloadStatusText = SelectedLanguageIndex == 0 
                    ? $"İndiriliyor ({i + 1}/{missing.Count}): {g.Name}" 
                    : $"Downloading ({i + 1}/{missing.Count}): {g.Name}";
                LogoDownloadProgress = (double)(i + 1) / missing.Count * 100.0;

                await OpenArtworkPipelineEngine.ResolveAndDownloadLogoAsync(g.Id, g.Name, g.Platform, g.StoreId);
                g.NotifyIconChanged();
            }

            ArtworkPipelineService.ClearCache();
            ApplyFilter();
            UpdateLogoCacheSize();
            IsLogoDownloading = false;
            ShowToastNotification(SelectedLanguageIndex == 0 ? "✅ Eksik logolar başarıyla indirildi!" : "✅ Missing logos successfully downloaded!");
        }

        [RelayCommand]
        public async Task RefreshOnlineLogosAsync()
        {
            if (IsLogoDownloading) return;
            if (AllGames.Count == 0) return;

            IsLogoDownloading = true;
            LogoDownloadProgress = 0;
            LogoDownloadStatusText = SelectedLanguageIndex == 0 ? $"Tüm logolar taranıyor (0/{AllGames.Count})..." : $"Scanning all logos (0/{AllGames.Count})...";

            ArtworkPipelineService.ClearCache();

            for (int i = 0; i < AllGames.Count; i++)
            {
                var g = AllGames[i];
                LogoDownloadStatusText = SelectedLanguageIndex == 0 
                    ? $"İndiriliyor ({i + 1}/{AllGames.Count}): {g.Name}" 
                    : $"Downloading ({i + 1}/{AllGames.Count}): {g.Name}";
                LogoDownloadProgress = (double)(i + 1) / AllGames.Count * 100.0;

                string path = OpenArtworkPipelineEngine.GetLogoPath(g.Id);
                if (File.Exists(path)) { try { File.Delete(path); } catch { } }

                await OpenArtworkPipelineEngine.ResolveAndDownloadLogoAsync(g.Id, g.Name, g.Platform, g.StoreId);
                g.NotifyIconChanged();
            }

            ArtworkPipelineService.ClearCache();
            ApplyFilter();
            UpdateLogoCacheSize();
            IsLogoDownloading = false;
            ShowToastNotification(SelectedLanguageIndex == 0 ? "✅ Tüm logolar açık kaynaklardan başarıyla güncellendi!" : "✅ All logos refreshed from open sources!");
        }

        [RelayCommand]
        public void OpenGameDetail(GameItem? game)
        {
            if (game == null) return;
            SelectedGame = game;
            IsGameDetailOpen = true;
        }

        [RelayCommand]
        public void CloseGameDetail()
        {
            IsGameDetailOpen = false;
            SelectedGame = null;
        }

        [RelayCommand]
        public void OpenGameFolder(GameItem? game)
        {
            var target = game ?? SelectedGame;
            if (target == null) return;

            string? path = target.InstallPath;
            if (string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(target.Executable))
            {
                path = Path.GetDirectoryName(target.Executable);
            }

            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{path}\"",
                        UseShellExecute = true
                    });
                    ShowToastNotification($"📁 Klasör açıldı: {Path.GetFileName(path)}");
                }
                catch (Exception ex)
                {
                    ShowToastNotification($"❌ Klasör açılamadı: {ex.Message}");
                }
            }
            else
            {
                ShowToastNotification("⚠️ Oyunun kurulum klasörü bulunamadı.");
            }
        }

        [RelayCommand]
        public void CreateDesktopShortcut(GameItem? game)
        {
            var target = game ?? SelectedGame;
            if (target == null) return;

            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string shortcutPath = Path.Combine(desktopPath, $"{SanitizeFileName(target.Name)}.url");

                string urlContent = "";
                if (target.Platform.Equals("steam", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(target.StoreId))
                {
                    urlContent = $"[InternetShortcut]\nURL=steam://rungameid/{target.StoreId}\nIconIndex=0\n";
                }
                else if (!string.IsNullOrEmpty(target.Executable))
                {
                    urlContent = $"[InternetShortcut]\nURL=file:///{target.Executable.Replace('\\', '/')}\nIconIndex=0\nIconFile={target.Executable}\n";
                }

                if (!string.IsNullOrEmpty(urlContent))
                {
                    File.WriteAllText(shortcutPath, urlContent);
                    ShowToastNotification($"✨ Masaüstü kısayolu oluşturuldu: {target.Name}");
                }
            }
            catch (Exception ex)
            {
                ShowToastNotification($"❌ Kısayol oluşturulamadı: {ex.Message}");
            }
        }

        [RelayCommand]
        public async Task PickCustomLogoAsync()
        {
            if (SelectedGame == null) return;

            try
            {
                var topLevel = TopLevel.GetTopLevel(App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null);
                if (topLevel != null)
                {
                    var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Özel Oyun Logosu Seç (.png, .ico, .jpg, .svg)",
                        AllowMultiple = false,
                        FileTypeFilter = new List<FilePickerFileType>
                        {
                            new("Resim Dosyaları") { Patterns = new[] { "*.png", "*.ico", "*.jpg", "*.jpeg", "*.svg" } }
                        }
                    });

                    if (files.Count > 0)
                    {
                        string localPath = files[0].Path.LocalPath;
                        SelectedGame.CustomLogoPath = localPath;
                        SelectedGame.NotifyIconChanged();
                        ShowToastNotification($"🖼️ Yeni logo seçildi: {Path.GetFileName(localPath)}");
                    }
                }
            }
            catch (Exception ex)
            {
                ShowToastNotification($"❌ Logo seçilemedi: {ex.Message}");
            }
        }

        [RelayCommand]
        public void ClearCustomLogo()
        {
            if (SelectedGame == null) return;
            SelectedGame.CustomLogoPath = null;
            SelectedGame.PreferOnlineLogo = false;
            SelectedGame.NotifyIconChanged();
            ShowToastNotification("🖥️ Oyunun sistemdeki orijinal yerel logosuna dönüldü.");
        }

        [RelayCommand]
        public void UseOnlineLogo()
        {
            if (SelectedGame == null) return;
            SelectedGame.CustomLogoPath = null;
            SelectedGame.PreferOnlineLogo = true;

            // Önceki indirilmiş logo dosyasını diskten silerek taze arama tetikle
            string logoPath = OpenArtworkPipelineEngine.GetLogoPath(SelectedGame.Id);
            if (File.Exists(logoPath))
            {
                try { File.Delete(logoPath); } catch { }
            }
            ArtworkPipelineService.ClearCache();

            OpenArtworkPipelineEngine.QueueDownload(SelectedGame.Id, SelectedGame.Name, SelectedGame.Platform, SelectedGame.StoreId, () =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    SelectedGame.NotifyIconChanged();
                    OnPropertyChanged(nameof(FilteredGames));
                });
            });
            SelectedGame.NotifyIconChanged();
            ShowToastNotification(SelectedLanguageIndex == 0 ? "🌐 Çevrimiçi logo taranıyor ve güncelleniyor..." : "🌐 Fetching online logo...");
        }

        [RelayCommand]
        public async Task OpenLogoPickerAsync()
        {
            if (SelectedGame == null) return;
            LogoPickerSearchQuery = SelectedGame.Name;
            LogoPickerCustomUrl = "";
            IsLogoPickerOpen = true;
            await SearchLogoCandidatesAsync();
        }

        [RelayCommand]
        public void CloseLogoPicker()
        {
            IsLogoPickerOpen = false;
        }

        [RelayCommand]
        public async Task SearchLogoCandidatesAsync()
        {
            if (string.IsNullOrWhiteSpace(LogoPickerSearchQuery)) return;
            IsLogoPickerLoading = true;
            LogoCandidates.Clear();

            try
            {
                var list = await OpenArtworkPipelineEngine.SearchLogoCandidatesAsync(LogoPickerSearchQuery, SelectedGame?.Publisher);
                foreach (var c in list)
                {
                    LogoCandidates.Add(c);
                }
            }
            catch { }
            finally
            {
                IsLogoPickerLoading = false;
            }
        }

        [RelayCommand]
        public async Task SelectLogoCandidateAsync(LogoCandidate? candidate)
        {
            if (candidate == null || SelectedGame == null) return;

            string targetPath = OpenArtworkPipelineEngine.GetLogoPath(SelectedGame.Id);
            bool ok = await OpenArtworkPipelineEngine.SaveCustomLogoFromUrlAsync(SelectedGame.Id, candidate.DownloadUrl);
            if (ok)
            {
                SelectedGame.CustomLogoPath = targetPath;
                SelectedGame.PreferOnlineLogo = true;
                ArtworkPipelineService.ClearCache();
                SelectedGame.NotifyIconChanged();
                ApplyFilter();
                UpdateLogoCacheSize();
                IsLogoPickerOpen = false;
                ShowToastNotification(SelectedLanguageIndex == 0 ? $"✅ Logo başarıyla uygulandı: {candidate.Title}" : $"✅ Logo applied: {candidate.Title}");
            }
            else
            {
                ShowToastNotification(SelectedLanguageIndex == 0 ? "❌ Logo indirilemedi veya geçersiz format." : "❌ Failed to download logo.");
            }
        }

        [RelayCommand]
        public async Task ApplyCustomLogoUrlAsync()
        {
            if (SelectedGame == null || string.IsNullOrWhiteSpace(LogoPickerCustomUrl)) return;
            string url = LogoPickerCustomUrl.Trim();

            string targetPath = OpenArtworkPipelineEngine.GetLogoPath(SelectedGame.Id);
            bool ok = await OpenArtworkPipelineEngine.SaveCustomLogoFromUrlAsync(SelectedGame.Id, url);
            if (ok)
            {
                SelectedGame.CustomLogoPath = targetPath;
                SelectedGame.PreferOnlineLogo = true;
                ArtworkPipelineService.ClearCache();
                SelectedGame.NotifyIconChanged();
                ApplyFilter();
                UpdateLogoCacheSize();
                IsLogoPickerOpen = false;
                ShowToastNotification(SelectedLanguageIndex == 0 ? "✅ Özel logo bağlantısı başarıyla uygulandı!" : "✅ Custom logo applied from URL!");
            }
            else
            {
                ShowToastNotification(SelectedLanguageIndex == 0 ? "❌ Geçersiz görsel URL'si veya indirilemedi." : "❌ Invalid image URL or download failed.");
            }
        }

        [RelayCommand]
        public void SaveGameConfig()
        {
            if (SelectedGame == null) return;
            SelectedGame.NotifyIconChanged();
            var recentIds = RecentGames.Select(g => g.Id).ToList();
            _scannerService.SaveOfflineData(AllGames.ToList(), CustomGames.ToList(), recentIds);
            ApplyFilter();
            IsGameDetailOpen = false;
            ShowToastNotification(SelectedLanguageIndex == 0 
                ? $"💾 '{SelectedGame.Name}' yapılandırması diske kaydedildi!" 
                : $"💾 '{SelectedGame.Name}' configuration saved to disk!");
        }

        [RelayCommand]
        public void SelectScaleCandidate(object? param)
        {
            double scale = 1.0;
            if (param is double d) scale = d;
            else if (param is string s && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed)) scale = parsed;

            PendingUiScale = Math.Clamp(scale, 0.75, 1.45);
            PendingUiScaleText = $"%{(int)Math.Round(PendingUiScale * 100)}";
        }

        [RelayCommand]
        public void ApplyUiScale()
        {
            UiScale = PendingUiScale;
            UiScaleText = PendingUiScaleText;
            SaveCurrentSettings();
            ShowToastNotification(SelectedLanguageIndex == 0 ? $"🔍 Arayüz ölçeği {UiScaleText} olarak uygulandı." : $"🔍 UI scale applied: {UiScaleText}.");
        }

        partial void OnPendingUiScaleChanged(double value)
        {
            PendingUiScaleText = $"%{(int)Math.Round(value * 100)}";
        }

        partial void OnSelectedLanguageIndexChanged(int value)
        {
            var lang = (AppLanguage)Math.Clamp(value, 0, 7);
            LocalizationService.CurrentLanguage = lang;

            switch (lang)
            {
                case AppLanguage.German: // Almanca
                    TxtSearchWatermark = "Spiele oder Plattformen suchen...";
                    TxtAllGames = "Alle Spiele";
                    TxtFavorites = "Favoriten";
                    TxtLocalGames = "Lokale Spiele";
                    TxtScan = "Scannen";
                    TxtAdd = "Hinzufügen";
                    TxtSettings = "Einstellungen";
                    TxtScanning = "Wird gescannt...";
                    TxtLastOpened = "ZULETZT GEÖFFNET";
                    TxtPlayNow = "JETZT SPIELEN";
                    TxtRecentGames = "KÜRZLICH GESPIELT";
                    TxtSelectGame = "Spiel auswählen";
                    TxtInstalled = "Installiert";
                    TxtPlay = "Spielen";
                    TxtGameTitle = "SPIELTITEL";
                    TxtReadyToPlay = "Bereit zum Spielen in deiner Bibliothek";
                    TxtCustomLogoMedia = "Benutzerdefiniertes Logo & Medien";
                    TxtChangeLogo = "Logo ändern";
                    TxtSystemLogo = "System-Logo";
                    TxtOnlineLogo = "Online-Logo";
                    TxtSearchAndPickLogo = "Online suchen & wählen";
                    TxtLaunchArgs = "STARTPARAMETER (ARGS)";
                    TxtQuickLaunch = "SCHNELLSTART / VERKNÜPFUNGSBEFEHL";
                    TxtFolder = "Ordner";
                    TxtDesktopShortcut = "Desktop-Verknüpfung";
                    TxtRemove = "Entfernen";
                    TxtSave = "Speichern";
                    TxtAppSettings = "Anwendungseinstellungen";
                    TxtAppSettingsSubtitle = "Konfiguriere ODZEN Systemeinstellungen und intelligente Hintergrund-Spielerkennung.";
                    TxtLanguageChange = "Sprachauswahl";
                    TxtLanguageDesc = "Wähle die Sprache der Benutzeroberfläche";
                    TxtUiScaleTitle = "UI- & Anzeige-Skalierung";
                    TxtUiScaleDesc = "Passe die Größe aller Texte, Karten und Menüs an";
                    TxtApplyScale = "⚡ UI-Skalierung anwenden";
                    TxtShowcaseTitle = "Showcase & Anzeigeoptionen";
                    TxtShowShowcaseOpt = "Showcase-Bereich anzeigen";
                    TxtShowShowcaseDesc = "Zeigt/versteckt das große Showcase-Panel auf der Startseite";
                    TxtShowMusicOpt = "Musik-Dienste anzeigen";
                    TxtShowMusicDesc = "Zeigt/versteckt Spotify, YouTube Music usw. in der oberen Leiste";
                    TxtShowDiscordOpt = "Discord-Button anzeigen";
                    TxtShowDiscordDesc = "Zeigt/versteckt den Discord-Schnellstart-Button in der oberen Leiste";
                    TxtSystemConfig = "Systemkonfiguration";
                    TxtAutostart = "Mit Windows starten";
                    TxtAutostartDesc = "Startet leise im Hintergrund beim Windows-Start";
                    TxtMinimizeTray = "Beim Schließen in System-Tray minimieren";
                    TxtMinimizeTrayDesc = "Versteckt beim Klick auf ✕ im System-Tray statt zu schließen";
                    TxtAutoScan = "Automatische Verknüpfungssuche";
                    TxtAutoScanDesc = "Überwacht Desktop und Startmenü kontinuierlich";
                    TxtMetin2Detect = "Metin2 Server-Erkennung";
                    TxtMetin2DetectDesc = "In Plattformliste anzeigen, wenn Server erkannt werden";
                    TxtOnlineMediaConfig = "Online-Medien & Logo-Konfiguration";
                    TxtDownloadOnlineLogos = "Original-Logos aus dem Internet laden (HD/4K)";
                    TxtDownloadOnlineLogosDesc = "Lädt transparente 4K-Logos aus dem Steam Store herunter";
                    TxtRefreshAllLogos = "Alle Logos online aktualisieren";
                    TxtRefreshAllLogosDesc = "Lädt die neuesten Logos für alle Bibliotheksspiele neu herunter";
                    TxtRefreshLogosBtn = "Alle aktualisieren";
                    TxtDownloadMissingLogos = "Nur fehlende laden";
                    TxtOpenLogoFolder = "Ordner öffnen";
                    TxtClearLogoCache = "Cache leeren";
                    TxtSourcesTitle = "Suchquellen:";
                    TxtSteamGridDbKeyPlaceholder = "SteamGridDB API-Schlüssel (Optional)...";
                    TxtAiEngine = "Intelligente Hintergrund-Spielerkennung";
                    TxtAiDetection = "Intelligente Spielerkennung (Hintergrund)";
                    TxtAiDetectionDesc = "Erkennt neue Spiele im Hintergrund über Systemressourcen und bittet um Bestätigung zur Aufnahme in die Bibliothek.";
                    TxtGpuThreshold = "GPU-Nutzungsschwelle (5%-Schritte)";
                    TxtCpuThreshold = "CPU-Nutzungsschwelle (5%-Schritte)";
                    TxtBack = "Zurück";
                    TxtSaveSettings = "Schließen / Zurück";
                    TxtResetSettings = "Standardeinstellungen";
                    TxtAddGameTitle = "Spiel & App hinzufügen";
                    TxtRunningAppsTitle = "Laufende Anwendungen & Fenster";
                    TxtManualAddTitle = "Manuell hinzufügen";
                    TxtPlatformLaunchers = "Plattformen";
                    TxtPlatformLaunchersSubtitle = "Starte offizielle Spiele-Clients direkt von deinem PC.";
                    TxtBackToLibrary = "Zurück zur Bibliothek";
                    TxtRefresh = "Aktualisieren";
                    TxtBrowseExe = "EXE / Verknüpfung auswählen";
                    TxtFilePath = "DATEIPFAD";
                    TxtLaunchArgsOptional = "STARTPARAMETER (OPTIONAL)";
                    TxtAddToLibraryBtn = "Zur Bibliothek hinzufügen";
                    TxtAiPromptTitle = "Neues Spiel gestartet";
                    TxtYesAdd = "Ja, Hinzufügen";
                    TxtNoDismiss = "Nein, Überspringen";
                    TxtLaunch = "Starten";
                    TxtDeveloperCredits = "Entwickler & Credits";
                    TxtVisitGitHub = "GitHub-Profil öffnen";
                    CurrentSectionTitle = "Alle Spiele";
                    CurrentSectionSubtitle = "In deinem System erkannte Spiele werden aufgelistet.";
                    ShowToastNotification("🇩🇪 Sprache auf Deutsch geändert.");
                    break;

                case AppLanguage.Bulgarian: // Bulgarca
                    TxtSearchWatermark = "Търсене на игри или платформи...";
                    TxtAllGames = "Всички игри";
                    TxtFavorites = "Любими";
                    TxtLocalGames = "Локални игри";
                    TxtScan = "Сканиране";
                    TxtAdd = "Добавяне";
                    TxtSettings = "Настройки";
                    TxtScanning = "Сканиране...";
                    TxtLastOpened = "ПОСЛЕДНО ОТВОРЕНО";
                    TxtPlayNow = "ИГРАЙ СЕГА";
                    TxtRecentGames = "СКОРО ИГРАНИ";
                    TxtSelectGame = "Изберете игра";
                    TxtInstalled = "Инсталирана";
                    TxtPlay = "Играй";
                    TxtGameTitle = "ЗАГЛАВИЕ НА ИГРАТА";
                    TxtReadyToPlay = "Готова за игра във вашата библиотека";
                    TxtCustomLogoMedia = "Персонализирано лого и медия";
                    TxtChangeLogo = "Смяна на лого";
                    TxtSystemLogo = "Системно лого";
                    TxtOnlineLogo = "Онлайн лого";
                    TxtSearchAndPickLogo = "Търси онлайн & избери";
                    TxtLaunchArgs = "ПАРАМЕТРИ ЗА СТАРТИРАНЕ (ARGS)";
                    TxtQuickLaunch = "БЪРЗ СТАРТ / КОМАНДА ЗА ПРЯК ПЪТ";
                    TxtFolder = "Папка";
                    TxtDesktopShortcut = "Пряк път на работния плот";
                    TxtRemove = "Премахване";
                    TxtSave = "Запази";
                    TxtAppSettings = "Настройки на приложението";
                    TxtAppSettingsSubtitle = "Конфигурирайте системните предпочитания на ODZEN и интелигентното фоново откриване на игри.";
                    TxtLanguageChange = "Избор на език";
                    TxtLanguageDesc = "Изберете език на интерфейса";
                    TxtUiScaleTitle = "Мащабиране на интерфейса";
                    TxtUiScaleDesc = "Настройте размера на всички карти, текстове и менюта";
                    TxtApplyScale = "⚡ Приложи мащаба";
                    TxtShowcaseTitle = "Предпочитания за витрина";
                    TxtShowShowcaseOpt = "Показване на витрина";
                    TxtShowShowcaseDesc = "Показва/скрива големия панел с последно играната игра";
                    TxtShowMusicOpt = "Показване на музикални бутони";
                    TxtShowMusicDesc = "Показва/скрива Spotify, YouTube Music и др. в горната лента";
                    TxtShowDiscordOpt = "Показване на бутон за Discord";
                    TxtShowDiscordDesc = "Показва/скрива бутона за бърз достъп до Discord в горната лента";
                    TxtSystemConfig = "Системна конфигурация";
                    TxtAutostart = "Стартиране с Windows";
                    TxtAutostartDesc = "Работи тихо във фонов режим при стартиране на Windows";
                    TxtMinimizeTray = "Минимизиране в системния трей при затваряне";
                    TxtMinimizeTrayDesc = "Скрива в системния трей при натискане на ✕";
                    TxtAutoScan = "Автоматично сканиране на преки пътища";
                    TxtAutoScanDesc = "Непрекъснато наблюдава десктопа и Start менюто";
                    TxtMetin2Detect = "Откриване на Metin2 сървъри";
                    TxtMetin2DetectDesc = "Показване в списъка с платформи при откриване на сървъри";
                    TxtOnlineMediaConfig = "Конфигурация на онлайн медия и лого";
                    TxtDownloadOnlineLogos = "Изтегляне на оригинални лога от интернет (HD/4K)";
                    TxtDownloadOnlineLogosDesc = "Изтегля прозрачни 4K лога от Steam Store";
                    TxtRefreshAllLogos = "Обновяване на всички лога онлайн";
                    TxtRefreshAllLogosDesc = "Изтегля наново най-новите лога за всички игри";
                    TxtRefreshLogosBtn = "Обнови всички";
                    TxtDownloadMissingLogos = "Само липсващи";
                    TxtOpenLogoFolder = "Отвори папка";
                    TxtClearLogoCache = "Изчисти кеша";
                    TxtSourcesTitle = "Източници на търсене:";
                    TxtSteamGridDbKeyPlaceholder = "SteamGridDB API ключ (По избор)...";
                    TxtAiEngine = "Интелигентно фоново откриване на игри";
                    TxtAiDetection = "Интелигентно откриване на игри (Фон)";
                    TxtAiDetectionDesc = "Открива нови игри във фонов режим чрез системни ресурси и пита за добавяне в библиотеката.";
                    TxtGpuThreshold = "Праг на използване на GPU (стъпки от 5%)";
                    TxtCpuThreshold = "Праг на използване на CPU (стъпки от 5%)";
                    TxtBack = "Назад";
                    TxtSaveSettings = "Затвори / Назад";
                    TxtResetSettings = "Нулиране на настройките";
                    TxtAddGameTitle = "Добавяне на игра и приложение";
                    TxtRunningAppsTitle = "Работещи приложения и прозорци";
                    TxtManualAddTitle = "Ръчно добавяне";
                    TxtPlatformLaunchers = "Платформи";
                    TxtPlatformLaunchersSubtitle = "Стартирайте официалните клиенти за игри директно.";
                    TxtBackToLibrary = "Обратно към библиотеката";
                    TxtRefresh = "Опресни";
                    TxtBrowseExe = "Избор на .exe / пряк път";
                    TxtFilePath = "ПЪТ ДО ФАЙЛА";
                    TxtLaunchArgsOptional = "ПАРАМЕТРИ ЗА СТАРТИРАНЕ (ОПЦИОНАЛНО)";
                    TxtAddToLibraryBtn = "Добави към библиотеката";
                    TxtAiPromptTitle = "Открита е нова игра";
                    TxtYesAdd = "Да, добави";
                    TxtNoDismiss = "Не, пропусни";
                    TxtLaunch = "Старт";
                    TxtDeveloperCredits = "Разработчик и авторски права";
                    TxtVisitGitHub = "Отвори GitHub профил";
                    CurrentSectionTitle = "Всички игри";
                    CurrentSectionSubtitle = "Списък с откритите игри във вашата система.";
                    ShowToastNotification("🇧🇬 Езикът е променен на български.");
                    break;

                case AppLanguage.Spanish: // İspanyolca
                    TxtSearchWatermark = "Buscar juegos o plataformas...";
                    TxtAllGames = "Todos los juegos";
                    TxtFavorites = "Favoritos";
                    TxtLocalGames = "Juegos locales";
                    TxtScan = "Escanear";
                    TxtAdd = "Añadir";
                    TxtSettings = "Ajustes";
                    TxtScanning = "Escaneando...";
                    TxtLastOpened = "ÚLTIMO ABIERTO";
                    TxtPlayNow = "JUGAR AHORA";
                    TxtRecentGames = "JUGADOS RECIENTEMENTE";
                    TxtSelectGame = "Selecciona un juego";
                    TxtInstalled = "Instalado";
                    TxtPlay = "Jugar";
                    TxtGameTitle = "TÍTULO DEL JUEGO";
                    TxtReadyToPlay = "Listo para jugar en tu biblioteca";
                    TxtCustomLogoMedia = "Logo personalizado y multimedia";
                    TxtChangeLogo = "Cambiar logo";
                    TxtSystemLogo = "Logo del sistema";
                    TxtOnlineLogo = "Logo en línea";
                    TxtSearchAndPickLogo = "Buscar en línea y elegir";
                    TxtLaunchArgs = "PARÁMETROS DE LANZAMIENTO (ARGS)";
                    TxtQuickLaunch = "LANZAMIENTO RÁPIDO / COMANDO DE ACCESO DIRECTO";
                    TxtFolder = "Carpeta";
                    TxtDesktopShortcut = "Acceso directo en el escritorio";
                    TxtRemove = "Eliminar";
                    TxtSave = "Guardar";
                    TxtAppSettings = "Ajustes de la aplicación";
                    TxtAppSettingsSubtitle = "Configura las preferencias del sistema y detección inteligente de juegos en segundo plano de ODZEN.";
                    TxtLanguageChange = "Selección de idioma";
                    TxtLanguageDesc = "Selecciona el idioma de la interfaz";
                    TxtUiScaleTitle = "Escalado de la interfaz y pantalla";
                    TxtUiScaleDesc = "Ajusta el tamaño de textos, tarjetas y menús según tu pantalla";
                    TxtApplyScale = "⚡ Aplicar escala de interfaz";
                    TxtShowcaseTitle = "Preferencias de vitrina y visualización";
                    TxtShowShowcaseOpt = "Mostrar sección de vitrina";
                    TxtShowShowcaseDesc = "Muestra/oculta el panel superior de último juego jugado";
                    TxtShowMusicOpt = "Mostrar botones de música";
                    TxtShowMusicDesc = "Muestra/oculta Spotify, YouTube Music, etc. en la barra superior";
                    TxtShowDiscordOpt = "Mostrar botón de Discord";
                    TxtShowDiscordDesc = "Muestra/oculta el botón de inicio rápido de Discord en la barra superior";
                    TxtSystemConfig = "Configuración del sistema";
                    TxtAutostart = "Iniciar con Windows";
                    TxtAutostartDesc = "Se ejecuta en segundo plano al iniciar Windows";
                    TxtMinimizeTray = "Minimizar a la bandeja al cerrar";
                    TxtMinimizeTrayDesc = "Al pulsar ✕ se oculta en la bandeja del sistema en lugar de cerrarse";
                    TxtAutoScan = "Escaneo automático de accesos directos";
                    TxtAutoScanDesc = "Supervisa el escritorio y el menú Inicio constantemente";
                    TxtMetin2Detect = "Detección de servidores Metin2";
                    TxtMetin2DetectDesc = "Mostrar en plataformas al detectar servidores Metin2";
                    TxtOnlineMediaConfig = "Configuración multimedia y logos en línea";
                    TxtDownloadOnlineLogos = "Descargar logos originales de Internet (HD/4K)";
                    TxtDownloadOnlineLogosDesc = "Descarga logos 4K transparentes desde Steam Store";
                    TxtRefreshAllLogos = "Actualizar todos los logos en línea";
                    TxtRefreshAllLogosDesc = "Descarga de nuevo los logos más recientes para todos los juegos";
                    TxtRefreshLogosBtn = "Actualizar todos";
                    TxtDownloadMissingLogos = "Descargar solo faltantes";
                    TxtOpenLogoFolder = "Abrir carpeta";
                    TxtClearLogoCache = "Limpiar caché";
                    TxtSourcesTitle = "Fuentes de búsqueda:";
                    TxtSteamGridDbKeyPlaceholder = "Clave API SteamGridDB (Opcional)...";
                    TxtAiEngine = "Detección inteligente de juegos en segundo plano";
                    TxtAiDetection = "Detección inteligente de juegos (Segundo plano)";
                    TxtAiDetectionDesc = "Detecta nuevos juegos en segundo plano mediante recursos del sistema y solicita confirmación para agregarlos.";
                    TxtGpuThreshold = "Umbral de uso de GPU (pasos del 5%)";
                    TxtCpuThreshold = "Umbral de uso de CPU (pasos del 5%)";
                    TxtBack = "Atrás";
                    TxtSaveSettings = "Cerrar / Atrás";
                    TxtResetSettings = "Restablecer ajustes";
                    TxtAddGameTitle = "Añadir juego y aplicación";
                    TxtRunningAppsTitle = "Aplicaciones y ventanas abiertas";
                    TxtManualAddTitle = "Añadir manualmente";
                    TxtPlatformLaunchers = "Plataformas";
                    TxtPlatformLaunchersSubtitle = "Inicia clientes oficiales de juegos directamente desde tu PC.";
                    TxtBackToLibrary = "Volver a la biblioteca";
                    TxtRefresh = "Actualizar";
                    TxtBrowseExe = "Seleccionar .exe / acceso directo";
                    TxtFilePath = "RUTA DEL ARCHIVO";
                    TxtLaunchArgsOptional = "PARÁMETROS DE LANZAMIENTO (OPCIONAL)";
                    TxtAddToLibraryBtn = "Añadir a la biblioteca";
                    TxtAiPromptTitle = "Nuevo juego detectado";
                    TxtYesAdd = "Sí, añadir";
                    TxtNoDismiss = "No, omitir";
                    TxtLaunch = "Iniciar";
                    TxtDeveloperCredits = "Desarrollador y créditos";
                    TxtVisitGitHub = "Abrir perfil de GitHub";
                    CurrentSectionTitle = "Todos los juegos";
                    CurrentSectionSubtitle = "Juegos detectados en tu sistema y bibliotecas.";
                    ShowToastNotification("🇪🇸 Idioma cambiado a Español.");
                    break;

                case AppLanguage.Dutch: // Hollandaca
                    TxtSearchWatermark = "Zoek games of platforms...";
                    TxtAllGames = "Alle games";
                    TxtFavorites = "Favorieten";
                    TxtLocalGames = "Lokale games";
                    TxtScan = "Scannen";
                    TxtAdd = "Toevoegen";
                    TxtSettings = "Instellingen";
                    TxtScanning = "Scannen...";
                    TxtLastOpened = "LAATST GEOPEND";
                    TxtPlayNow = "NU SPELEN";
                    TxtRecentGames = "RECENT GESPEELD";
                    TxtSelectGame = "Selecteer een game";
                    TxtInstalled = "Geïnstalleerd";
                    TxtPlay = "Spelen";
                    TxtGameTitle = "GAMETITEL";
                    TxtReadyToPlay = "Klaar om te spelen in je bibliotheek";
                    TxtCustomLogoMedia = "Aangepast logo & media";
                    TxtChangeLogo = "Logo wijzigen";
                    TxtSystemLogo = "Systeemlogo";
                    TxtOnlineLogo = "Online logo";
                    TxtSearchAndPickLogo = "Online zoeken & kiezen";
                    TxtLaunchArgs = "OPSTARTPARAMETERS (ARGS)";
                    TxtQuickLaunch = "SNELSTART / SNELKOPPELINGSOPDRACHT";
                    TxtFolder = "Map";
                    TxtDesktopShortcut = "Bureaubladsnelkoppeling";
                    TxtRemove = "Verwijderen";
                    TxtSave = "Opslaan";
                    TxtAppSettings = "Applicatie-instellingen";
                    TxtAppSettingsSubtitle = "Configureer ODZEN systeemvoorkeuren en slimme achtergrond gamedetectie.";
                    TxtLanguageChange = "Taalkeuze";
                    TxtLanguageDesc = "Selecteer de weergavetaal van de app";
                    TxtUiScaleTitle = "Interface- & Weergaveschaling";
                    TxtUiScaleDesc = "Pas de grootte van alle teksten, kaarten en menu's aan";
                    TxtApplyScale = "⚡ Schaling toepassen";
                    TxtShowcaseTitle = "Showcase- & Weergavevoorkeuren";
                    TxtShowShowcaseOpt = "Showcase-sectie tonen";
                    TxtShowShowcaseDesc = "Toont/verbergt het grote showcase-paneel op de startpagina";
                    TxtShowMusicOpt = "Muziekknoppen weergeven";
                    TxtShowMusicDesc = "Toont/verbergt Spotify, YouTube Music enz. in de bovenste balk";
                    TxtShowDiscordOpt = "Discord-knop weergeven";
                    TxtShowDiscordDesc = "Toont/verbergt de Discord-snelstartknop in de bovenste balk";
                    TxtSystemConfig = "Systeemconfiguratie";
                    TxtAutostart = "Starten met Windows";
                    TxtAutostartDesc = "Draait stil op de achtergrond bij het opstarten van Windows";
                    TxtMinimizeTray = "Minimaliseren naar systeemvak bij sluiten";
                    TxtMinimizeTrayDesc = "Verbergt in het systeemvak bij ✕ in plaats van af te sluiten";
                    TxtAutoScan = "Automatische snelkoppelingsscan";
                    TxtAutoScanDesc = "Bewaakt bureaublad en startmenu continu";
                    TxtMetin2Detect = "Metin2 serverdetectie";
                    TxtMetin2DetectDesc = "Tonen in platformen wanneer servers worden gedetecteerd";
                    TxtOnlineMediaConfig = "Online media & logo-configuratie";
                    TxtDownloadOnlineLogos = "Originele logo's downloaden van internet (HD/4K)";
                    TxtDownloadOnlineLogosDesc = "Downloadt transparante 4K-logo's van Steam Store";
                    TxtRefreshAllLogos = "Alle logo's online vernieuwen";
                    TxtRefreshAllLogosDesc = "Downloadt opnieuw de nieuwste logo's voor alle games";
                    TxtRefreshLogosBtn = "Alles vernieuwen";
                    TxtDownloadMissingLogos = "Alleen ontbrekende";
                    TxtOpenLogoFolder = "Map openen";
                    TxtClearLogoCache = "Cache wissen";
                    TxtSourcesTitle = "Zoekbronnen:";
                    TxtSteamGridDbKeyPlaceholder = "SteamGridDB API-sleutel (Optioneel)...";
                    TxtAiEngine = "Slimme achtergrond gamedetectie";
                    TxtAiDetection = "Slimme gamedetectie (Achtergrond)";
                    TxtAiDetectionDesc = "Detecteert nieuwe games op de achtergrond via systeembronnen en vraagt bevestiging om toe te voegen.";
                    TxtGpuThreshold = "GPU-gebruiksdrempel (stappen van 5%)";
                    TxtCpuThreshold = "CPU-gebruiksdrempel (stappen van 5%)";
                    TxtBack = "Terug";
                    TxtSaveSettings = "Sluiten / Terug";
                    TxtResetSettings = "Standaardinstellingen herstellen";
                    TxtAddGameTitle = "Game & App toevoegen";
                    TxtRunningAppsTitle = "Actieve applicaties & vensters";
                    TxtManualAddTitle = "Handmatig toevoegen";
                    TxtPlatformLaunchers = "Platformen";
                    TxtPlatformLaunchersSubtitle = "Start officiële gameclients rechtstreeks vanaf je pc.";
                    TxtBackToLibrary = "Terug naar bibliotheek";
                    TxtRefresh = "Vernieuwen";
                    TxtBrowseExe = "Selecteer .exe / snelkoppeling";
                    TxtFilePath = "BESTANDSPAD";
                    TxtLaunchArgsOptional = "OPSTARTPARAMETERS (OPTIONEEL)";
                    TxtAddToLibraryBtn = "Toevoegen aan bibliotheek";
                    TxtAiPromptTitle = "Nieuwe game gestart";
                    TxtYesAdd = "Ja, toevoegen";
                    TxtNoDismiss = "Nee, overslaan";
                    TxtLaunch = "Starten";
                    TxtDeveloperCredits = "Ontwikkelaar & Credits";
                    TxtVisitGitHub = "GitHub-profiel openen";
                    CurrentSectionTitle = "Alle games";
                    CurrentSectionSubtitle = "Gedetecteerde games in je systeem en bibliotheken.";
                    ShowToastNotification("🇳🇱 Taal gewijzigd naar Nederlands.");
                    break;

                case AppLanguage.French: // Fransızca
                    TxtSearchWatermark = "Rechercher des jeux ou des plateformes...";
                    TxtAllGames = "Tous les jeux";
                    TxtFavorites = "Favoris";
                    TxtLocalGames = "Jeux locaux";
                    TxtScan = "Analyser";
                    TxtAdd = "Ajouter";
                    TxtSettings = "Paramètres";
                    TxtScanning = "Analyse en cours...";
                    TxtLastOpened = "DERNIER OUVERT";
                    TxtPlayNow = "JOUER MAINTENANT";
                    TxtRecentGames = "JOUÉS RÉCEMMENT";
                    TxtSelectGame = "Sélectionner un jeu";
                    TxtInstalled = "Installé";
                    TxtPlay = "Jouer";
                    TxtGameTitle = "TITRE DU JEU";
                    TxtReadyToPlay = "Prêt à jouer dans votre bibliothèque";
                    TxtCustomLogoMedia = "Logo et médias personnalisés";
                    TxtChangeLogo = "Changer de logo";
                    TxtSystemLogo = "Logo système";
                    TxtOnlineLogo = "Logo en ligne";
                    TxtSearchAndPickLogo = "Rechercher et choisir";
                    TxtLaunchArgs = "PARAMÈTRES DE LANCEMENT (ARGS)";
                    TxtQuickLaunch = "LANCEMENT RAPIDE / COMMANDE DE RACCOURCI";
                    TxtFolder = "Dossier";
                    TxtDesktopShortcut = "Raccourci sur le bureau";
                    TxtRemove = "Supprimer";
                    TxtSave = "Enregistrer";
                    TxtAppSettings = "Paramètres de l'application";
                    TxtAppSettingsSubtitle = "Configurez les préférences système et la détection intelligente de jeux en arrière-plan d'ODZEN.";
                    TxtLanguageChange = "Choix de la langue";
                    TxtLanguageDesc = "Sélectionnez la langue de l'interface";
                    TxtUiScaleTitle = "Mise à l'échelle de l'interface";
                    TxtUiScaleDesc = "Ajustez la taille des textes, cartes et menus selon votre écran";
                    TxtApplyScale = "⚡ Appliquer l'échelle";
                    TxtShowcaseTitle = "Préférences de vitrine et d'affichage";
                    TxtShowShowcaseOpt = "Afficher la vitrine";
                    TxtShowShowcaseDesc = "Affiche/masque le grand panneau de jeu récent sur l'accueil";
                    TxtShowMusicOpt = "Afficher les boutons de musique";
                    TxtShowMusicDesc = "Affiche/masque Spotify, YouTube Music, etc. dans la barre supérieure";
                    TxtShowDiscordOpt = "Afficher le bouton Discord";
                    TxtShowDiscordDesc = "Affiche/masque le bouton de lancement rapide de Discord dans la barre supérieure";
                    TxtSystemConfig = "Configuration système";
                    TxtAutostart = "Démarrer avec Windows";
                    TxtAutostartDesc = "S'exécute discrètement au démarrage de Windows";
                    TxtMinimizeTray = "Réduire dans la zone de notification";
                    TxtMinimizeTrayDesc = "Se masque dans la zone de notification au clic sur ✕";
                    TxtAutoScan = "Analyse automatique des raccourcis";
                    TxtAutoScanDesc = "Surveille le bureau et le menu Démarrer en continu";
                    TxtMetin2Detect = "Détection des serveurs Metin2";
                    TxtMetin2DetectDesc = "Afficher dans les plateformes si des serveurs sont détectés";
                    TxtOnlineMediaConfig = "Médias et logos en ligne";
                    TxtDownloadOnlineLogos = "Télécharger les logos originaux (HD/4K)";
                    TxtDownloadOnlineLogosDesc = "Télécharge des logos 4K transparents depuis Steam Store";
                    TxtRefreshAllLogos = "Actualiser tous les logos en ligne";
                    TxtRefreshAllLogosDesc = "Télécharge à nouveau les derniers logos pour tous les jeux";
                    TxtRefreshLogosBtn = "Tout actualiser";
                    TxtDownloadMissingLogos = "Télécharger manquants";
                    TxtOpenLogoFolder = "Ouvrir dossier";
                    TxtClearLogoCache = "Vider le cache";
                    TxtSourcesTitle = "Sources de recherche :";
                    TxtSteamGridDbKeyPlaceholder = "Clé API SteamGridDB (Optionnel)...";
                    TxtAiEngine = "Détection intelligente de jeux en arrière-plan";
                    TxtAiDetection = "Détection intelligente de jeux (Arrière-plan)";
                    TxtAiDetectionDesc = "Détecte les nouveaux jeux en arrière-plan via les ressources système et demande confirmation.";
                    TxtGpuThreshold = "Seuil d'utilisation du GPU (par 5%)";
                    TxtCpuThreshold = "Seuil d'utilisation du CPU (par 5%)";
                    TxtBack = "Retour";
                    TxtSaveSettings = "Fermer / Retour";
                    TxtResetSettings = "Réinitialiser les paramètres";
                    TxtAddGameTitle = "Ajouter un jeu ou une application";
                    TxtRunningAppsTitle = "Applications & fenêtres actives";
                    TxtManualAddTitle = "Ajout manuel";
                    TxtPlatformLaunchers = "Plateformes";
                    TxtPlatformLaunchersSubtitle = "Lancez directement les clients de jeux officiels de votre PC.";
                    TxtBackToLibrary = "Retour à la bibliothèque";
                    TxtRefresh = "Actualiser";
                    TxtBrowseExe = "Sélectionner un .exe / raccourci";
                    TxtFilePath = "CHEMIN DU FICHIER";
                    TxtLaunchArgsOptional = "PARAMÈTRES DE LANCEMENT (OPTIONNEL)";
                    TxtAddToLibraryBtn = "Ajouter à la bibliothèque";
                    TxtAiPromptTitle = "Nouveau jeu détecté";
                    TxtYesAdd = "Oui, ajouter";
                    TxtNoDismiss = "Non, ignorer";
                    TxtLaunch = "Lancer";
                    TxtDeveloperCredits = "Développeur & Crédits";
                    TxtVisitGitHub = "Ouvrir le profil GitHub";
                    CurrentSectionTitle = "Tous les jeux";
                    CurrentSectionSubtitle = "Jeux détectés sur votre système et vos bibliothèques.";
                    ShowToastNotification("🇫🇷 Langue changée en Français.");
                    break;

                case AppLanguage.Russian: // Rusça
                    TxtSearchWatermark = "Поиск игр или платформ...";
                    TxtAllGames = "Все игры";
                    TxtFavorites = "Избранное";
                    TxtLocalGames = "Локальные игры";
                    TxtScan = "Сканировать";
                    TxtAdd = "Добавить";
                    TxtSettings = "Настройки";
                    TxtScanning = "Сканирование...";
                    TxtLastOpened = "ПОСЛЕДНЯЯ ЗАПУЩЕННАЯ";
                    TxtPlayNow = "ИГРАТЬ СЕЙЧАС";
                    TxtRecentGames = "НЕДАВНО ИГРАВШИЕСЯ";
                    TxtSelectGame = "Выберите игру";
                    TxtInstalled = "Установлено";
                    TxtPlay = "Играть";
                    TxtGameTitle = "НАЗВАНИЕ ИГРЫ";
                    TxtReadyToPlay = "Готово к запуску в вашей библиотеке";
                    TxtCustomLogoMedia = "Пользовательский логотип и медиа";
                    TxtChangeLogo = "Изменить логотип";
                    TxtSystemLogo = "Системный логотип";
                    TxtOnlineLogo = "Онлайн-логотип";
                    TxtSearchAndPickLogo = "Искать онлайн и выбрать";
                    TxtLaunchArgs = "ПАРАМЕТРЫ ЗАПУСКА (ARGS)";
                    TxtQuickLaunch = "БЫСТРЫЙ ЗАПУСК / КОМАНДА ЯРЛЫКА";
                    TxtFolder = "Папка";
                    TxtDesktopShortcut = "Ярлык на рабочем столе";
                    TxtRemove = "Удалить";
                    TxtSave = "Сохранить";
                    TxtAppSettings = "Настройки приложения";
                    TxtAppSettingsSubtitle = "Настройте параметры системы ODZEN и умное фоновое обнаружение игр.";
                    TxtLanguageChange = "Выбор языка";
                    TxtLanguageDesc = "Выберите язык интерфейса приложения";
                    TxtUiScaleTitle = "Масштабирование интерфейса";
                    TxtUiScaleDesc = "Настройте размер всех карточек, текстов и меню под ваш экран";
                    TxtApplyScale = "⚡ Применить масштаб";
                    TxtShowcaseTitle = "Настройки витрины и отображения";
                    TxtShowShowcaseOpt = "Показывать блок витрины";
                    TxtShowShowcaseDesc = "Показывает/скрывает верхнюю панель последней запущенной игры";
                    TxtShowMusicOpt = "Показывать кнопки музыки";
                    TxtShowMusicDesc = "Показывает/скрывает Spotify, YouTube Music и др. в верхней панели";
                    TxtShowDiscordOpt = "Показывать кнопку Discord";
                    TxtShowDiscordDesc = "Показывает/скрывает кнопку быстрого запуска Discord в верхней панели";
                    TxtSystemConfig = "Конфигурация системы";
                    TxtAutostart = "Автозапуск с Windows";
                    TxtAutostartDesc = "Работает тихо в фоновом режиме при запуске Windows";
                    TxtMinimizeTray = "Сворачивать в трей при закрытии";
                    TxtMinimizeTrayDesc = "Скрывать в системный трей при нажатии ✕ вместо закрытия";
                    TxtAutoScan = "Автосканирование ярлыков";
                    TxtAutoScanDesc = "Непрерывно отслеживает рабочий стол и меню Пуск";
                    TxtMetin2Detect = "Обнаружение серверов Metin2";
                    TxtMetin2DetectDesc = "Показывать в списке платформ при обнаружении серверов";
                    TxtOnlineMediaConfig = "Онлайн-медиа и логотипы";
                    TxtDownloadOnlineLogos = "Скачивать оригинальные логотипы (HD/4K)";
                    TxtDownloadOnlineLogosDesc = "Загружает прозрачные 4K логотипы из Steam Store";
                    TxtRefreshAllLogos = "Обновить все логотипы онлайн";
                    TxtRefreshAllLogosDesc = "Заново загружает актуальные логотипы для всех игр библиотеки";
                    TxtRefreshLogosBtn = "Обновить все";
                    TxtDownloadMissingLogos = "Только недостающие";
                    TxtOpenLogoFolder = "Открыть папку";
                    TxtClearLogoCache = "Очистить кэш";
                    TxtSourcesTitle = "Источники поиска:";
                    TxtSteamGridDbKeyPlaceholder = "API ключ SteamGridDB (Необязательно)...";
                    TxtAiEngine = "Умное фоновое обнаружение игр";
                    TxtAiDetection = "Умное обнаружение игр (Фоновое)";
                    TxtAiDetectionDesc = "Определяет новые игры в фоновом режиме по системным ресурсам и запрашивает подтверждение.";
                    TxtGpuThreshold = "Порог использования GPU (шаг 5%)";
                    TxtCpuThreshold = "Порог использования CPU (шаг 5%)";
                    TxtBack = "Назад";
                    TxtSaveSettings = "Закрыть / Назад";
                    TxtResetSettings = "Сбросить настройки";
                    TxtAddGameTitle = "Добавить игру и приложение";
                    TxtRunningAppsTitle = "Запущенные приложения и окна";
                    TxtManualAddTitle = "Добавить вручную";
                    TxtPlatformLaunchers = "Платформы";
                    TxtPlatformLaunchersSubtitle = "Запускайте официальные игровые клиенты прямо с ПК.";
                    TxtBackToLibrary = "Вернуться в библиотеку";
                    TxtRefresh = "Обновить";
                    TxtBrowseExe = "Выбрать .exe / ярлык";
                    TxtFilePath = "ПУТЬ К ФАЙЛУ";
                    TxtLaunchArgsOptional = "ПАРАМЕТРЫ ЗАПУСКА (ОПЦИОНАЛЬНО)";
                    TxtAddToLibraryBtn = "Добавить в библиотеку";
                    TxtAiPromptTitle = "Обнаружена новая игра";
                    TxtYesAdd = "Да, добавить";
                    TxtNoDismiss = "Нет, пропустить";
                    TxtLaunch = "Запуск";
                    TxtDeveloperCredits = "Разработчик и авторские права";
                    TxtVisitGitHub = "Открыть профиль GitHub";
                    CurrentSectionTitle = "Все игры";
                    CurrentSectionSubtitle = "Игры, найденные в вашей системе и библиотеках.";
                    ShowToastNotification("🇷🇺 Язык изменен на Русский.");
                    break;

                case AppLanguage.English: // İngilizce
                    TxtSearchWatermark = "Search games or platforms...";
                    TxtAllGames = "All Games";
                    TxtFavorites = "Favorites";
                    TxtLocalGames = "Local Games";
                    TxtScan = "Scan";
                    TxtAdd = "Add";
                    TxtSettings = "Settings";
                    TxtScanning = "Scanning...";
                    TxtLastOpened = "LAST OPENED";
                    TxtPlayNow = "PLAY NOW";
                    TxtRecentGames = "RECENT GAMES";
                    TxtSelectGame = "Select a Game";
                    TxtInstalled = "Installed";
                    TxtPlay = "Play";
                    TxtGameTitle = "GAME TITLE";
                    TxtReadyToPlay = "Ready to play in your library";
                    TxtCustomLogoMedia = "Custom Game Logo & Media";
                    TxtChangeLogo = "Change Logo";
                    TxtSystemLogo = "System Logo";
                    TxtOnlineLogo = "Online Logo";
                    TxtSearchAndPickLogo = "Search Online & Pick";
                    TxtLaunchArgs = "LAUNCH ARGUMENTS (ARGS)";
                    TxtQuickLaunch = "QUICK LAUNCH / SHORTCUT COMMAND";
                    TxtFolder = "Folder";
                    TxtDesktopShortcut = "Desktop Shortcut";
                    TxtRemove = "Remove";
                    TxtSave = "Save";
                    TxtAppSettings = "Application Settings";
                    TxtAppSettingsSubtitle = "Configure ODZEN system preferences and background game detection.";
                    TxtLanguageChange = "Language Selection";
                    TxtLanguageDesc = "Select application display language";
                    TxtUiScaleTitle = "UI & Display Scaling";
                    TxtUiScaleDesc = "Adjust interface size for your display";
                    TxtApplyScale = "⚡ Apply UI Scale";
                    TxtShowcaseTitle = "Showcase & Display Preferences";
                    TxtShowShowcaseOpt = "Show Showcase Section";
                    TxtShowShowcaseDesc = "Shows/hides the top large showcase panel on the main page";
                    TxtShowMusicOpt = "Show Music Buttons";
                    TxtShowMusicDesc = "Shows/hides Spotify, YouTube Music, Apple Music etc. in top bar";
                    TxtShowDiscordOpt = "Show Discord Button";
                    TxtShowDiscordDesc = "Shows/hides Discord quick launch button in top bar";
                    TxtSystemConfig = "System Configuration";
                    TxtAutostart = "Start with Windows";
                    TxtAutostartDesc = "Runs silently in background on Windows startup (Registry Active)";
                    TxtMinimizeTray = "Minimize to System Tray on Close";
                    TxtMinimizeTrayDesc = "Minimizes application to tray instead of closing on ✕ button";
                    TxtAutoScan = "Automatic Shortcut Scanning";
                    TxtAutoScanDesc = "Continuously monitors Desktop and Start Menu";
                    TxtMetin2Detect = "Metin2 Server Detection";
                    TxtMetin2DetectDesc = "Show in platforms list when Metin2 servers are detected";
                    TxtOnlineMediaConfig = "Online Game Media & Logo Configuration";
                    TxtDownloadOnlineLogos = "Download Original Logos from Internet (HD/4K)";
                    TxtDownloadOnlineLogosDesc = "Downloads transparent 4K logos from Steam Store and open media networks";
                    TxtRefreshAllLogos = "Refresh All Logos Online";
                    TxtRefreshAllLogosDesc = "Downloads the latest logos for all library games";
                    TxtRefreshLogosBtn = "Refresh All";
                    TxtDownloadMissingLogos = "Download Missing Only";
                    TxtOpenLogoFolder = "Open Folder";
                    TxtClearLogoCache = "Clear Cache";
                    TxtSourcesTitle = "Search Sources:";
                    TxtSteamGridDbKeyPlaceholder = "SteamGridDB API Key (Optional)...";
                    TxtAiEngine = "Automatic Game Detection";
                    TxtAiDetection = "Detect Running Games";
                    TxtAiDetectionDesc = "Shows a prompt when a new game starts in background to add it to your library.";
                    TxtGpuThreshold = "GPU Usage Threshold (5% Steps)";
                    TxtCpuThreshold = "CPU Usage Threshold (5% Steps)";
                    TxtBack = "Back";
                    TxtSaveSettings = "Close / Back";
                    TxtResetSettings = "Reset to Defaults";
                    TxtAddGameTitle = "Add Game & Application";
                    TxtRunningAppsTitle = "Running Applications & Windows";
                    TxtManualAddTitle = "Manual Add";
                    TxtPlatformLaunchers = "Platforms";
                    TxtPlatformLaunchersSubtitle = "Launch official game clients directly from your PC.";
                    TxtBackToLibrary = "Back to Library";
                    TxtRefresh = "Refresh";
                    TxtBrowseExe = "Browse .exe / Shortcut";
                    TxtFilePath = "FILE PATH";
                    TxtLaunchArgsOptional = "LAUNCH ARGUMENTS (OPTIONAL)";
                    TxtAddToLibraryBtn = "Add to Library";
                    TxtAiPromptTitle = "New Game Detected";
                    TxtYesAdd = "Add to Library";
                    TxtNoDismiss = "Ignore";
                    TxtLaunch = "Launch";
                    TxtDeveloperCredits = "Developer & Credits";
                    TxtVisitGitHub = "Open GitHub Profile";
                    CurrentSectionTitle = "All Games";
                    CurrentSectionSubtitle = "Games detected in your system and libraries are listed.";
                    ShowToastNotification("🇬🇧 Language changed to English.");
                    break;

                default: // Türkçe
                    TxtSearchWatermark = "Oyunlarda veya platformlarda ara...";
                    TxtAllGames = "Tüm Oyunlar";
                    TxtFavorites = "Favoriler";
                    TxtLocalGames = "Yerel Oyunlar";
                    TxtScan = "Tara";
                    TxtAdd = "Ekle";
                    TxtSettings = "Ayarlar";
                    TxtScanning = "Taranıyor...";
                    TxtLastOpened = "EN SON AÇILAN";
                    TxtPlayNow = "HEMEN OYNA";
                    TxtRecentGames = "SON OYNANANLAR";
                    TxtSelectGame = "Oyun Seçin";
                    TxtInstalled = "Yüklü";
                    TxtPlay = "Oyna";
                    TxtGameTitle = "OYUN BAŞLIĞI";
                    TxtReadyToPlay = "Kütüphanenizde oynamaya hazır";
                    TxtCustomLogoMedia = "Özel Oyun Logosu & Medya";
                    TxtChangeLogo = "Logo Değiştir";
                    TxtSystemLogo = "Sistem Logosu";
                    TxtOnlineLogo = "Online Logo";
                    TxtSearchAndPickLogo = "Çevrimiçi Ara & Seç";
                    TxtLaunchArgs = "BAŞLATMA PARAMETRELERİ (ARGS)";
                    TxtQuickLaunch = "HIZLI BAŞLATMA / KISAYOL KOMUTU";
                    TxtFolder = "Klasör";
                    TxtDesktopShortcut = "Masaüstü Kısayolu";
                    TxtRemove = "Kaldır";
                    TxtSave = "Kaydet";
                    TxtAppSettings = "Uygulama Ayarları";
                    TxtAppSettingsSubtitle = "ODZEN sistem tercihlerini ve kütüphane davranışlarını yapılandırın.";
                    TxtLanguageChange = "Dil Değiştirme";
                    TxtLanguageDesc = "Uygulama arayüz dilini seçin";
                    TxtUiScaleTitle = "Arayüz ve Görünüm Ölçeklendirmesi";
                    TxtUiScaleDesc = "Arayüzdeki tüm kart, metin ve menülerin boyutunu ekranınıza göre ayarlayın";
                    TxtApplyScale = "⚡ Arayüz Ölçeğini Uygula";
                    TxtShowcaseTitle = "Vitrin ve Görünüm Tercihleri";
                    TxtShowShowcaseOpt = "Vitrin Bölümünü Göster";
                    TxtShowShowcaseDesc = "Ana sayfadaki en son açılan oyun vitrini panelini gösterir/gizler";
                    TxtShowMusicOpt = "Müzik Butonlarını Göster";
                    TxtShowMusicDesc = "Üst çubuktaki Spotify, YouTube Music, Apple Music vb. müzik servis butonlarını gösterir/gizler";
                    TxtShowDiscordOpt = "Discord Butonunu Göster";
                    TxtShowDiscordDesc = "Üst çubuktaki Discord hızlı başlatma butonunu gösterir/gizler";
                    TxtSystemConfig = "Sistem Yapılandırması";
                    TxtAutostart = "Windows ile Başlat";
                    TxtAutostartDesc = "Windows açılışında arka planda sessiz çalışır (Kayıt Defteri Aktif)";
                    TxtMinimizeTray = "Kapatıldığında Sistem Tepsisine Küçült";
                    TxtMinimizeTrayDesc = "Sağ üstteki ✕ butonuna basıldığında uygulamayı kapatmak yerine sistem tepsisine gizler";
                    TxtAutoScan = "Otomatik Kısayol Taraması";
                    TxtAutoScanDesc = "Masaüstü ve Başlat menüsünü sürekli izler";
                    TxtMetin2Detect = "Metin2 Sunucu Tespiti";
                    TxtMetin2DetectDesc = "Metin2 sunucuları algılandığında platformlarda göster";
                    TxtOnlineMediaConfig = "Çevrimiçi Oyun Medya ve Logo Yapılandırması";
                    TxtDownloadOnlineLogos = "İnternetten Orijinal Logoları İndir (HD/4K)";
                    TxtDownloadOnlineLogosDesc = "Steam Store ve açık medya ağlarından şeffaf 4K logoları bir kez indirip bilgisayara kaydeder";
                    TxtRefreshAllLogos = "Tüm Logoları Çevrimiçi Yenile";
                    TxtRefreshAllLogosDesc = "Kütüphanedeki tüm oyunlar için açık sunuculardan en güncel logoları sıfırdan indirir";
                    TxtRefreshLogosBtn = "Tümünü Yenile";
                    TxtDownloadMissingLogos = "Yalnızca Eksikleri İndir";
                    TxtOpenLogoFolder = "Klasörü Aç";
                    TxtClearLogoCache = "Önbelleği Temizle";
                    TxtSourcesTitle = "Arama Kaynakları:";
                    TxtSteamGridDbKeyPlaceholder = "SteamGridDB API Anahtarı (İsteğe bağlı)...";
                    TxtAiEngine = "Otomatik Oyun Algılama";
                    TxtAiDetection = "Çalışan Oyunları Algıla";
                    TxtAiDetectionDesc = "Arka planda yeni bir oyun açıldığında bildirim gösterir ve kütüphanenize tek tıkla eklemenizi sağlar.";
                    TxtGpuThreshold = "GPU Kullanım Eşiği (%5 Adımlı)";
                    TxtCpuThreshold = "İşlemci (CPU) Kullanım Eşiği (%5 Adımlı)";
                    TxtBack = "Geri Dön";
                    TxtSaveSettings = "Kapat / Geri Dön";
                    TxtResetSettings = "Ayarları Sıfırla";
                    TxtAddGameTitle = "Oyun & Uygulama Ekle";
                    TxtRunningAppsTitle = "Açık Uygulamalar & Pencereler";
                    TxtManualAddTitle = "Manuel Oyun Ekle";
                    TxtPlatformLaunchers = "Platformlar";
                    TxtPlatformLaunchersSubtitle = "Bilgisayarınızda yüklü resmi oyun istemcilerini ve mağazalarını doğrudan başlatın.";
                    TxtBackToLibrary = "Kütüphaneye Dön";
                    TxtRefresh = "Yenile";
                    TxtBrowseExe = "Bilgisayardan .exe / Kısayol Seç";
                    TxtFilePath = "DOSYA YOLU";
                    TxtLaunchArgsOptional = "BAŞLATMA PARAMETRELERİ (OPSİYONEL)";
                    TxtAddToLibraryBtn = "Kütüphaneye Ekle";
                    TxtAiPromptTitle = "Yeni Oyun Algılandı";
                    TxtYesAdd = "Kütüphaneye Ekle";
                    TxtNoDismiss = "Yoksay";
                    TxtLaunch = "Başlat";
                    TxtDeveloperCredits = "Geliştirici & Telif";
                    TxtVisitGitHub = "GitHub Profilini Aç";
                    CurrentSectionTitle = "Tüm Oyunlar";
                    CurrentSectionSubtitle = "Sistemde ve kütüphanelerde tespit edilen oyunlar listeleniyor.";
                    ShowToastNotification("🇹🇷 Dil Türkçe olarak ayarlandı.");
                    break;
            }

            SaveCurrentSettings();

            // Sistem Tepsisi (Tray Menu) dilini anında güncelle
            App.UpdateTrayLanguage(lang);
        }

        [RelayCommand]
        public void ChangeLanguage(object? param)
        {
            int index = 0;
            if (param is int i) index = i;
            else if (param is string s && int.TryParse(s, out int parsed)) index = parsed;

            SelectedLanguageIndex = index;
        }

        [RelayCommand]
        public void OpenGitHubProfile()
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/taroxzen",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        [RelayCommand]
        public void DeleteCurrentGame()
        {
            if (SelectedGame == null) return;
            var target = SelectedGame;
            IsGameDetailOpen = false;
            RemoveCustomGame(target);
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        [RelayCommand]
        public void OpenSettings()
        {
            IsSettingsOpen = true;
            IsPlatformsViewOpen = false;
        }

        [RelayCommand]
        public void BackToGames()
        {
            IsSettingsOpen = false;
            IsPlatformsViewOpen = false;
            SaveCurrentSettings();
            ShowToastNotification(SelectedLanguageIndex == 0 ? "⚙️ Ayarlar başarıyla kaydedildi." : "⚙️ Settings saved successfully.");
        }

        [RelayCommand]
        public void OpenPlatformsView()
        {
            IsPlatformsViewOpen = true;
            IsSettingsOpen = false;
        }

        [RelayCommand]
        public void ClosePlatformsView()
        {
            IsPlatformsViewOpen = false;
        }

        [RelayCommand]
        public void LaunchPlatformClient(string platformKey)
        {
            var (success, msg) = PlatformLauncherService.LaunchPlatform(platformKey);
            ShowToastNotification(msg);
        }

        // Music and External Launchers
        [RelayCommand]
        public void LaunchSpotify() => MusicService.LaunchSpotify();

        [RelayCommand]
        public void LaunchYouTubeMusic() => MusicService.LaunchYouTubeMusic();

        [RelayCommand]
        public void LaunchAppleMusic() => MusicService.LaunchAppleMusic();

        [RelayCommand]
        public void LaunchTidal() => MusicService.LaunchTidal();

        [RelayCommand]
        public void LaunchDeezer() => MusicService.LaunchDeezer();

        [RelayCommand]
        public void LaunchDiscord() => MusicService.LaunchDiscord();
    }
}
