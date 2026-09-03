// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
use std::fs;
use std::path::{Path, PathBuf};

use crate::error::Result;
use crate::model::{Game, LaunchTarget, Platform, ScanOptions};
use crate::scanners::Scanner;
use crate::util::{self, paths};

pub struct UbisoftScanner;

impl Scanner for UbisoftScanner {
    fn platform(&self) -> Platform {
        Platform::Ubisoft
    }

    fn is_available(&self) -> bool {
        launcher_dir().is_some()
            || registry_installs_key_exists()
            || settings_path().is_file()
    }

    fn scan(&self, options: &ScanOptions) -> Result<Vec<Game>> {
        let mut games = Vec::new();
        let mut seen = std::collections::HashSet::new();

        // 1) Registry Installs\{id}\InstallDir
        #[cfg(windows)]
        {
            for (id, install) in registry_installs() {
                if !is_installed_game_dir(&install) {
                    continue;
                }
                if !seen.insert(id.clone()) {
                    continue;
                }
                let name = install
                    .file_name()
                    .map(|s| s.to_string_lossy().into_owned())
                    .unwrap_or_else(|| format!("Ubisoft {id}"));
                games.push(make_game(&id, &name, install, options));
            }
        }

        // 2) Default / configured games folder
        for root in game_roots() {
            scan_games_folder(&root, options, &mut games, &mut seen);
        }

        games.sort_by(|a, b| a.name.to_lowercase().cmp(&b.name.to_lowercase()));
        Ok(games)
    }
}

fn make_game(id: &str, name: &str, install: PathBuf, options: &ScanOptions) -> Game {
    let exe = util::find_main_exe(&install);
    let mut game = Game::new(Platform::Ubisoft, id, name);
    game.install_path = Some(install.clone());
    game.executable = exe;
    game.launch = LaunchTarget::Protocol {
        uri: format!("uplay://launch/{id}/0"),
    };
    if options.compute_size {
        game.size_bytes = util::dir_size(&install);
    }
    game
}

fn is_installed_game_dir(path: &Path) -> bool {
    path.is_dir() && util::find_main_exe(path).is_some()
}

fn launcher_dir() -> Option<PathBuf> {
    let candidates = [
        paths::program_files_x86()
            .join("Ubisoft")
            .join("Ubisoft Game Launcher"),
        paths::program_files()
            .join("Ubisoft")
            .join("Ubisoft Game Launcher"),
    ];
    candidates.into_iter().find(|p| p.is_dir())
}

fn settings_path() -> PathBuf {
    paths::local_app_data()
        .join("Ubisoft Game Launcher")
        .join("settings.yaml")
}

fn registry_installs_key_exists() -> bool {
    #[cfg(windows)]
    {
        util::registry::open_subkey(
            &util::registry::hklm(),
            r"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs",
        )
        .is_some()
            || util::registry::open_subkey(
                &util::registry::hklm(),
                r"SOFTWARE\Ubisoft\Launcher\Installs",
            )
            .is_some()
    }
    #[cfg(not(windows))]
    {
        false
    }
}

#[cfg(windows)]
fn registry_installs() -> Vec<(String, PathBuf)> {
    let mut out = Vec::new();
    for base in [
        r"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs",
        r"SOFTWARE\Ubisoft\Launcher\Installs",
    ] {
        for id in util::registry::subkey_names(util::registry::Hive::LocalMachine, base) {
            let key = format!(r"{base}\{id}");
            let install = util::registry::read_string(
                util::registry::Hive::LocalMachine,
                &key,
                "InstallDir",
            )
            .or_else(|| {
                util::registry::read_string(
                    util::registry::Hive::LocalMachine,
                    &key,
                    "InstallLocation",
                )
            })
            .map(|s| PathBuf::from(s.replace('/', "\\").trim_end_matches(['\\', '/'])));
            if let Some(p) = install {
                out.push((id, p));
            }
        }
    }
    out
}

fn game_roots() -> Vec<PathBuf> {
    let mut roots = Vec::new();
    if let Some(launcher) = launcher_dir() {
        let g = launcher.join("games");
        if g.is_dir() {
            roots.push(g);
        }
    }
    // settings.yaml game_installation_path
    if let Ok(text) = fs::read_to_string(settings_path()) {
        if let Some(path) = yaml_value(&text, "game_installation_path") {
            let p = PathBuf::from(path.replace('/', "\\"));
            if p.is_dir() {
                roots.push(p);
            }
        }
    }
    roots.sort();
    roots.dedup();
    roots
}

fn scan_games_folder(
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
        if !path.is_dir() || !is_installed_game_dir(&path) {
            continue;
        }
        let name = entry.file_name().to_string_lossy().to_string();
        // Use folder name as id if not already from registry
        let id = format!("folder:{}", name.to_ascii_lowercase().replace(' ', "_"));
        if !seen.insert(id.clone()) {
            continue;
        }
        // Avoid double-listing if same path already added via registry
        if games
            .iter()
            .any(|g| g.install_path.as_ref().is_some_and(|p| p == &path))
        {
            continue;
        }
        games.push(make_game(&id, &name, path, options));
    }
}

fn yaml_value(text: &str, key: &str) -> Option<String> {
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
