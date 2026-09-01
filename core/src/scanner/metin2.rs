// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
//! Metin2 Private Server (Yan Sunucu) Scanner
//!
//! Taramalar, bilgisayardaki sabit sürücülerde ve özel klasörlerde Metin2 istemci
//! ve yan sunucu kurulumlarını özel dosya imzaları ile tespit eder.

use std::collections::HashSet;
use std::fs;
use std::path::{Path, PathBuf};

use crate::error::Result;
use crate::model::{Game, LaunchTarget, Platform, ScanOptions};
use crate::scanners::Scanner;
use crate::util::{self, paths};

pub struct Metin2Scanner;

/// Atlanması gereken sistem ve kütüphane klasör isimleri
const SKIP_DIR_NAMES: &[&str] = &[
    "windows",
    "program files",
    "program files (x86)",
    "programdata",
    "appdata",
    "steam",
    "steamapps",
    "epic games",
    "riot games",
    "xboxgames",
    "windowsapps",
    "node_modules",
    "$recycle.bin",
    "system volume information",
    "perflogs",
    "recovery",
];

impl Scanner for Metin2Scanner {
    fn platform(&self) -> Platform {
        Platform::Metin2
    }

    fn is_available(&self) -> bool {
        true
    }

    fn scan(&self, options: &ScanOptions) -> Result<Vec<Game>> {
        let mut games = Vec::new();
        let mut seen_paths = HashSet::new();

        // 1. Sabit sürücülerin kök dizinlerindeki doğrudan klasörleri tara (Örn: D:\RinaMT2, C:\Metin2_PvP)
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
                if SKIP_DIR_NAMES.contains(&name.as_str()) {
                    continue;
                }
                try_add_metin2_game(&path, options, &mut games, &mut seen_paths);
            }
        }

        // 2. Aday klasörleri derinlemesine tara (Masaüstü, İndirilenler, Games, Oyunlar vb.)
        for root in candidate_roots() {
            scan_folder_recursive(&root, 0, 2, options, &mut games, &mut seen_paths);
        }

        games.sort_by(|a, b| a.name.to_lowercase().cmp(&b.name.to_lowercase()));
        Ok(games)
    }
}

fn candidate_roots() -> Vec<PathBuf> {
    let mut roots = Vec::new();
    let home = paths::user_profile();

    // Kullanıcı dizini altındaki bilinen klasörler
    for c in ["Desktop", "Downloads", "Documents", "Games", "Oyunlar", "Masaüstü", "My Games"] {
        let p = home.join(c);
        if p.is_dir() {
            roots.push(p);
        }
    }

    // Disklerdeki Games / Oyunlar klasörleri
    for drive in paths::fixed_drive_roots() {
        for c in ["Games", "Oyunlar", "Metin2", "Metin 2", "Metin2_Servers", "Oyun"] {
            let p = drive.join(c);
            if p.is_dir() {
                roots.push(p);
            }
        }
    }

    roots.sort();
    roots.dedup();
    roots
}

fn scan_folder_recursive(
    dir: &Path,
    depth: usize,
    max_depth: usize,
    options: &ScanOptions,
    games: &mut Vec<Game>,
    seen: &mut HashSet<String>,
) {
    if depth > max_depth || !dir.is_dir() {
        return;
    }

    let name = dir
        .file_name()
        .unwrap_or_default()
        .to_string_lossy()
        .to_ascii_lowercase();
    if SKIP_DIR_NAMES.contains(&name.as_str()) {
        return;
    }

    if try_add_metin2_game(dir, options, games, seen) {
        return; // Metin2 istemcisi bulundu, aynı klasörün içine daha derin girilmez
    }

    if let Ok(entries) = fs::read_dir(dir) {
        for entry in entries.flatten() {
            let p = entry.path();
            if p.is_dir() {
                scan_folder_recursive(&p, depth + 1, max_depth, options, games, seen);
            }
        }
    }
}

fn try_add_metin2_game(
    dir: &Path,
    options: &ScanOptions,
    games: &mut Vec<Game>,
    seen: &mut HashSet<String>,
) -> bool {
    let score = score_metin2_dir(dir);
    if score < 50 {
        return false;
    }

    let key = normalize_path(dir);
    if !seen.insert(key) {
        return false;
    }

    let exe = find_metin2_exe(dir);
    let Some(exe) = exe else {
        return false;
    };

    let raw_name = dir
        .file_name()
        .map(|s| s.to_string_lossy().into_owned())
        .unwrap_or_else(|| "Metin2".into());

    let folder_name = clean_metin2_title(&raw_name);

    let store_id = format!("metin2-{}", slug_id(dir));
    let mut game = Game::new(Platform::Metin2, &store_id, &folder_name);
    game.install_path = Some(dir.to_path_buf());
    game.executable = Some(exe.clone());
    game.launch = LaunchTarget::Executable {
        path: exe,
        args: vec![],
        cwd: Some(dir.to_path_buf()),
    };
    game.tags.push("metin2".into());
    game.tags.push("p-server".into());
    game.tags.push("yan-sunucu".into());

    if options.compute_size {
        game.size_bytes = util::dir_size(dir);
    }

    games.push(game);
    true
}

/// Metin2 istemcisi güven puanı hesaplama (0 - 100+)
fn score_metin2_dir(dir: &Path) -> u32 {
    let mut score = 0;

    // 1. Pack klasörü kontrolü (+35 / +50 puan)
    let pack_dir = dir.join("pack");
    if pack_dir.is_dir() {
        score += 35;
        if let Ok(entries) = fs::read_dir(&pack_dir) {
            for entry in entries.flatten().take(25) {
                let name = entry.file_name().to_string_lossy().to_ascii_lowercase();
                if name.ends_with(".epk") || name.ends_with(".eix") || name.ends_with(".vfs") {
                    score += 15;
                    break;
                }
            }
        }
    }

    // 2. Metin2 özel DLL dosyaları (+20 puan)
    for dll in [
        "DEVIL.dll",
        "devil.dll",
        "IL.dll",
        "mss32.dll",
        "Mss32.dll",
        "python27.dll",
        "python22.dll",
        "python2.7.dll",
    ] {
        if dir.join(dll).is_file() {
            score += 20;
            break;
        }
    }

    // 3. Alt dizin kontrolü (miles, lib, mark, bgm) (+10 puan)
    if dir.join("miles").is_dir() || dir.join("lib").is_dir() {
        score += 10;
    }
    if dir.join("bgm").is_dir() || dir.join("mark").is_dir() {
        score += 10;
    }

    // 4. Metin2 ayar ve log dosyaları (+15 puan)
    for cfg in [
        "syserr.txt",
        "syslog.txt",
        "metin2.cfg",
        "locale.cfg",
        "config.exe",
    ] {
        if dir.join(cfg).is_file() {
            score += 15;
            break;
        }
    }

    // 5. Uygun çalıştırılabilir dosya bulunması (+25 puan)
    if find_metin2_exe(dir).is_some() {
        score += 25;
    }

    score
}

/// Metin2 sunucu klasöründeki en uygun başlatıcı (.exe) dosyasını bulur
fn find_metin2_exe(dir: &Path) -> Option<PathBuf> {
    // Öncelikli bilinen launcher / istemci isimleri
    let prefer_names = [
        "patcher.exe",
        "metin2launch.exe",
        "metin2_launch.exe",
        "autoupdater.exe",
        "launcher.exe",
        "metin2client.exe",
        "metin2.exe",
    ];

    for name in prefer_names {
        let p = dir.join(name);
        if p.is_file() {
            return Some(p);
        }
    }

    // Klasör içindeki .exe dosyaları arasında arama yap
    let folder_name = dir
        .file_name()
        .unwrap_or_default()
        .to_string_lossy()
        .to_ascii_lowercase();

    if let Ok(entries) = fs::read_dir(dir) {
        let mut candidates = Vec::new();
        for entry in entries.flatten() {
            let p = entry.path();
            if p.is_file() {
                let name = entry.file_name().to_string_lossy().to_ascii_lowercase();
                if name.ends_with(".exe") && name != "config.exe" && name != "uninstall.exe" {
                    if name.contains("metin")
                        || name.contains("patch")
                        || name.contains("launch")
                        || name.contains("mt2")
                        || (!folder_name.is_empty() && name.contains(&folder_name))
                    {
                        return Some(p);
                    }
                    candidates.push(p);
                }
            }
        }
        if let Some(first_exe) = candidates.into_iter().next() {
            return Some(first_exe);
        }
    }

    None
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
    let mut h: u64 = 5381;
    for b in s.bytes() {
        h = h.wrapping_mul(33).wrapping_add(b as u64);
    }
    format!("{h:x}")
}

fn clean_metin2_title(raw: &str) -> String {
    let mut s = raw.to_string();
    for suffix in ["_TestServer", "_testserver", "_Server", "_server", "-server", "-Server", "_PVP", "_pvp", "-PVP", "-pvp"] {
        if s.ends_with(suffix) {
            s.truncate(s.len() - suffix.len());
        }
    }
    s.trim().to_string()
}
