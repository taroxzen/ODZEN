// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
use std::fs;
use std::path::{Path, PathBuf};

use serde::Deserialize;
use serde_json::Value;

use crate::error::Result;
use crate::model::{Game, LaunchTarget, Platform, ScanOptions};
use crate::scanners::Scanner;
use crate::util::{self, paths};

pub struct RiotScanner;

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct RiotClientInstalls {
    #[allow(dead_code)]
    associated_client: Option<std::collections::HashMap<String, String>>,
    rc_default: Option<String>,
    rc_live: Option<String>,
}

impl Scanner for RiotScanner {
    fn platform(&self) -> Platform {
        Platform::Riot
    }

    fn is_available(&self) -> bool {
        installs_json().is_file()
            || paths::program_data().join("Riot Games").is_dir()
            || PathBuf::from(r"C:\Riot Games").is_dir()
            || PathBuf::from(r"D:\Riot Games").is_dir()
    }

    fn scan(&self, options: &ScanOptions) -> Result<Vec<Game>> {
        let mut games = Vec::new();
        let mut seen = std::collections::HashSet::new();
        let riot_client = find_riot_client_services();

        // 1) Metadata product_settings — only if install path exists on disk
        let meta_root = paths::program_data().join("Riot Games").join("Metadata");
        if meta_root.is_dir() {
            if let Ok(entries) = fs::read_dir(&meta_root) {
                for entry in entries.flatten() {
                    let dir = entry.path();
                    if !dir.is_dir() {
                        continue;
                    }
                    let folder = entry.file_name().to_string_lossy().to_string();
                    if is_non_game_product(&folder) {
                        continue;
                    }
                    // Prefer live patchline; skip pbe / game_patch noise unless path is unique
                    if folder.contains(".pbe") || folder.contains("game_patch") {
                        continue;
                    }

                    let Some((slug, patchline)) = parse_product_folder(&folder) else {
                        continue;
                    };
                    let settings = find_product_settings(&dir);
                    let install = settings
                        .as_ref()
                        .and_then(|p| read_install_path_from_yaml(p))
                        .filter(|p| is_installed_product_dir(p));

                    let Some(install_path) = install else {
                        continue;
                    };

                    let store_id = slug.clone();
                    if !seen.insert(store_id.clone()) {
                        continue;
                    }

                    let display = display_name_for(&slug, settings.as_deref());
                    games.push(make_game(
                        &store_id,
                        &display,
                        Some(install_path),
                        &slug,
                        &patchline,
                        riot_client.as_ref(),
                        options,
                    ));
                }
            }
        }

        // 2) RiotClientInstalls.json associated_client paths (must exist on disk)
        if let Some(map) = read_associated_clients() {
            for (install_key, _client) in map {
                let install_path = PathBuf::from(clean_path(&install_key));
                if !is_installed_product_dir(&install_path) {
                    continue;
                }
                let (slug, display) = slug_from_install_path(&install_path);
                if is_non_game_slug(&slug) {
                    continue;
                }
                if !seen.insert(slug.clone()) {
                    continue;
                }
                games.push(make_game(
                    &slug,
                    &display,
                    Some(install_path),
                    &slug,
                    "live",
                    riot_client.as_ref(),
                    options,
                ));
            }
        }

        // 3) Direct folder under Riot Games roots (VALORANT, etc.)
        for root in riot_game_roots() {
            let Ok(entries) = fs::read_dir(&root) else {
                continue;
            };
            for entry in entries.flatten() {
                let path = entry.path();
                if !path.is_dir() {
                    continue;
                }
                let name = entry.file_name().to_string_lossy().to_string();
                if is_non_game_folder(&name) {
                    continue;
                }

                // Prefer .../live subfolder if present
                let install = {
                    let live = path.join("live");
                    if is_installed_product_dir(&live) {
                        live
                    } else if is_installed_product_dir(&path) {
                        path.clone()
                    } else {
                        continue;
                    }
                };

                let slug = product_slug(&name);
                if is_non_game_slug(&slug) {
                    continue;
                }
                if !seen.insert(slug.clone()) {
                    continue;
                }
                let display = display_name_for(&slug, None);
                games.push(make_game(
                    &slug,
                    &display,
                    Some(install),
                    &slug,
                    "live",
                    riot_client.as_ref(),
                    options,
                ));
            }
        }

        games.sort_by(|a, b| a.name.to_lowercase().cmp(&b.name.to_lowercase()));
        Ok(games)
    }
}

fn installs_json() -> PathBuf {
    paths::program_data()
        .join("Riot Games")
        .join("RiotClientInstalls.json")
}

/// Strict: game files must exist on disk (not just metadata / .ok / uninstall leftover).
fn is_installed_product_dir(path: &Path) -> bool {
    if !path.is_dir() {
        return false;
    }
    // Never treat the shared "Riot Games" root as a single game
    if is_riot_launcher_root(path) {
        return false;
    }
    // Must have an exe somewhere shallow, or known game content markers
    if util::find_main_exe(path).is_some() {
        return true;
    }
    // VALORANT-style: VALORANT.exe one level up or Engine/ShooterGame
    for marker in [
        "VALORANT.exe",
        "LeagueClient.exe",
        "League of Legends.exe",
        "2XKO.exe",
        "Lion.exe",
    ] {
        if path.join(marker).is_file() {
            return true;
        }
    }
    for sub in ["Engine", "ShooterGame", "Game", "LIVE", "Live"] {
        let p = path.join(sub);
        if p.is_dir() && util::find_main_exe(&p).is_some() {
            return true;
        }
    }
    // Non-empty install with multiple files (weak) — avoid empty LocalAppData stubs
    let Ok(mut rd) = fs::read_dir(path) else {
        return false;
    };
    let mut count = 0;
    for _e in rd.by_ref().flatten() {
        count += 1;
        if count >= 3 {
            // Config/Data/Logs only (LocalAppData residue) is not a full install
            let names: Vec<String> = fs::read_dir(path)
                .into_iter()
                .flatten()
                .flatten()
                .filter_map(|x| x.file_name().into_string().ok())
                .map(|s| s.to_ascii_lowercase())
                .collect();
            let only_cache = names.iter().all(|n| {
                matches!(
                    n.as_str(),
                    "config" | "data" | "logs" | "httpcache" | "cache" | "crashpad"
                )
            });
            return !only_cache;
        }
    }
    false
}

fn is_non_game_product(folder: &str) -> bool {
    let f = folder.to_ascii_lowercase();
    f.starts_with("riot client")
        || f.contains("riot_client")
        || f == "riot client"
        || f.contains("vanguard")
}

fn is_non_game_folder(name: &str) -> bool {
    let n = name.to_ascii_lowercase();
    n == "riot client"
        || n == "metadata"
        || n == "riot vanguard"
        || n.contains("vanguard")
}

fn is_non_game_slug(slug: &str) -> bool {
    matches!(
        slug.to_ascii_lowercase().as_str(),
        "riot_client" | "riot client" | "client" | "vanguard"
    )
}

fn parse_product_folder(folder: &str) -> Option<(String, String)> {
    // valorant.live, lion.live, league_of_legends.live
    let lower = folder.to_ascii_lowercase();
    if lower == "riot client" {
        return None;
    }
    let parts: Vec<&str> = folder.split('.').collect();
    if parts.is_empty() {
        return None;
    }
    let slug = parts[0].to_ascii_lowercase();
    let patchline = parts.get(1).copied().unwrap_or("live").to_ascii_lowercase();
    Some((slug, patchline))
}

fn find_product_settings(dir: &Path) -> Option<PathBuf> {
    let Ok(entries) = fs::read_dir(dir) else {
        return None;
    };
    for entry in entries.flatten() {
        let name = entry.file_name().to_string_lossy().to_string();
        if name.ends_with("product_settings.yaml") || name.ends_with(".product_settings.yaml") {
            return Some(entry.path());
        }
    }
    None
}

fn read_install_path_from_yaml(path: &Path) -> Option<PathBuf> {
    let text = fs::read_to_string(path).ok()?;
    // Prefer full path only — product_install_root is often just "D:/Riot Games"
    // and would false-positive every product that shares the launcher root.
    let p = yaml_string(&text, "product_install_full_path")
        .map(|s| PathBuf::from(clean_path(&s)))?;
    if is_riot_launcher_root(&p) {
        return None;
    }
    Some(p)
}

/// True for `...\Riot Games` itself (not a game install).
fn is_riot_launcher_root(path: &Path) -> bool {
    let name = path
        .file_name()
        .and_then(|s| s.to_str())
        .unwrap_or("")
        .eq_ignore_ascii_case("Riot Games");
    name
}

fn yaml_string(text: &str, key: &str) -> Option<String> {
    for line in text.lines() {
        let line = line.trim();
        if let Some(rest) = line.strip_prefix(key) {
            let rest = rest.trim().trim_start_matches(':').trim();
            let val = rest.trim_matches('"').trim_matches('\'').trim();
            if !val.is_empty() {
                return Some(val.to_string());
            }
        }
    }
    None
}

fn display_name_for(slug: &str, settings_path: Option<&Path>) -> String {
    if let Some(p) = settings_path {
        if let Ok(text) = fs::read_to_string(p) {
            if let Some(sc) = yaml_string(&text, "shortcut_name") {
                let name = sc.trim_end_matches(".lnk").trim();
                if !name.is_empty() {
                    return name.to_string();
                }
            }
        }
    }
    match slug.to_ascii_lowercase().as_str() {
        "valorant" => "VALORANT".into(),
        "lion" | "2xko" => "2XKO".into(),
        "league_of_legends" | "lol" => "League of Legends".into(),
        "bacon" | "teamfighttactics" | "tft" => "Teamfight Tactics".into(),
        "lor" | "baconclient" => "Legends of Runeterra".into(),
        "wildrift" | "lion_mobile" => "Wild Rift".into(),
        other => title_case(other),
    }
}

fn product_slug(folder_name: &str) -> String {
    let n = folder_name.to_ascii_lowercase().replace(' ', "_");
    if n.contains("valorant") {
        "valorant".into()
    } else if n.contains("2xko") || n == "lion" {
        "lion".into()
    } else if n.contains("league") {
        "league_of_legends".into()
    } else if n.contains("tft") || n.contains("teamfight") {
        "bacon".into()
    } else {
        n
    }
}

fn slug_from_install_path(path: &Path) -> (String, String) {
    // D:\Riot Games\VALORANT\live → valorant / VALORANT
    let components: Vec<String> = path
        .components()
        .filter_map(|c| c.as_os_str().to_str().map(|s| s.to_string()))
        .collect();
    for c in components.iter().rev() {
        let lower = c.to_ascii_lowercase();
        if lower == "live" || lower == "pbe" {
            continue;
        }
        if lower.contains("valorant") {
            return ("valorant".into(), "VALORANT".into());
        }
        if lower.contains("2xko") || lower == "lion" {
            return ("lion".into(), "2XKO".into());
        }
        if lower.contains("league") {
            return ("league_of_legends".into(), "League of Legends".into());
        }
        if lower.contains("tft") || lower.contains("teamfight") {
            return ("bacon".into(), "Teamfight Tactics".into());
        }
        if lower != "riot" && lower != "games" && lower != "games" {
            let slug = product_slug(c);
            let display = display_name_for(&slug, None);
            return (slug, display);
        }
    }
    ("unknown".into(), "Riot Game".into())
}

fn make_game(
    store_id: &str,
    display: &str,
    install: Option<PathBuf>,
    product_slug: &str,
    patchline: &str,
    riot_client: Option<&PathBuf>,
    options: &ScanOptions,
) -> Game {
    let mut game = Game::new(Platform::Riot, store_id, display);
    game.install_path = install.clone();
    if let Some(ref p) = install {
        game.executable = util::find_main_exe(p);
        if options.compute_size {
            game.size_bytes = util::dir_size(p);
        }
    }
    game.launch = launch_for_product(product_slug, patchline, riot_client, install.as_deref());
    game
}

fn launch_for_product(
    product: &str,
    patchline: &str,
    riot_client: Option<&PathBuf>,
    install: Option<&Path>,
) -> LaunchTarget {
    let product_lower = product.to_ascii_lowercase();
    let product_arg = match product_lower.as_str() {
        "league_of_legends" | "lol" => "league_of_legends",
        "bacon" | "tft" | "teamfighttactics" => "bacon",
        "valorant" => "valorant",
        "lion" | "2xko" => "lion",
        other => other,
    };
    let patch = if patchline.is_empty() {
        "live"
    } else {
        patchline
    };

    if let Some(client) = riot_client {
        return LaunchTarget::Executable {
            path: client.clone(),
            args: vec![
                format!("--launch-product={product_arg}"),
                format!("--launch-patchline={patch}"),
            ],
            cwd: client.parent().map(|p| p.to_path_buf()),
        };
    }
    if let Some(install) = install {
        if let Some(exe) = util::find_main_exe(install) {
            return LaunchTarget::Executable {
                path: exe,
                args: vec![],
                cwd: Some(install.to_path_buf()),
            };
        }
    }
    LaunchTarget::Unknown
}

fn find_riot_client_services() -> Option<PathBuf> {
    if let Ok(text) = fs::read_to_string(installs_json()) {
        if let Ok(parsed) = serde_json::from_str::<RiotClientInstalls>(&text) {
            for cand in [parsed.rc_live, parsed.rc_default].into_iter().flatten() {
                let p = PathBuf::from(clean_path(&cand));
                let services = if p.ends_with("RiotClientServices.exe") {
                    p
                } else {
                    p.join("RiotClientServices.exe")
                };
                if services.is_file() {
                    return Some(services);
                }
            }
        }
    }

    let candidates = [
        PathBuf::from(r"D:\Riot Games\Riot Client\RiotClientServices.exe"),
        PathBuf::from(r"C:\Riot Games\Riot Client\RiotClientServices.exe"),
        paths::program_files()
            .join("Riot Games")
            .join("Riot Client")
            .join("RiotClientServices.exe"),
        paths::program_files_x86()
            .join("Riot Games")
            .join("Riot Client")
            .join("RiotClientServices.exe"),
    ];
    candidates.into_iter().find(|p| p.is_file())
}

fn read_associated_clients() -> Option<std::collections::HashMap<String, String>> {
    let text = fs::read_to_string(installs_json()).ok()?;
    let v: Value = serde_json::from_str(&text).ok()?;
    let map = v.get("associated_client")?.as_object()?;
    let mut out = std::collections::HashMap::new();
    for (k, val) in map {
        if let Some(s) = val.as_str() {
            out.insert(k.clone(), s.to_string());
        }
    }
    Some(out)
}

fn riot_game_roots() -> Vec<PathBuf> {
    let mut roots = vec![
        PathBuf::from(r"C:\Riot Games"),
        PathBuf::from(r"D:\Riot Games"),
        PathBuf::from(r"E:\Riot Games"),
        paths::program_files().join("Riot Games"),
        paths::program_files_x86().join("Riot Games"),
    ];
    // From associated_client parents
    if let Some(map) = read_associated_clients() {
        for (k, _) in map {
            let p = PathBuf::from(clean_path(&k));
            // .../VALORANT/live → parent of product = Riot Games
            if let Some(product_dir) = p.parent() {
                if let Some(root) = product_dir.parent() {
                    if root.is_dir() {
                        roots.push(root.to_path_buf());
                    }
                }
            }
        }
    }
    roots.retain(|p| p.is_dir());
    roots.sort();
    roots.dedup();
    roots
}

fn clean_path(s: &str) -> String {
    s.trim()
        .trim_matches('"')
        .trim_end_matches(['/', '\\'])
        .replace('/', "\\")
}

fn title_case(s: &str) -> String {
    s.split(|c: char| c == '_' || c == '-' || c == ' ')
        .filter(|p| !p.is_empty())
        .map(|p| {
            let mut c = p.chars();
            match c.next() {
                None => String::new(),
                Some(f) => f.to_uppercase().collect::<String>() + c.as_str(),
            }
        })
        .collect::<Vec<_>>()
        .join(" ")
}
