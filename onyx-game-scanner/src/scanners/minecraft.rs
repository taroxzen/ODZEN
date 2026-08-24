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

pub struct MinecraftScanner;

impl Scanner for MinecraftScanner {
    fn platform(&self) -> Platform {
        Platform::Minecraft
    }

    fn is_available(&self) -> bool {
        vanilla_dir().is_dir()
            || curseforge_instances().is_dir()
            || prism_roots().iter().any(|p| p.is_dir())
            || bedrock_package_present()
    }

    fn scan(&self, options: &ScanOptions) -> Result<Vec<Game>> {
        let mut games = Vec::new();
        let mut seen = std::collections::HashSet::new();

        // Official Java launcher profile / .minecraft
        if vanilla_dir().is_dir() {
            let dir = vanilla_dir();
            let store_id = "java_vanilla";
            if seen.insert(store_id.to_string()) {
                let mut game = Game::new(Platform::Minecraft, store_id, "Minecraft Java Edition");
                game.install_path = Some(dir.clone());
                game.tags.push("java".into());
                game.launch = minecraft_launcher_target();
                if options.compute_size {
                    game.size_bytes = util::dir_size(&dir);
                }
                games.push(game);
            }

            // Installed versions as optional tags/extra entries? Keep single vanilla entry.
            // Also detect Minecraft Dungeons / Legends from product library
            scan_product_library(options, &mut games, &mut seen);
        }

        // Bedrock via known package name
        if bedrock_package_present() {
            let store_id = "bedrock";
            if seen.insert(store_id.to_string()) {
                let mut game = Game::new(Platform::Minecraft, store_id, "Minecraft Bedrock Edition");
                game.tags.push("bedrock".into());
                game.launch = LaunchTarget::Protocol {
                    uri: "shell:appsFolder\\Microsoft.MinecraftUWP_8wekyb3d8bbwe!App".into(),
                };
                games.push(game);
            }
        }

        // CurseForge instances
        let cf = curseforge_instances();
        if cf.is_dir() {
            scan_instance_folder(
                &cf,
                "curseforge",
                options,
                &mut games,
                &mut seen,
                InstanceKind::CurseForge,
            );
        }

        // Prism Launcher instances
        for root in prism_roots() {
            let instances = root.join("instances");
            if instances.is_dir() {
                scan_instance_folder(
                    &instances,
                    "prism",
                    options,
                    &mut games,
                    &mut seen,
                    InstanceKind::Prism,
                );
            }
        }

        games.sort_by(|a, b| a.name.to_lowercase().cmp(&b.name.to_lowercase()));
        Ok(games)
    }
}

enum InstanceKind {
    CurseForge,
    Prism,
}

fn vanilla_dir() -> PathBuf {
    paths::roaming_app_data().join(".minecraft")
}

fn curseforge_instances() -> PathBuf {
    // User has: C:\Users\taner\curseforge\minecraft\Instances
    paths::user_profile()
        .join("curseforge")
        .join("minecraft")
        .join("Instances")
}

fn prism_roots() -> Vec<PathBuf> {
    vec![
        paths::roaming_app_data().join("PrismLauncher"),
        paths::local_app_data().join("PrismLauncher"),
        paths::roaming_app_data().join("PolyMC"),
        paths::roaming_app_data().join("PrimeHack"),
    ]
}

fn bedrock_package_present() -> bool {
    // Cheap check: package folder or start menu / known path
    #[cfg(windows)]
    {
        // PowerShell is heavy; check common install marker via Get-AppxPackage is done in xbox.
        // Here: look for Microsoft.MinecraftUWP under LocalAppData Packages
        let packages = paths::local_app_data().join("Packages");
        if packages.is_dir() {
            if let Ok(entries) = fs::read_dir(&packages) {
                for entry in entries.flatten() {
                    let name = entry.file_name().to_string_lossy().to_string();
                    if name.starts_with("Microsoft.MinecraftUWP") {
                        return true;
                    }
                }
            }
        }
        false
    }
    #[cfg(not(windows))]
    {
        false
    }
}

fn minecraft_launcher_target() -> LaunchTarget {
    let candidates = [
        paths::program_files()
            .join("Minecraft Launcher")
            .join("MinecraftLauncher.exe"),
        paths::program_files_x86()
            .join("Minecraft Launcher")
            .join("MinecraftLauncher.exe"),
        paths::local_app_data()
            .join("Programs")
            .join("minecraft-launcher")
            .join("MinecraftLauncher.exe"),
        // New Microsoft Store / Xbox PC app style
        paths::local_app_data()
            .join("Packages")
            .join("Microsoft.4297127D64EC6_8wekyb3d8bbwe")
            .join("LocalCache")
            .join("Local")
            .join("game")
            .join("Minecraft.exe"),
    ];
    for c in candidates {
        if c.is_file() {
            return LaunchTarget::Executable {
                path: c.clone(),
                args: vec![],
                cwd: c.parent().map(|p| p.to_path_buf()),
            };
        }
    }
    // URI for Microsoft Gaming Services launcher if present
    LaunchTarget::Protocol {
        uri: "minecraft://".into(),
    }
}

fn scan_product_library(
    options: &ScanOptions,
    games: &mut Vec<Game>,
    seen: &mut std::collections::HashSet<String>,
) {
    // Official launcher settings may point to product library (Dungeons, Legends)
    let settings = [
        paths::roaming_app_data()
            .join(".minecraft")
            .join("launcher_settings.json"),
        paths::local_app_data()
            .join("Packages")
            .join("Microsoft.4297127D64EC6_8wekyb3d8bbwe")
            .join("LocalCache")
            .join("Local")
            .join("launcher_settings.json"),
    ];
    for s in settings {
        if !s.is_file() {
            continue;
        }
        let Ok(text) = fs::read_to_string(&s) else {
            continue;
        };
        let Ok(v) = serde_json::from_str::<Value>(&text) else {
            continue;
        };
        let Some(lib) = v.get("productLibraryDir").and_then(|x| x.as_str()) else {
            continue;
        };
        let lib = PathBuf::from(lib);
        if !lib.is_dir() {
            continue;
        }
        // Dungeons
        let dungeons = lib.join("dungeons").join("dungeons").join("Dungeons.exe");
        if dungeons.is_file() && seen.insert("dungeons".into()) {
            let mut game = Game::new(Platform::Minecraft, "dungeons", "Minecraft Dungeons");
            game.install_path = dungeons.parent().map(|p| p.to_path_buf());
            game.executable = Some(dungeons.clone());
            game.launch = LaunchTarget::Executable {
                path: dungeons.clone(),
                args: vec![],
                cwd: dungeons.parent().map(|p| p.to_path_buf()),
            };
            if options.compute_size {
                if let Some(p) = game.install_path.as_ref() {
                    game.size_bytes = util::dir_size(p);
                }
            }
            games.push(game);
        }
        // Legends
        let legends_candidates = [
            lib.join("legends").join("MinecraftLegends.exe"),
            lib.join("Legends").join("MinecraftLegends.exe"),
        ];
        for leg in legends_candidates {
            if leg.is_file() && seen.insert("legends".into()) {
                let mut game = Game::new(Platform::Minecraft, "legends", "Minecraft Legends");
                game.install_path = leg.parent().map(|p| p.to_path_buf());
                game.executable = Some(leg.clone());
                game.launch = LaunchTarget::Executable {
                    path: leg.clone(),
                    args: vec![],
                    cwd: leg.parent().map(|p| p.to_path_buf()),
                };
                games.push(game);
                break;
            }
        }
    }
}

fn scan_instance_folder(
    instances: &Path,
    source: &str,
    options: &ScanOptions,
    games: &mut Vec<Game>,
    seen: &mut std::collections::HashSet<String>,
    kind: InstanceKind,
) {
    let Ok(entries) = fs::read_dir(instances) else {
        return;
    };
    for entry in entries.flatten() {
        let path = entry.path();
        if !path.is_dir() {
            continue;
        }
        let folder = entry.file_name().to_string_lossy().to_string();
        let store_id = format!("{source}:{folder}");
        if !seen.insert(store_id.clone()) {
            continue;
        }

        let display_name = match kind {
            InstanceKind::CurseForge => {
                // minecraftinstance.json may have name
                read_curseforge_name(&path).unwrap_or_else(|| folder.clone())
            }
            InstanceKind::Prism => {
                read_prism_name(&path).unwrap_or_else(|| folder.clone())
            }
        };

        let mut game = Game::new(
            Platform::Minecraft,
            &store_id,
            format!("Minecraft - {display_name}"),
        );
        game.install_path = Some(path.clone());
        game.tags.push(source.into());
        game.tags.push("instance".into());
        game.launch = instance_launch(&path, &kind);
        if options.compute_size {
            game.size_bytes = util::dir_size(&path);
        }
        games.push(game);
    }
}

fn read_curseforge_name(instance: &Path) -> Option<String> {
    let meta = instance.join("minecraftinstance.json");
    let text = fs::read_to_string(meta).ok()?;
    let v: Value = serde_json::from_str(&text).ok()?;
    v.get("name")
        .and_then(|x| x.as_str())
        .map(|s| s.to_string())
}

fn read_prism_name(instance: &Path) -> Option<String> {
    // instance.cfg is key=value; also mmc-pack.json
    let cfg = instance.join("instance.cfg");
    if let Ok(text) = fs::read_to_string(&cfg) {
        for line in text.lines() {
            if let Some(rest) = line.strip_prefix("name=") {
                let name = rest.trim();
                if !name.is_empty() {
                    return Some(name.to_string());
                }
            }
        }
    }
    None
}

fn instance_launch(instance: &Path, kind: &InstanceKind) -> LaunchTarget {
    match kind {
        InstanceKind::CurseForge => {
            // CurseForge app protocol is unreliable; point at instance folder exe if any
            if let Some(exe) = util::find_main_exe(instance) {
                LaunchTarget::Executable {
                    path: exe,
                    args: vec![],
                    cwd: Some(instance.to_path_buf()),
                }
            } else {
                LaunchTarget::Protocol {
                    uri: "curseforge://".into(),
                }
            }
        }
        InstanceKind::Prism => {
            // Prism can launch by instance dir via CLI if binary found
            let prism_bins = [
                paths::local_app_data()
                    .join("Programs")
                    .join("PrismLauncher")
                    .join("prismlauncher.exe"),
                paths::program_files()
                    .join("PrismLauncher")
                    .join("prismlauncher.exe"),
            ];
            for bin in prism_bins {
                if bin.is_file() {
                    let id = instance
                        .file_name()
                        .map(|s| s.to_string_lossy().into_owned())
                        .unwrap_or_default();
                    return LaunchTarget::Executable {
                        path: bin,
                        args: vec!["--launch".into(), id],
                        cwd: None,
                    };
                }
            }
            LaunchTarget::Unknown
        }
    }
}
