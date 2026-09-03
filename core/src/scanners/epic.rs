// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
use std::fs;
use std::path::PathBuf;

use serde::Deserialize;

use crate::error::Result;
use crate::model::{Game, LaunchTarget, Platform, ScanOptions};
use crate::scanners::Scanner;
use crate::util::{self, paths};

pub struct EpicScanner;

#[derive(Debug, Deserialize)]
#[serde(rename_all = "PascalCase")]
struct EpicItem {
    display_name: Option<String>,
    app_name: Option<String>,
    install_location: Option<String>,
    launch_executable: Option<String>,
    #[serde(default)]
    b_is_incomplete_install: Option<bool>,
    main_game_app_name: Option<String>,
}

impl Scanner for EpicScanner {
    fn platform(&self) -> Platform {
        Platform::Epic
    }

    fn is_available(&self) -> bool {
        manifests_dir().is_dir()
    }

    fn scan(&self, options: &ScanOptions) -> Result<Vec<Game>> {
        let dir = manifests_dir();
        if !dir.is_dir() {
            return Ok(Vec::new());
        }

        let mut games = Vec::new();
        let Ok(entries) = fs::read_dir(&dir) else {
            return Ok(Vec::new());
        };

        for entry in entries.flatten() {
            let path = entry.path();
            if path.extension().and_then(|e| e.to_str()) != Some("item") {
                continue;
            }
            let Ok(text) = fs::read_to_string(&path) else {
                continue;
            };
            let Ok(item) = serde_json::from_str::<EpicItem>(&text) else {
                continue;
            };

            if item.b_is_incomplete_install == Some(true) {
                continue;
            }

            // Skip sub-items / DLC / base game packages that belong to a main game
            if let Some(ref main_app) = item.main_game_app_name {
                if !main_app.is_empty() {
                    continue;
                }
            }

            let app_name = match item.app_name {
                Some(a) if !a.is_empty() => a,
                _ => continue,
            };
            let display = item
                .display_name
                .filter(|s| !s.is_empty())
                .unwrap_or_else(|| app_name.clone());

            let display_low = display.to_ascii_lowercase();

            // Skip DLC / base game container sub-entries (e.g. "GOALS Base Game", "Content")
            if display_low.contains("content")
                || display_low.ends_with(" base game")
                || display_low.contains(" dlc")
                || display_low.ends_with(" bonus")
            {
                continue;
            }

            let install = item
                .install_location
                .map(|p| PathBuf::from(p.replace('/', "\\")));

            let launch_exe = item.launch_executable.as_ref().map(|s| s.replace('/', "\\"));

            // Prefer games with a real install folder
            if let Some(ref install_path) = install {
                if !install_path.is_dir() {
                    continue;
                }
            } else {
                continue;
            }

            let install_path = install.unwrap();

            // Fully installed = declared launch .exe exists on disk.
            let exe = match launch_exe.as_ref() {
                Some(rel) if rel.to_ascii_lowercase().ends_with(".exe") => {
                    let full = install_path.join(rel);
                    if full.is_file() {
                        Some(full)
                    } else {
                        // Incomplete / cancelled install — do not list
                        continue;
                    }
                }
                Some(_) => util::find_main_exe(&install_path),
                None => util::find_main_exe(&install_path),
            };

            let Some(exe) = exe else {
                continue;
            };

            let mut game = Game::new(Platform::Epic, &app_name, display);
            game.install_path = Some(install_path.clone());
            game.executable = Some(exe);
            game.launch = LaunchTarget::Protocol {
                uri: format!(
                    "com.epicgames.launcher://apps/{}?action=launch&silent=true",
                    app_name
                ),
            };
            if options.compute_size {
                game.size_bytes = util::dir_size(&install_path);
            }
            games.push(game);
        }

        // Deduplicate multiple items sharing the exact same install folder
        let mut unique_games: Vec<Game> = Vec::new();
        let mut seen_dirs: std::collections::HashSet<String> = std::collections::HashSet::new();

        for g in games {
            let dir_key = g.install_path.as_ref().map(|p| p.to_string_lossy().to_ascii_lowercase()).unwrap_or_default();
            if !dir_key.is_empty() {
                if seen_dirs.insert(dir_key) {
                    unique_games.push(g);
                }
            } else {
                unique_games.push(g);
            }
        }

        unique_games.sort_by(|a, b| a.name.to_lowercase().cmp(&b.name.to_lowercase()));
        Ok(unique_games)
    }
}

fn manifests_dir() -> PathBuf {
    paths::program_data()
        .join("Epic")
        .join("EpicGamesLauncher")
        .join("Data")
        .join("Manifests")
}

#[allow(dead_code)]
fn launcher_installed_dat() -> PathBuf {
    paths::program_data()
        .join("Epic")
        .join("UnrealEngineLauncher")
        .join("LauncherInstalled.dat")
}


