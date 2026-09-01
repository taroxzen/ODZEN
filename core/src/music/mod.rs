// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
use std::path::PathBuf;
use std::process::Command;
use std::time::Instant;
use serde::{Deserialize, Serialize};

#[cfg(windows)]
use crate::util::registry::{self, Hive};

/// Category of music app
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum MusicAppCategory {
    Streaming,
    LocalPlayer,
}

impl MusicAppCategory {
    pub fn display_name(&self) -> &'static str {
        match self {
            Self::Streaming => "Dijital Akış Servisi",
            Self::LocalPlayer => "Yerel Müzik Oynatıcı",
        }
    }
}

/// Dynamic action performed when user clicks an app card
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum MusicLaunchTarget {
    /// Launches local .exe
    Executable { path: PathBuf },
    /// Launches registered Windows URI protocol (e.g. spotify:)
    Protocol { uri: String },
    /// Opens fallback URL in default web browser
    WebFallback { url: String },
}

/// Single music application status and metadata
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MusicApp {
    pub id: String,
    pub name: String,
    pub category: MusicAppCategory,
    pub is_installed: bool,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub executable: Option<PathBuf>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub protocol_uri: Option<String>,
    pub web_url: String,
    pub launch: MusicLaunchTarget,
}

impl MusicApp {
    /// Launches the music app: desktop app if installed, or web fallback in default browser.
    pub fn launch(&self) -> Result<String, String> {
        match &self.launch {
            MusicLaunchTarget::Executable { path } => {
                if path.exists() {
                    Command::new(path)
                        .spawn()
                        .map_err(|e| format!("Uygulama çalıştırılamadı ({}): {}", path.display(), e))?;
                    Ok(format!("{} masaüstü uygulaması başlatıldı.", self.name))
                } else {
                    // Executable went missing -> fallback to web
                    Self::open_in_browser(&self.web_url)?;
                    Ok(format!(
                        "{} executable bulunamadı, tarayıcıda Web Player açıldı.",
                        self.name
                    ))
                }
            }
            MusicLaunchTarget::Protocol { uri } => {
                Self::open_uri_or_url(uri)?;
                Ok(format!("{} protokolu ({}) başlatıldı.", self.name, uri))
            }
            MusicLaunchTarget::WebFallback { url } => {
                Self::open_in_browser(url)?;
                Ok(format!(
                    "{} bilgisayarda bulunamadı, varsayılan tarayıcıda Web Player açıldı.",
                    self.name
                ))
            }
        }
    }

    fn open_in_browser(url: &str) -> Result<(), String> {
        Self::open_uri_or_url(url)
    }

    fn open_uri_or_url(target: &str) -> Result<(), String> {
        #[cfg(windows)]
        {
            Command::new("rundll32")
                .args(["url.dll,FileProtocolHandler", target])
                .spawn()
                .map_err(|e| format!("URL/URI açılamadı ({target}): {e}"))?;
            Ok(())
        }
        #[cfg(not(windows))]
        {
            Err("Sadece Windows desteklenmektedir.".to_string())
        }
    }
}

/// Full music scan report summary
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MusicScanReport {
    pub apps: Vec<MusicApp>,
    pub total_count: usize,
    pub installed_count: usize,
    pub web_fallback_count: usize,
    pub duration_ms: u64,
}

/// Engine to discover music applications on Windows
#[derive(Default)]
pub struct MusicScanner;

impl MusicScanner {
    pub fn new() -> Self {
        Self
    }

    /// Scans installed music apps and constructs fallback metadata
    pub fn scan(&self) -> MusicScanReport {
        let start = Instant::now();
        let definitions = get_app_definitions();
        let mut apps = Vec::with_capacity(definitions.len());

        #[cfg(windows)]
        let uninstall_entries = collect_all_uninstall_entries();

        for def in definitions {
            let mut found_exe: Option<PathBuf> = None;

            // 1. Check direct file system candidate paths
            for path in &def.candidate_paths {
                if path.is_file() {
                    found_exe = Some(path.clone());
                    break;
                }
            }

            // 2. Registry uninstall scan if not found in candidate paths
            #[cfg(windows)]
            if found_exe.is_none() {
                if let Some(path) = find_exe_in_uninstall(&def, &uninstall_entries) {
                    found_exe = Some(path);
                }
            }

            // 3. Protocol handler check if protocol uri exists
            #[cfg(windows)]
            let protocol_installed = if found_exe.is_none() && def.protocol_uri.is_some() {
                check_protocol_registered(def.protocol_uri.as_deref().unwrap())
            } else {
                false
            };

            #[cfg(not(windows))]
            let protocol_installed = false;

            let is_installed = found_exe.is_some() || protocol_installed;

            let launch = if let Some(ref exe) = found_exe {
                MusicLaunchTarget::Executable { path: exe.clone() }
            } else if protocol_installed && def.protocol_uri.is_some() {
                MusicLaunchTarget::Protocol {
                    uri: def.protocol_uri.clone().unwrap(),
                }
            } else {
                MusicLaunchTarget::WebFallback {
                    url: def.web_url.clone(),
                }
            };

            apps.push(MusicApp {
                id: def.id,
                name: def.name,
                category: def.category,
                is_installed,
                executable: found_exe,
                protocol_uri: def.protocol_uri,
                web_url: def.web_url,
                launch,
            });
        }

        let installed_count = apps.iter().filter(|a| a.is_installed).count();
        let total_count = apps.len();
        let web_fallback_count = total_count - installed_count;

        MusicScanReport {
            apps,
            total_count,
            installed_count,
            web_fallback_count,
            duration_ms: start.elapsed().as_millis() as u64,
        }
    }
}

struct MusicAppDef {
    id: String,
    name: String,
    category: MusicAppCategory,
    web_url: String,
    protocol_uri: Option<String>,
    candidate_paths: Vec<PathBuf>,
    registry_keywords: Vec<String>,
}

fn get_app_definitions() -> Vec<MusicAppDef> {
    let appdata = std::env::var("APPDATA").ok().map(PathBuf::from);
    let localappdata = std::env::var("LOCALAPPDATA").ok().map(PathBuf::from);
    let programfiles = std::env::var("ProgramFiles").ok().map(PathBuf::from);
    let programfiles_x86 = std::env::var("ProgramFiles(x86)").ok().map(PathBuf::from);

    let mut defs = Vec::new();

    // 1. Spotify
    let mut spotify_paths = Vec::new();
    if let Some(ref p) = appdata {
        spotify_paths.push(p.join("Spotify\\Spotify.exe"));
    }
    if let Some(ref p) = localappdata {
        spotify_paths.push(p.join("Microsoft\\WindowsApps\\Spotify.exe"));
        spotify_paths.push(p.join("Spotify\\Spotify.exe"));
    }
    defs.push(MusicAppDef {
        id: "spotify".into(),
        name: "Spotify".into(),
        category: MusicAppCategory::Streaming,
        web_url: "https://open.spotify.com".into(),
        protocol_uri: Some("spotify:".into()),
        candidate_paths: spotify_paths,
        registry_keywords: vec!["spotify".into()],
    });

    // 2. YouTube Music
    let mut ytm_paths = Vec::new();
    if let Some(ref p) = localappdata {
        ytm_paths.push(p.join("Programs\\youtube_music\\YouTube Music.exe"));
    }
    if let Some(ref p) = appdata {
        ytm_paths.push(p.join("YouTube Music\\YouTube Music.exe"));
    }
    defs.push(MusicAppDef {
        id: "ytmusic".into(),
        name: "YouTube Music".into(),
        category: MusicAppCategory::Streaming,
        web_url: "https://music.youtube.com".into(),
        protocol_uri: None,
        candidate_paths: ytm_paths,
        registry_keywords: vec!["youtube music".into()],
    });

    // 3. Apple Music / iTunes
    let mut apple_paths = Vec::new();
    if let Some(ref p) = programfiles {
        apple_paths.push(p.join("iTunes\\iTunes.exe"));
    }
    if let Some(ref p) = programfiles_x86 {
        apple_paths.push(p.join("iTunes\\iTunes.exe"));
    }
    if let Some(ref p) = localappdata {
        apple_paths.push(p.join("Microsoft\\WindowsApps\\AppleMusic.exe"));
    }
    defs.push(MusicAppDef {
        id: "applemusic".into(),
        name: "Apple Music / iTunes".into(),
        category: MusicAppCategory::Streaming,
        web_url: "https://music.apple.com".into(),
        protocol_uri: Some("applemusic:".into()),
        candidate_paths: apple_paths,
        registry_keywords: vec!["apple music".into(), "itunes".into()],
    });

    // 4. TIDAL
    let mut tidal_paths = Vec::new();
    if let Some(ref p) = localappdata {
        tidal_paths.push(p.join("TIDAL\\TIDAL.exe"));
        tidal_paths.push(p.join("Programs\\TIDAL\\TIDAL.exe"));
    }
    if let Some(ref p) = appdata {
        tidal_paths.push(p.join("TIDAL\\TIDAL.exe"));
    }
    defs.push(MusicAppDef {
        id: "tidal".into(),
        name: "TIDAL".into(),
        category: MusicAppCategory::Streaming,
        web_url: "https://listen.tidal.com".into(),
        protocol_uri: Some("tidal:".into()),
        candidate_paths: tidal_paths,
        registry_keywords: vec!["tidal".into()],
    });

    // 5. Deezer
    let mut deezer_paths = Vec::new();
    if let Some(ref p) = localappdata {
        deezer_paths.push(p.join("Programs\\deezer-desktop\\Deezer.exe"));
    }
    if let Some(ref p) = appdata {
        deezer_paths.push(p.join("Deezer\\Deezer.exe"));
    }
    defs.push(MusicAppDef {
        id: "deezer".into(),
        name: "Deezer".into(),
        category: MusicAppCategory::Streaming,
        web_url: "https://www.deezer.com".into(),
        protocol_uri: Some("deezer:".into()),
        candidate_paths: deezer_paths,
        registry_keywords: vec!["deezer".into()],
    });

    // 6. Amazon Music
    let mut amazon_paths = Vec::new();
    if let Some(ref p) = localappdata {
        amazon_paths.push(p.join("Amazon Music\\Amazon Music.exe"));
    }
    if let Some(ref p) = programfiles {
        amazon_paths.push(p.join("Amazon\\Amazon Music\\Amazon Music.exe"));
    }
    defs.push(MusicAppDef {
        id: "amazonmusic".into(),
        name: "Amazon Music".into(),
        category: MusicAppCategory::Streaming,
        web_url: "https://music.amazon.com".into(),
        protocol_uri: None,
        candidate_paths: amazon_paths,
        registry_keywords: vec!["amazon music".into()],
    });

    // 7. SoundCloud
    let mut soundcloud_paths = Vec::new();
    if let Some(ref p) = localappdata {
        soundcloud_paths.push(p.join("Programs\\soundcloud\\SoundCloud.exe"));
    }
    defs.push(MusicAppDef {
        id: "soundcloud".into(),
        name: "SoundCloud".into(),
        category: MusicAppCategory::Streaming,
        web_url: "https://soundcloud.com".into(),
        protocol_uri: None,
        candidate_paths: soundcloud_paths,
        registry_keywords: vec!["soundcloud".into()],
    });

    // 8. Qobuz
    let mut qobuz_paths = Vec::new();
    if let Some(ref p) = localappdata {
        qobuz_paths.push(p.join("Programs\\qobuz-desktop\\Qobuz.exe"));
    }
    if let Some(ref p) = appdata {
        qobuz_paths.push(p.join("Qobuz\\Qobuz.exe"));
    }
    defs.push(MusicAppDef {
        id: "qobuz".into(),
        name: "Qobuz".into(),
        category: MusicAppCategory::Streaming,
        web_url: "https://play.qobuz.com".into(),
        protocol_uri: None,
        candidate_paths: qobuz_paths,
        registry_keywords: vec!["qobuz".into()],
    });

    // 9. Foobar2000
    let mut foobar_paths = Vec::new();
    if let Some(ref p) = programfiles {
        foobar_paths.push(p.join("foobar2000\\foobar2000.exe"));
    }
    if let Some(ref p) = programfiles_x86 {
        foobar_paths.push(p.join("foobar2000\\foobar2000.exe"));
    }
    defs.push(MusicAppDef {
        id: "foobar2000".into(),
        name: "Foobar2000".into(),
        category: MusicAppCategory::LocalPlayer,
        web_url: "https://www.foobar2000.org".into(),
        protocol_uri: None,
        candidate_paths: foobar_paths,
        registry_keywords: vec!["foobar2000".into()],
    });

    // 10. Winamp
    let mut winamp_paths = Vec::new();
    if let Some(ref p) = programfiles_x86 {
        winamp_paths.push(p.join("Winamp\\winamp.exe"));
    }
    if let Some(ref p) = programfiles {
        winamp_paths.push(p.join("Winamp\\winamp.exe"));
    }
    defs.push(MusicAppDef {
        id: "winamp".into(),
        name: "Winamp".into(),
        category: MusicAppCategory::LocalPlayer,
        web_url: "https://www.winamp.com".into(),
        protocol_uri: None,
        candidate_paths: winamp_paths,
        registry_keywords: vec!["winamp".into()],
    });

    // 11. AIMP
    let mut aimp_paths = Vec::new();
    if let Some(ref p) = programfiles {
        aimp_paths.push(p.join("AIMP\\AIMP.exe"));
    }
    if let Some(ref p) = programfiles_x86 {
        aimp_paths.push(p.join("AIMP\\AIMP.exe"));
    }
    defs.push(MusicAppDef {
        id: "aimp".into(),
        name: "AIMP".into(),
        category: MusicAppCategory::LocalPlayer,
        web_url: "https://www.aimp.ru".into(),
        protocol_uri: None,
        candidate_paths: aimp_paths,
        registry_keywords: vec!["aimp".into()],
    });

    // 12. MusicBee
    let mut musicbee_paths = Vec::new();
    if let Some(ref p) = programfiles_x86 {
        musicbee_paths.push(p.join("MusicBee\\MusicBee.exe"));
    }
    if let Some(ref p) = programfiles {
        musicbee_paths.push(p.join("MusicBee\\MusicBee.exe"));
    }
    defs.push(MusicAppDef {
        id: "musicbee".into(),
        name: "MusicBee".into(),
        category: MusicAppCategory::LocalPlayer,
        web_url: "https://getmusicbee.com".into(),
        protocol_uri: None,
        candidate_paths: musicbee_paths,
        registry_keywords: vec!["musicbee".into()],
    });

    // 13. VLC Media Player
    let mut vlc_paths = Vec::new();
    if let Some(ref p) = programfiles {
        vlc_paths.push(p.join("VideoLAN\\VLC\\vlc.exe"));
    }
    if let Some(ref p) = programfiles_x86 {
        vlc_paths.push(p.join("VideoLAN\\VLC\\vlc.exe"));
    }
    defs.push(MusicAppDef {
        id: "vlc".into(),
        name: "VLC Media Player".into(),
        category: MusicAppCategory::LocalPlayer,
        web_url: "https://www.videolan.org/vlc/".into(),
        protocol_uri: None,
        candidate_paths: vlc_paths,
        registry_keywords: vec!["vlc media player".into(), "videolan".into()],
    });

    // 14. MediaMonkey
    let mut mediamonkey_paths = Vec::new();
    if let Some(ref p) = programfiles {
        mediamonkey_paths.push(p.join("MediaMonkey\\MediaMonkey.exe"));
        mediamonkey_paths.push(p.join("MediaMonkey 5\\MediaMonkey.exe"));
    }
    if let Some(ref p) = programfiles_x86 {
        mediamonkey_paths.push(p.join("MediaMonkey\\MediaMonkey.exe"));
        mediamonkey_paths.push(p.join("MediaMonkey 5\\MediaMonkey.exe"));
    }
    defs.push(MusicAppDef {
        id: "mediamonkey".into(),
        name: "MediaMonkey".into(),
        category: MusicAppCategory::LocalPlayer,
        web_url: "https://www.mediamonkey.com".into(),
        protocol_uri: None,
        candidate_paths: mediamonkey_paths,
        registry_keywords: vec!["mediamonkey".into()],
    });

    defs
}

#[cfg(windows)]
fn collect_all_uninstall_entries() -> Vec<registry::UninstallEntry> {
    let mut entries = Vec::new();
    const PATHS: &[&str] = &[
        r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        r"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    ];
    for path in PATHS {
        entries.extend(registry::enum_uninstall_entries(Hive::CurrentUser, path));
        entries.extend(registry::enum_uninstall_entries(Hive::LocalMachine, path));
    }
    entries
}

#[cfg(windows)]
fn find_exe_in_uninstall(
    def: &MusicAppDef,
    entries: &[registry::UninstallEntry],
) -> Option<PathBuf> {
    for entry in entries {
        let name = entry.display_name.as_deref().unwrap_or("").to_lowercase();
        let publisher = entry.publisher.as_deref().unwrap_or("").to_lowercase();

        let matches_keyword = def
            .registry_keywords
            .iter()
            .any(|kw| name.contains(kw) || publisher.contains(kw));

        if matches_keyword {
            if let Some(ref loc) = entry.install_location {
                let loc_path = PathBuf::from(loc);
                if loc_path.is_dir() {
                    if let Ok(dir) = std::fs::read_dir(&loc_path) {
                        for item in dir.flatten() {
                            let p = item.path();
                            if p.is_file()
                                && p.extension().map_or(false, |ext| ext.eq_ignore_ascii_case("exe"))
                            {
                                return Some(p);
                            }
                        }
                    }
                    return Some(loc_path);
                }
            }

            if let Some(ref icon) = entry.display_icon {
                let clean_icon = icon.trim_matches('"').split(',').next().unwrap_or("");
                let icon_path = PathBuf::from(clean_icon);
                if icon_path.is_file()
                    && icon_path
                        .extension()
                        .map_or(false, |ext| ext.eq_ignore_ascii_case("exe"))
                {
                    return Some(icon_path);
                }
            }
        }
    }
    None
}

#[cfg(windows)]
fn check_protocol_registered(protocol: &str) -> bool {
    let clean = protocol.trim_end_matches(':');
    let path = format!(r"Software\Classes\{clean}");
    registry::read_string(Hive::CurrentUser, &path, "").is_some()
        || registry::read_string(Hive::LocalMachine, &path, "").is_some()
}
