// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
use std::path::PathBuf;

use crate::error::Result;
use crate::model::{Game, LaunchTarget, Platform, ScanOptions};
use crate::scanners::Scanner;
use crate::util::{self, paths};

pub struct RockstarScanner;

/// Known Rockstar titles and registry key suffixes.
const KNOWN_TITLES: &[(&str, &str)] = &[
    ("Grand Theft Auto V", "Grand Theft Auto V"),
    ("Grand Theft Auto IV", "Grand Theft Auto IV"),
    ("Red Dead Redemption 2", "Red Dead Redemption 2"),
    ("Red Dead Redemption", "Red Dead Redemption"),
    ("Max Payne 3", "Max Payne 3"),
    ("L.A. Noire", "L.A. Noire"),
    ("Bully Scholarship Edition", "Bully Scholarship Edition"),
    ("Grand Theft Auto III", "Grand Theft Auto III"),
    ("Grand Theft Auto Vice City", "Grand Theft Auto Vice City"),
    ("Grand Theft Auto San Andreas", "Grand Theft Auto San Andreas"),
];

impl Scanner for RockstarScanner {
    fn platform(&self) -> Platform {
        Platform::Rockstar
    }

    fn is_available(&self) -> bool {
        #[cfg(windows)]
        {
            util::registry::rockstar_launcher_path().is_some()
                || paths::program_files().join("Rockstar Games").is_dir()
                || paths::program_files_x86().join("Rockstar Games").is_dir()
        }
        #[cfg(not(windows))]
        {
            false
        }
    }

    fn scan(&self, options: &ScanOptions) -> Result<Vec<Game>> {
        let mut games = Vec::new();
        let mut seen = std::collections::HashSet::new();

        let launcher = {
            #[cfg(windows)]
            {
                util::registry::rockstar_launcher_path()
            }
            #[cfg(not(windows))]
            {
                None
            }
        };

        #[cfg(windows)]
        {
            // Enumerate Rockstar Games registry keys
            for base in [
                r"SOFTWARE\WOW6432Node\Rockstar Games",
                r"SOFTWARE\Rockstar Games",
            ] {
                for name in util::registry::subkey_names(util::registry::Hive::LocalMachine, base) {
                    if name.eq_ignore_ascii_case("Launcher")
                        || name.eq_ignore_ascii_case("Social Club")
                        || name.eq_ignore_ascii_case("Rockstar Games Social Club")
                    {
                        continue;
                    }
                    let key_path = format!(r"{base}\{name}");
                    let install = util::registry::read_string(
                        util::registry::Hive::LocalMachine,
                        &key_path,
                        "InstallFolder",
                    )
                    .or_else(|| {
                        util::registry::read_string(
                            util::registry::Hive::LocalMachine,
                            &key_path,
                            "Install Folder",
                        )
                    })
                    .or_else(|| {
                        util::registry::read_string(
                            util::registry::Hive::LocalMachine,
                            &key_path,
                            "InstallPath",
                        )
                    })
                    .map(PathBuf::from);

                    let Some(install_path) = install else {
                        continue;
                    };
                    if !install_path.is_dir() {
                        continue;
                    }
                    let store_id = name.clone();
                    if !seen.insert(store_id.clone()) {
                        continue;
                    }

                    let exe = find_rockstar_exe(&install_path, &name);
                    let mut game = Game::new(Platform::Rockstar, &store_id, name);
                    game.install_path = Some(install_path.clone());
                    game.executable = exe.clone();
                    game.launch = launch_target(&launcher, &exe, &install_path);
                    if options.compute_size {
                        game.size_bytes = util::dir_size(&install_path);
                    }
                    games.push(game);
                }
            }
        }

        // Fallback: Program Files\Rockstar Games\* folders
        for root in [
            paths::program_files().join("Rockstar Games"),
            paths::program_files_x86().join("Rockstar Games"),
        ] {
            if !root.is_dir() {
                continue;
            }
            if let Ok(entries) = std::fs::read_dir(&root) {
                for entry in entries.flatten() {
                    let path = entry.path();
                    if !path.is_dir() {
                        continue;
                    }
                    let name = entry.file_name().to_string_lossy().to_string();
                    if name.eq_ignore_ascii_case("Launcher")
                        || name.eq_ignore_ascii_case("Social Club")
                    {
                        continue;
                    }
                    if !seen.insert(name.clone()) {
                        continue;
                    }
                    let exe = find_rockstar_exe(&path, &name);
                    if exe.is_none() {
                        continue;
                    }
                    let mut game = Game::new(Platform::Rockstar, &name, name.clone());
                    game.install_path = Some(path.clone());
                    game.executable = exe.clone();
                    game.launch = launch_target(&launcher, &exe, &path);
                    if options.compute_size {
                        game.size_bytes = util::dir_size(&path);
                    }
                    games.push(game);
                }
            }
        }

        // Ensure known titles from fixed registry paths even if enum missed
        #[cfg(windows)]
        {
            for (display, key) in KNOWN_TITLES {
                if seen.contains(*key) {
                    continue;
                }
                let path = format!(r"SOFTWARE\WOW6432Node\Rockstar Games\{key}");
                if let Some(install) = util::registry::read_string(
                    util::registry::Hive::LocalMachine,
                    &path,
                    "InstallFolder",
                )
                .map(PathBuf::from)
                {
                    if install.is_dir() {
                        seen.insert((*key).to_string());
                        let exe = find_rockstar_exe(&install, key);
                        let mut game = Game::new(Platform::Rockstar, *key, *display);
                        game.install_path = Some(install.clone());
                        game.executable = exe.clone();
                        game.launch = launch_target(&launcher, &exe, &install);
                        if options.compute_size {
                            game.size_bytes = util::dir_size(&install);
                        }
                        games.push(game);
                    }
                }
            }
        }

        games.sort_by(|a, b| a.name.to_lowercase().cmp(&b.name.to_lowercase()));
        Ok(games)
    }
}

fn find_rockstar_exe(install: &std::path::Path, title: &str) -> Option<PathBuf> {
    let candidates = [
        "PlayGTAV.exe",
        "GTA5.exe",
        "PlayRDR2.exe",
        "RDR2.exe",
        "Launcher.exe",
        "MaxPayne3.exe",
        "LaNoire.exe",
        "Bully.exe",
    ];
    for c in candidates {
        let p = install.join(c);
        if p.is_file() {
            return Some(p);
        }
    }
    // title-based
    let sanitized = title.replace(' ', "");
    let p = install.join(format!("{sanitized}.exe"));
    if p.is_file() {
        return Some(p);
    }
    util::find_main_exe(install)
}

fn launch_target(
    launcher: &Option<PathBuf>,
    exe: &Option<PathBuf>,
    install: &std::path::Path,
) -> LaunchTarget {
    if let Some(exe) = exe {
        return LaunchTarget::Executable {
            path: exe.clone(),
            args: vec![],
            cwd: Some(install.to_path_buf()),
        };
    }
    if let Some(launcher) = launcher {
        let launcher_exe = if launcher.is_file() {
            launcher.clone()
        } else {
            launcher.join("Launcher.exe")
        };
        if launcher_exe.is_file() {
            return LaunchTarget::Executable {
                path: launcher_exe,
                args: vec![],
                cwd: launcher.parent().map(|p| p.to_path_buf()),
            };
        }
    }
    LaunchTarget::Unknown
}
