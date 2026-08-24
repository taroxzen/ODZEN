// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
//! Local (Yerel) scan — installs not already covered by store scanners.
//!
//! - Level 3 (guide): Games/Oyunlar folders + engine markers
//! - Level 1 (guide): Windows Uninstall registry (game-like only)
//!
//! Platform name: `local` / Yerel.

use std::fs;
use std::path::{Path, PathBuf};

use crate::error::Result;
use crate::model::{Game, LaunchTarget, Platform, ScanOptions};
use crate::scanners::Scanner;
use crate::util::{self, paths};

pub struct LocalScanner;

/// DisplayName fragments that are almost never games.
const REJECT_NAME_SUBSTR: &[&str] = &[
    "microsoft visual c++",
    "visual studio",
    "directx",
    ".net ",
    "dotnet",
    "redistributable",
    "runtime",
    "update for",
    "security update",
    "driver",
    "device software",
    "nvidia",
    "amd ",
    "intel ",
    "realtek",
    "steam",
    "epic games launcher",
    "epic games",
    "ea app",
    "ea desktop",
    "origin",
    "battle.net",
    "blizzard",
    "ubisoft connect",
    "ubisoft game launcher",
    "gog galaxy",
    "xbox",
    "windows sdk",
    "windows software",
    "microsoft edge",
    "microsoft office",
    "office 1",
    "onedrive",
    "teams",
    "skype",
    "chrome",
    "firefox",
    "brave",
    "zen browser",
    "java ",
    "python",
    "node.js",
    "git version",
    "vc_redist",
    "vcredist",
    "microsoft .net",
    "microsoft asp",
    "microsoft natural",
    "sql server",
    "powershell",
    "windows terminal",
    "c++ build tools",
    "windows installer",
    "uninstall",
    "hotfix",
    "kb4",
    "prerequisites",
    "easy anticheat",
    "battleye",
    "denuvo",
    "gameoverlay",
    "7-zip",
    "winrar",
    "winzip",
    "vlc media",
    "filezilla",
    "bleachbit",
    "treesize",
    "proton vpn",
    "vpn",
    "webcam",
    "iriun",
    "canon ",
    "printer",
    "scanner",
    "armoury",
    "asus framework",
    "razer cortex",
    "kdrive",
    "easeus",
    "movavi",
    "video editor",
    "stremio",
    "msedgeredirect",
    "winhance",
    "wedge",
    "gameping",
    "framework service",
    "ekran el kitabı",
    "manual",
    "documentation",
    "rockstar games launcher",
    "launcher",
];

/// Drive-root / common container folder names (case-insensitive match).
const CONTAINER_NAMES: &[&str] = &[
    "games",
    "game",
    "oyunlar",
    "oyunlarım",
    "oyunlarim",
    "my games",
    "pc games",
    "oyun",
];

/// Path segments that mean "this is already a store library / system".
const SKIP_PATH_PARTS: &[&str] = &[
    "\\windows\\",
    "\\system32\\",
    "\\syswow64\\",
    "\\windowsapps\\",
    "\\xboxgames\\",
    "\\steamapps\\",
    "\\epic games\\launcher\\",
    "\\$recycle.bin",
    "\\system volume information",
    "\\programdata\\microsoft\\",
    "\\appdata\\local\\temp\\",
    "\\node_modules\\",
    "\\dotnet\\",
    "\\msbuild\\",
    "\\windows defender\\",
];

/// Engine / game markers (guide + extras).
const GAME_INDICATORS: &[&str] = &[
    "steam_api64.dll",
    "steam_api.dll",
    "unityplayer.dll",
    "gameassembly.dll",
    "bink2w64.dll",
    "binkw64.dll",
    "fmod.dll",
    "fmodstudio.dll",
    "fmodex.dll",
    "fmod_event.dll",
    "fmod_event_net.dll",
    "gameclient.dll",
    "games.server.dll",
    "unrealengine.ini",
    "ue4game.exe",
    "ue5game.exe",
    // Common standalone titles
    "gta_sa.exe",
    "gta-sa.exe",
    "gta_sa.exe",
    "multi theft auto.exe",
    "launcherapp.exe",
];

const GAME_INDICATOR_DIRS: &[&str] = &[
    "monobleedingedge",
    "engine\\binaries",
    "engine/binaries",
    "_data", // Unity often has GameName_Data
];

impl Scanner for LocalScanner {
    fn platform(&self) -> Platform {
        Platform::Local
    }

    fn is_available(&self) -> bool {
        // Folder roots and/or Windows Uninstall (always present on Windows)
        !container_roots().is_empty() || cfg!(windows)
    }

    fn scan(&self, options: &ScanOptions) -> Result<Vec<Game>> {
        let mut games = Vec::new();
        let mut seen_paths = std::collections::HashSet::new();

        // Level 3a — drive-root game folders (e.g. D:\S2) that look like installs
        scan_drive_root_games(options, &mut games, &mut seen_paths);

        // Level 3b — container folders (Games / Oyunlar / …)
        for root in container_roots() {
            scan_container(&root, options, &mut games, &mut seen_paths);
        }

        // Extra folders from UI / CLI `--folder`
        for root in &options.extra_folders {
            if !root.is_dir() {
                continue;
            }
            let _ = try_add_game(root, options, &mut games, &mut seen_paths);
            scan_container(root, options, &mut games, &mut seen_paths);
        }

        // Level 1 — Uninstall registry (game-like installs only)
        #[cfg(windows)]
        {
            scan_uninstall_registry(options, &mut games, &mut seen_paths);
        }

        games.sort_by(|a, b| a.name.to_lowercase().cmp(&b.name.to_lowercase()));
        Ok(games)
    }
}

/// Folders directly under drive roots that themselves are game installs
/// (not only children of "Games"). Example: `D:\S2`.
fn scan_drive_root_games(
    options: &ScanOptions,
    games: &mut Vec<Game>,
    seen: &mut std::collections::HashSet<String>,
) {
    for drive in paths::fixed_drive_roots() {
        let Ok(entries) = fs::read_dir(&drive) else {
            continue;
        };
        for entry in entries.flatten() {
            let path = entry.path();
            if !path.is_dir() {
                continue;
            }
            let name = entry.file_name().to_string_lossy().to_ascii_lowercase();
            // Skip container roots here — handled by scan_container
            if CONTAINER_NAMES.iter().any(|c| *c == name) {
                continue;
            }
            if path_is_skipped(&path) || drive_root_skip_name(&name) {
                continue;
            }
            try_add_game(&path, options, games, seen);
        }
    }
}

/// Drive-root names that are never single-game installs.
fn drive_root_skip_name(name: &str) -> bool {
    matches!(
        name,
        "program files"
            | "program files (x86)"
            | "programdata"
            | "users"
            | "windows"
            | "perflogs"
            | "recovery"
            | "system volume information"
            | "$recycle.bin"
            | "steamlibrary"
            | "steam"
            | "epic games"
            | "riot games"
            | "xboxgames"
            | "xbox games"
            | "windowsapps"
            | "wpsystem"
            | "wudownloadcache"
            | "documents and settings"
            | "intel"
            | "amd"
            | "nvidia"
            | "msocache"
            | "config.msi"
            | "temp"
            | "tmp"
            | "sdk"
            | "filmler"
            | "video compress"
            | "google fotoğraflar"
            | "google fotograflar"
            | "razercortexgameclips"
            | "playnite"
            | "hydra games"
            | "pang yedekleri"
            | "pang yedeklerı"
            | "temalar"
            | "and2"
            | "asus driver"
            | "anakart sürücüleri"
            | "anakart suruculeri"
            | "dpi"
            | "turkish dpi"
            | "xiaomi router"
            | "op25"
            | "v1"
            | "taner"
    ) || name.contains("driver")
        || name.contains("sürücü")
        || name.contains("surucu")
}

fn container_roots() -> Vec<PathBuf> {
    let mut roots = Vec::new();

    for drive in paths::fixed_drive_roots() {
        // Direct children named Games / Oyunlar / ...
        if let Ok(entries) = fs::read_dir(&drive) {
            for e in entries.flatten() {
                let p = e.path();
                if !p.is_dir() {
                    continue;
                }
                let name = e.file_name().to_string_lossy().to_ascii_lowercase();
                if CONTAINER_NAMES.iter().any(|c| *c == name) {
                    roots.push(p);
                }
            }
        }
        // Also X:\Games nested common
        for c in ["Games", "Oyunlar", "PC Games"] {
            let p = drive.join(c);
            if p.is_dir() {
                roots.push(p);
            }
        }
    }

    // User profile
    let home = paths::user_profile();
    for c in ["Games", "Oyunlar", "My Games"] {
        let p = home.join(c);
        if p.is_dir() {
            roots.push(p);
        }
    }
    let docs_games = paths::user_profile().join("Documents").join("My Games");
    if docs_games.is_dir() {
        roots.push(docs_games);
    }

    roots.sort();
    roots.dedup();
    roots
}

fn scan_container(
    root: &Path,
    options: &ScanOptions,
    games: &mut Vec<Game>,
    seen: &mut std::collections::HashSet<String>,
) {
    let Ok(entries) = fs::read_dir(root) else {
        return;
    };
    for entry in entries.flatten() {
        let path = entry.path();
        if !path.is_dir() {
            continue;
        }
        if path_is_skipped(&path) {
            continue;
        }

        // Direct child of Games\Title
        if try_add_game(&path, options, games, seen) {
            continue;
        }

        // One more level: Games\Publisher\Title
        if let Ok(subs) = fs::read_dir(&path) {
            for sub in subs.flatten() {
                let sp = sub.path();
                if sp.is_dir() && !path_is_skipped(&sp) {
                    try_add_game(&sp, options, games, seen);
                }
            }
        }
    }
}

fn try_add_game(
    dir: &Path,
    options: &ScanOptions,
    games: &mut Vec<Game>,
    seen: &mut std::collections::HashSet<String>,
) -> bool {
    if !looks_like_game_dir(dir) {
        return false;
    }
    let key = normalize_path(dir);
    if !seen.insert(key.clone()) {
        return false;
    }

    let Some(exe) = util::find_main_exe(dir) else {
        return false;
    };

    let name = dir
        .file_name()
        .map(|s| s.to_string_lossy().into_owned())
        .unwrap_or_else(|| "Local Game".into());

    let store_id = slug_id(dir);
    let mut game = Game::new(Platform::Local, &store_id, name);
    game.install_path = Some(dir.to_path_buf());
    game.executable = Some(exe.clone());
    game.launch = LaunchTarget::Executable {
        path: exe,
        args: vec![],
        cwd: Some(dir.to_path_buf()),
    };
    game.tags.push("yerel".into());
    if options.compute_size {
        game.size_bytes = util::dir_size(dir);
    }
    games.push(game);
    true
}

fn looks_like_game_dir(dir: &Path) -> bool {
    if !dir.is_dir() {
        return false;
    }

    // Engine file indicators in folder (depth 0–2)
    for ind in GAME_INDICATORS {
        if dir.join(ind).is_file() {
            return util::find_main_exe(dir).is_some();
        }
        // shallow search
        if let Ok(entries) = fs::read_dir(dir) {
            for e in entries.flatten() {
                let p = e.path();
                if p.is_file()
                    && p.file_name()
                        .and_then(|n| n.to_str())
                        .map(|n| n.eq_ignore_ascii_case(ind))
                        .unwrap_or(false)
                {
                    return util::find_main_exe(dir).is_some();
                }
                if p.is_dir() {
                    if p.join(ind).is_file() {
                        return util::find_main_exe(dir).is_some();
                    }
                }
            }
        }
    }

    for d in GAME_INDICATOR_DIRS {
        let p = dir.join(d.replace('/', "\\"));
        if p.is_dir() || p.is_file() {
            return util::find_main_exe(dir).is_some();
        }
        // Unity: *\_Data
        if let Ok(entries) = fs::read_dir(dir) {
            for e in entries.flatten() {
                let name = e.file_name().to_string_lossy().to_ascii_lowercase();
                if name.ends_with("_data") && e.path().is_dir() {
                    return util::find_main_exe(dir).is_some();
                }
            }
        }
    }

    // Unreal: Binaries\Win64 with large exe
    let ue = dir.join("Binaries").join("Win64");
    if ue.is_dir() {
        if util::find_main_exe(&ue).is_some() || util::find_main_exe(dir).is_some() {
            return true;
        }
    }
    let ue2 = dir.join("Engine").join("Binaries");
    if ue2.is_dir() && util::find_main_exe(dir).is_some() {
        return true;
    }

    false
}

fn path_is_skipped(path: &Path) -> bool {
    let s = format!("\\{}\\", normalize_path(path));
    SKIP_PATH_PARTS.iter().any(|p| s.contains(p))
        || {
            let name = path
                .file_name()
                .and_then(|n| n.to_str())
                .unwrap_or("")
                .to_ascii_lowercase();
            matches!(
                name.as_str(),
                "windows"
                    | "program files"
                    | "program files (x86)"
                    | "programdata"
                    | "appdata"
                    | "steam"
                    | "steamapps"
                    | "epic games"
                    | "riot games"
                    | "xboxgames"
                    | "windowsapps"
                    | "battle.net"
                    | "ea desktop"
                    | "ubisoft game launcher"
                    | "gog galaxy"
                    | "microsoft"
                    | "common files"
                    | "installer"
                    | "redist"
                    | "directx"
            )
        }
}

fn normalize_path(path: &Path) -> String {
    path.to_string_lossy()
        .to_ascii_lowercase()
        .replace('/', "\\")
        .trim_end_matches('\\')
        .to_string()
}

fn slug_id(path: &Path) -> String {
    let s = normalize_path(path);
    // stable short-ish id from path
    let mut h: u64 = 5381;
    for b in s.bytes() {
        h = h.wrapping_mul(33).wrapping_add(b as u64);
    }
    format!("{h:x}")
}

// --- Level 1: Windows Uninstall registry ---

#[cfg(windows)]
fn scan_uninstall_registry(
    options: &ScanOptions,
    games: &mut Vec<Game>,
    seen: &mut std::collections::HashSet<String>,
) {
    use util::registry::{enum_uninstall_entries, Hive};

    let paths = [
        (Hive::LocalMachine, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        (
            Hive::LocalMachine,
            r"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        ),
        (Hive::CurrentUser, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
    ];

    for (hive, reg_path) in paths {
        for e in enum_uninstall_entries(hive, reg_path) {
            let Some(display) = e.display_name.as_ref().map(|s| s.trim().to_string()) else {
                continue;
            };
            if display.is_empty() || reject_display_name(&display) {
                continue;
            }

            let Some(loc) = e.install_location.as_ref() else {
                continue;
            };
            let install = PathBuf::from(clean_reg_path(loc));
            if install.as_os_str().is_empty() || !install.is_dir() {
                continue;
            }
            if path_is_skipped(&install) {
                continue;
            }

            let key = normalize_path(&install);
            if seen.contains(&key) {
                continue;
            }

            // Strict game signal for registry (avoids 7-Zip / browsers / tools):
            // engine markers, OR install sits under a Games/Oyunlar container.
            let under_container = is_under_game_container(&install);
            let has_engine = looks_like_game_dir(&install);
            if !has_engine && !under_container {
                continue;
            }
            // Under container still needs a real main exe
            let icon_exe = e
                .display_icon
                .as_ref()
                .map(|s| PathBuf::from(clean_icon_path(s)))
                .filter(|p| {
                    p.extension()
                        .and_then(|x| x.to_str())
                        .map(|x| x.eq_ignore_ascii_case("exe"))
                        .unwrap_or(false)
                        && p.is_file()
                        && (p.starts_with(&install) || p.parent() == Some(install.as_path()))
                });

            let exe = icon_exe
                .filter(|p| p.is_file())
                .or_else(|| util::find_main_exe(&install));
            let Some(exe) = exe else {
                continue;
            };

            if !has_engine {
                // Container-only: require larger binary (skip tiny helpers)
                if let Ok(meta) = fs::metadata(&exe) {
                    if meta.len() < 200_000 {
                        continue;
                    }
                }
            }

            seen.insert(key);
            let store_id = format!("reg-{}", slug_id(&install));
            let mut game = Game::new(Platform::Local, &store_id, display);
            game.install_path = Some(install.clone());
            game.executable = Some(exe.clone());
            game.launch = LaunchTarget::Executable {
                path: exe,
                args: vec![],
                cwd: Some(install),
            };
            game.tags.push("yerel".into());
            game.tags.push("registry".into());
            if options.compute_size {
                if let Some(ref p) = game.install_path {
                    game.size_bytes = util::dir_size(p);
                }
            }
            games.push(game);
        }
    }
}

/// True if path is under a known game container (Games, Oyunlar, …).
fn is_under_game_container(path: &Path) -> bool {
    let s = normalize_path(path);
    let parts: Vec<&str> = s.split('\\').collect();
    parts.iter().any(|p| CONTAINER_NAMES.iter().any(|c| c == p))
}

fn reject_display_name(name: &str) -> bool {
    let n = name.to_ascii_lowercase();
    REJECT_NAME_SUBSTR.iter().any(|s| n.contains(s))
}

fn clean_reg_path(s: &str) -> String {
    s.trim()
        .trim_matches('"')
        .trim_end_matches(['\\', '/'])
        .replace('/', "\\")
}

fn clean_icon_path(s: &str) -> String {
    let s = s.trim().trim_matches('"');
    if let Some((path, idx)) = s.rsplit_once(',') {
        if idx.chars().all(|c| c.is_ascii_digit() || c == '-') {
            return path.trim().trim_matches('"').replace('/', "\\");
        }
    }
    s.replace('/', "\\")
}
