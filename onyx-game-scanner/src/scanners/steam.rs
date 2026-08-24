// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
use std::fs;
use std::path::PathBuf;

use crate::error::Result;
use crate::model::{Game, LaunchTarget, Platform, ScanOptions};
use crate::scanners::Scanner;
use crate::util::{self, vdf};

pub struct SteamScanner;

impl Scanner for SteamScanner {
    fn platform(&self) -> Platform {
        Platform::Steam
    }

    fn is_available(&self) -> bool {
        steam_path().is_some()
    }

    fn scan(&self, options: &ScanOptions) -> Result<Vec<Game>> {
        let Some(steam) = steam_path() else {
            return Ok(Vec::new());
        };

        let steamapps = steam.join("steamapps");
        let mut lib_roots = Vec::new();

        let vdf_path = steamapps.join("libraryfolders.vdf");
        if vdf_path.is_file() {
            if let Ok(text) = fs::read_to_string(&vdf_path) {
                for p in vdf::library_paths(&text) {
                    let path = PathBuf::from(p);
                    // library path points at library root; steamapps is under it
                    let sa = if path.ends_with("steamapps") {
                        path
                    } else {
                        path.join("steamapps")
                    };
                    if sa.is_dir() {
                        lib_roots.push(sa);
                    }
                }
            }
        }

        if steamapps.is_dir() && !lib_roots.iter().any(|p| p == &steamapps) {
            lib_roots.insert(0, steamapps.clone());
        }

        let mut games = Vec::new();
        let mut seen_ids = std::collections::HashSet::new();

        for lib in lib_roots {
            let Ok(entries) = fs::read_dir(&lib) else {
                continue;
            };
            for entry in entries.flatten() {
                let path = entry.path();
                let name = path
                    .file_name()
                    .and_then(|n| n.to_str())
                    .unwrap_or_default();
                if !name.starts_with("appmanifest_") || !name.ends_with(".acf") {
                    continue;
                }
                let Ok(text) = fs::read_to_string(&path) else {
                    continue;
                };
                let Some((appid, title, installdir)) = vdf::parse_appmanifest(&text) else {
                    continue;
                };

                if !options.include_tools && is_likely_tool(&appid, &title) {
                    continue;
                }
                if !seen_ids.insert(appid.clone()) {
                    continue;
                }

                let install_path = lib.join("common").join(&installdir);
                if !install_path.is_dir() {
                    // Still list if manifest exists; files may be on offline drive
                    // but prefer skipping missing installs
                    continue;
                }

                let exe = util::find_main_exe(&install_path);
                let mut game = Game::new(Platform::Steam, &appid, title);
                game.install_path = Some(install_path.clone());
                game.executable = exe.clone();
                game.launch = LaunchTarget::Protocol {
                    uri: format!("steam://rungameid/{appid}"),
                };
                if options.compute_size {
                    game.size_bytes = util::dir_size(&install_path);
                }
                games.push(game);
            }
        }

        games.sort_by(|a, b| a.name.to_lowercase().cmp(&b.name.to_lowercase()));
        Ok(games)
    }
}

fn steam_path() -> Option<PathBuf> {
    #[cfg(windows)]
    {
        return util::registry::steam_install_path().filter(|p| p.is_dir());
    }
    #[cfg(not(windows))]
    {
        None
    }
}

fn is_likely_tool(appid: &str, name: &str) -> bool {
    // Common non-game / redistributable appids and name heuristics
    const TOOL_APPIDS: &[&str] = &[
        "228980", // Steamworks Common Redistributables
        "1070560",
        "1391110",
    ];
    if TOOL_APPIDS.contains(&appid) {
        return true;
    }
    let n = name.to_ascii_lowercase();
    n.contains("redistributable")
        || n.contains("steamworks common")
        || n.contains("proton ")
        || n.ends_with(" dedicated server")
        || n.contains("server dedicated")
}
