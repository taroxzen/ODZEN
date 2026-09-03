// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
use std::fs;
use std::path::{Path, PathBuf};

use serde_json::Value;

use crate::error::Result;
use crate::model::{Game, LaunchTarget, Platform, ScanOptions};
use crate::scanners::Scanner;
use crate::util::{self, paths};

pub struct BattleNetScanner;

/// Known Battle.net product codes → display names.
const PRODUCT_NAMES: &[(&str, &str)] = &[
    ("WoW", "World of Warcraft"),
    ("WoWC", "World of Warcraft Classic"),
    ("W3", "Warcraft III: Reforged"),
    ("W1R", "Warcraft I: Remastered"),
    ("W2R", "Warcraft II: Remastered"),
    ("D3", "Diablo III"),
    ("OSI", "Diablo II Resurrected"),
    ("Fen", "Diablo IV"),
    ("D1", "Diablo"),
    ("ANBS", "Diablo Immortal"),
    ("Pro", "Overwatch 2"),
    ("WTCG", "Hearthstone"),
    ("Hero", "Heroes of the Storm"),
    ("S1", "StarCraft"),
    ("S2", "StarCraft II"),
    ("ZEUS", "Call of Duty: Black Ops Cold War"),
    ("VIPR", "Call of Duty: Black Ops 4"),
    ("ODIN", "Call of Duty: Modern Warfare"),
    ("AUKS", "Call of Duty"),
    ("LAZR", "Call of Duty: MW2 Campaign Remastered"),
    ("FORE", "Call of Duty: Vanguard"),
    ("SPOT", "Call of Duty: Modern Warfare III"),
    ("WLBY", "Crash Bandicoot 4"),
    ("RTRO", "Blizzard Arcade Collection"),
    ("Aqua", "Avowed"),
    ("SCOR", "Sea of Thieves"),
];

impl Scanner for BattleNetScanner {
    fn platform(&self) -> Platform {
        Platform::BattleNet
    }

    fn is_available(&self) -> bool {
        battle_net_exe().is_some()
            || product_db_path().is_file()
            || paths::program_data().join("Battle.net").is_dir()
            || paths::local_app_data().join("Battle.net").is_dir()
    }

    fn scan(&self, options: &ScanOptions) -> Result<Vec<Game>> {
        let mut games = Vec::new();
        let mut seen = std::collections::HashSet::new();
        let bnet_exe = battle_net_exe();

        // 1) product.db string scrape for product codes + install dirs
        if let Some(db) = product_db_path().is_file().then(product_db_path) {
            for (code, dir) in parse_product_db_strings(&db) {
                if is_launcher_code(&code) {
                    continue;
                }
                if !is_game_install_dir(&dir) {
                    continue;
                }
                if !seen.insert(code.clone()) {
                    continue;
                }
                games.push(make_game(&code, &dir, bnet_exe.as_ref(), options));
            }
        }

        // 2) Battle.net config JSON Games section
        for cfg in config_candidates() {
            if !cfg.is_file() {
                continue;
            }
            for (code, dir_hint) in parse_battlenet_config(&cfg) {
                if is_launcher_code(&code) || !seen.insert(code.clone()) {
                    continue;
                }
                let dir = dir_hint
                    .filter(|p| is_game_install_dir(p))
                    .or_else(|| find_install_for_code(&code));
                let Some(dir) = dir else {
                    continue;
                };
                if !is_game_install_dir(&dir) {
                    continue;
                }
                games.push(make_game(&code, &dir, bnet_exe.as_ref(), options));
            }
        }

        // 3) Uninstall registry (Blizzard)
        #[cfg(windows)]
        {
            for (name, path, code_hint) in blizzard_uninstall_games() {
                if !is_game_install_dir(&path) {
                    continue;
                }
                let code = code_hint.unwrap_or_else(|| {
                    path.file_name()
                        .map(|s| s.to_string_lossy().into_owned())
                        .unwrap_or_else(|| name.clone())
                });
                if is_launcher_code(&code) {
                    continue;
                }
                let key = code.clone();
                if !seen.insert(key) {
                    // same path already listed
                    if games
                        .iter()
                        .any(|g| g.install_path.as_ref().is_some_and(|p| p == &path))
                    {
                        continue;
                    }
                }
                let mut game = make_game(&code, &path, bnet_exe.as_ref(), options);
                game.name = name;
                games.push(game);
            }
        }

        // 4) Known folder markers (.build.info)
        for (code, dir) in scan_build_info_roots() {
            if is_launcher_code(&code) || !seen.insert(code.clone()) {
                continue;
            }
            if !is_game_install_dir(&dir) {
                continue;
            }
            games.push(make_game(&code, &dir, bnet_exe.as_ref(), options));
        }

        games.sort_by(|a, b| a.name.to_lowercase().cmp(&b.name.to_lowercase()));
        Ok(games)
    }
}

fn make_game(
    code: &str,
    install: &Path,
    bnet_exe: Option<&PathBuf>,
    options: &ScanOptions,
) -> Game {
    let name = product_display_name(code, install);
    let mut game = Game::new(Platform::BattleNet, code, name);
    game.install_path = Some(install.to_path_buf());
    game.executable = util::find_main_exe(install);
    game.launch = if let Some(exe) = bnet_exe {
        LaunchTarget::Executable {
            path: exe.clone(),
            args: vec![format!("--exec=launch {code}")],
            cwd: exe.parent().map(|p| p.to_path_buf()),
        }
    } else {
        LaunchTarget::Protocol {
            uri: format!("battlenet://{code}/"),
        }
    };
    if options.compute_size {
        game.size_bytes = util::dir_size(install);
    }
    game
}

fn is_launcher_code(code: &str) -> bool {
    matches!(
        code.to_ascii_lowercase().as_str(),
        "bna" | "battle_net" | "battlenet" | "agent"
    )
}

fn is_game_install_dir(path: &Path) -> bool {
    if !path.is_dir() {
        return false;
    }
    // Skip pure Battle.net client folder
    if path.join("Battle.net.exe").is_file() && !path.join(".build.info").is_file() {
        // client install may have tiny .product.db only
        let has_game_marker = path.join(".build.info").is_file()
            || path.join("_retail_").is_dir()
            || path.join("_classic_").is_dir();
        if !has_game_marker {
            return false;
        }
    }
    path.join(".build.info").is_file()
        || path.join(".product.db").is_file() && util::find_main_exe(path).is_some()
        || util::find_main_exe(path).is_some()
            && (path.join("_retail_").is_dir()
                || path.join("Overwatch.exe").is_file()
                || path.join("Diablo IV.exe").is_file()
                || path.join("Hearthstone.exe").is_file()
                || path.join("Wow.exe").is_file()
                || path.join("WowClassic.exe").is_file())
}

fn product_display_name(code: &str, install: &Path) -> String {
    for (c, n) in PRODUCT_NAMES {
        if c.eq_ignore_ascii_case(code) {
            return (*n).to_string();
        }
    }
    install
        .file_name()
        .map(|s| s.to_string_lossy().into_owned())
        .unwrap_or_else(|| code.to_string())
}

fn product_db_path() -> PathBuf {
    paths::program_data()
        .join("Battle.net")
        .join("Agent")
        .join("product.db")
}

fn battle_net_exe() -> Option<PathBuf> {
    let candidates = [
        PathBuf::from(r"E:\Battle.net\Battle.net.exe"),
        paths::program_files_x86().join("Battle.net").join("Battle.net.exe"),
        paths::program_files().join("Battle.net").join("Battle.net.exe"),
    ];
    candidates.into_iter().find(|p| p.is_file()).or_else(|| {
        // uninstall display icon
        #[cfg(windows)]
        {
            for e in util::registry::enum_uninstall_entries(
                util::registry::Hive::LocalMachine,
                r"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
            ) {
                if e.display_name.as_deref() == Some("Battle.net") {
                    if let Some(icon) = e.display_icon {
                        let p = PathBuf::from(icon.trim_matches('"').split(',').next().unwrap_or(""));
                        if p.is_file() {
                            return Some(p);
                        }
                    }
                    if let Some(loc) = e.install_location {
                        let p = PathBuf::from(loc.trim_matches('"')).join("Battle.net.exe");
                        if p.is_file() {
                            return Some(p);
                        }
                    }
                }
            }
        }
        None
    })
}

fn config_candidates() -> Vec<PathBuf> {
    let mut out = Vec::new();
    let base = paths::local_app_data().join("Battle.net");
    if base.is_dir() {
        if let Ok(entries) = fs::read_dir(&base) {
            for e in entries.flatten() {
                let n = e.file_name().to_string_lossy().to_string();
                if n.ends_with(".config") || n == "Battle.net.config" {
                    out.push(e.path());
                }
            }
        }
    }
    out
}

/// Extract (product_code, install_dir) pairs from protobuf product.db via printable strings.
fn parse_product_db_strings(path: &Path) -> Vec<(String, PathBuf)> {
    let Ok(bytes) = fs::read(path) else {
        return Vec::new();
    };
    // Collect ASCII / UTF-8-ish runs
    let mut strings = Vec::new();
    let mut cur = Vec::new();
    for &b in &bytes {
        if (32..127).contains(&b) {
            cur.push(b);
        } else if !cur.is_empty() {
            if cur.len() >= 3 {
                if let Ok(s) = String::from_utf8(cur.clone()) {
                    strings.push(s);
                }
            }
            cur.clear();
        }
    }
    if cur.len() >= 3 {
        if let Ok(s) = String::from_utf8(cur) {
            strings.push(s);
        }
    }

    let mut dirs: Vec<PathBuf> = Vec::new();
    let mut codes: Vec<String> = Vec::new();
    for s in &strings {
        if looks_like_windows_path(s) {
            let p = PathBuf::from(s.replace('/', "\\").trim_end_matches(['\\', '/']));
            if p.is_dir() {
                dirs.push(p);
            }
        }
        // product codes are short alphanumeric (2–5 chars typically)
        if s.len() <= 5
            && s.len() >= 2
            && s.chars().all(|c| c.is_ascii_alphanumeric())
            && PRODUCT_NAMES.iter().any(|(c, _)| c.eq_ignore_ascii_case(s))
        {
            codes.push(s.clone());
        }
    }

    let mut out = Vec::new();
    // Pair codes with nearby install dirs that look like games
    for dir in dirs {
        if !is_game_install_dir(&dir) {
            continue;
        }
        let code = codes
            .iter()
            .find(|c| {
                let name = product_display_name(c, &dir).to_ascii_lowercase();
                let folder = dir
                    .file_name()
                    .map(|s| s.to_string_lossy().to_ascii_lowercase())
                    .unwrap_or_default();
                folder.contains(&name.to_ascii_lowercase().chars().take(4).collect::<String>())
                    || name.split_whitespace().any(|w| folder.contains(&w.to_ascii_lowercase()))
            })
            .cloned()
            .or_else(|| {
                // folder-based guess
                let folder = dir
                    .file_name()
                    .map(|s| s.to_string_lossy().to_string())
                    .unwrap_or_default();
                PRODUCT_NAMES
                    .iter()
                    .find(|(_, n)| {
                        folder
                            .to_ascii_lowercase()
                            .contains(&n.to_ascii_lowercase().replace(':', ""))
                            || n.to_ascii_lowercase()
                                .split_whitespace()
                                .any(|w| w.len() > 3 && folder.to_ascii_lowercase().contains(w))
                    })
                    .map(|(c, _)| (*c).to_string())
                    .or(Some(folder))
            })
            .unwrap_or_else(|| "unknown".into());
        if !is_launcher_code(&code) {
            out.push((code, dir));
        }
    }
    out
}

fn looks_like_windows_path(s: &str) -> bool {
    let s = s.trim();
    if s.len() < 6 {
        return false;
    }
    let bytes = s.as_bytes();
    // C:\ or C:/
    bytes[0].is_ascii_alphabetic()
        && bytes.get(1) == Some(&b':')
        && (bytes.get(2) == Some(&b'\\') || bytes.get(2) == Some(&b'/'))
}

fn parse_battlenet_config(path: &Path) -> Vec<(String, Option<PathBuf>)> {
    let Ok(text) = fs::read_to_string(path) else {
        return Vec::new();
    };
    let Ok(v) = serde_json::from_str::<Value>(&text) else {
        return Vec::new();
    };
    let mut out = Vec::new();
    let Some(games) = v.get("Games").and_then(|g| g.as_object()) else {
        return out;
    };
    for (key, data) in games {
        if key.eq_ignore_ascii_case("battle_net") || key.eq_ignore_ascii_case("bna") {
            continue;
        }
        // Resumable false => fully installed (per community scanners)
        let resumable = data
            .get("Resumable")
            .and_then(|x| x.as_str())
            .unwrap_or("true");
        if resumable != "false" {
            // still try if InstallPath present
            if data.get("InstallPath").is_none() && data.get("Path").is_none() {
                continue;
            }
        }
        let dir = data
            .get("InstallPath")
            .or_else(|| data.get("Path"))
            .and_then(|x| x.as_str())
            .map(|s| PathBuf::from(s.replace('/', "\\")));
        out.push((key.clone(), dir));
    }
    out
}

fn find_install_for_code(code: &str) -> Option<PathBuf> {
    let name = product_display_name(code, Path::new(""));
    for root in [
        paths::program_files_x86(),
        paths::program_files(),
        PathBuf::from(r"D:\"),
        PathBuf::from(r"E:\"),
        PathBuf::from(r"C:\Games"),
        PathBuf::from(r"D:\Games"),
        PathBuf::from(r"E:\Games"),
    ] {
        if !root.exists() {
            continue;
        }
        // direct known folder
        let candidate = root.join(&name);
        if is_game_install_dir(&candidate) {
            return Some(candidate);
        }
        if let Ok(entries) = fs::read_dir(&root) {
            for e in entries.flatten() {
                let p = e.path();
                if !p.is_dir() {
                    continue;
                }
                let folder = e.file_name().to_string_lossy().to_ascii_lowercase();
                if PRODUCT_NAMES.iter().any(|(c, n)| {
                    c.eq_ignore_ascii_case(code)
                        && (folder.contains(&n.to_ascii_lowercase().replace(':', ""))
                            || n.to_ascii_lowercase()
                                .split_whitespace()
                                .filter(|w| w.len() > 3)
                                .any(|w| folder.contains(w)))
                }) && is_game_install_dir(&p)
                {
                    return Some(p);
                }
            }
        }
    }
    None
}

#[cfg(windows)]
fn blizzard_uninstall_games() -> Vec<(String, PathBuf, Option<String>)> {
    let mut out = Vec::new();
    for (hive, path) in [
        (
            util::registry::Hive::LocalMachine,
            r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        ),
        (
            util::registry::Hive::LocalMachine,
            r"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        ),
        (
            util::registry::Hive::CurrentUser,
            r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        ),
    ] {
        for e in util::registry::enum_uninstall_entries(hive, path) {
            let pub_ = e.publisher.as_deref().unwrap_or("").to_ascii_lowercase();
            let name = e.display_name.as_deref().unwrap_or("");
            if name.eq_ignore_ascii_case("Battle.net") {
                continue;
            }
            if !(pub_.contains("blizzard") || pub_.contains("activision")) {
                continue;
            }
            let Some(loc) = e.install_location.as_ref() else {
                continue;
            };
            let p = PathBuf::from(loc.trim_matches('"').replace('/', "\\"));
            if p.is_dir() {
                out.push((name.to_string(), p, None));
            }
        }
    }
    out
}

fn scan_build_info_roots() -> Vec<(String, PathBuf)> {
    let mut out = Vec::new();
    let roots = [
        paths::program_files_x86(),
        paths::program_files(),
        PathBuf::from(r"D:\"),
        PathBuf::from(r"E:\"),
        PathBuf::from(r"D:\Games"),
        PathBuf::from(r"E:\Games"),
    ];
    for root in roots {
        if !root.is_dir() {
            continue;
        }
        let Ok(entries) = fs::read_dir(&root) else {
            continue;
        };
        for e in entries.flatten() {
            let p = e.path();
            if !p.is_dir() || !p.join(".build.info").is_file() {
                continue;
            }
            if !is_game_install_dir(&p) {
                continue;
            }
            let folder = e.file_name().to_string_lossy().to_string();
            let code = PRODUCT_NAMES
                .iter()
                .find(|(_, n)| {
                    folder
                        .to_ascii_lowercase()
                        .contains(&n.to_ascii_lowercase().chars().filter(|c| c.is_alphanumeric() || c.is_whitespace()).collect::<String>().split_whitespace().next().unwrap_or(""))
                })
                .map(|(c, _)| (*c).to_string())
                .unwrap_or(folder);
            out.push((code, p));
        }
    }
    out
}
