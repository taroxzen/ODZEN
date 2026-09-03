// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
use std::fs;
use std::path::{Path, PathBuf};
use std::process::Command;

use serde::Deserialize;
use serde_json::Value;

use crate::error::Result;
use crate::model::{Game, LaunchTarget, Platform, ScanOptions};
use crate::scanners::Scanner;
use crate::util::{self, paths};

pub struct XboxScanner;

#[derive(Debug, Deserialize)]
#[serde(rename_all = "PascalCase")]
struct AppxPackage {
    name: Option<String>,
    package_family_name: Option<String>,
    install_location: Option<String>,
    #[serde(default)]
    is_framework: Option<bool>,
    display_name: Option<String>,
}

impl Scanner for XboxScanner {
    fn platform(&self) -> Platform {
        Platform::Xbox
    }

    fn is_available(&self) -> bool {
        xbox_games_roots().iter().any(|p| p.is_dir()) || cfg!(windows)
    }

    fn scan(&self, options: &ScanOptions) -> Result<Vec<Game>> {
        let mut games = Vec::new();
        // Dedupe keys: package family, xboxgames id, and normalized display names
        let mut seen_ids = std::collections::HashSet::new();
        let mut seen_names = std::collections::HashSet::new();

        #[cfg(windows)]
        let appx_packages = list_appx_packages();
        #[cfg(not(windows))]
        let appx_packages: Vec<AppxPackage> = Vec::new();

        // 1) Physical XboxGames folders first (preferred install location)
        for root in xbox_games_roots() {
            scan_xbox_games_folder(
                &root,
                options,
                &mut games,
                &mut seen_ids,
                &mut seen_names,
                &appx_packages,
            );
        }

        // 2) AppX packages — stricter: MicrosoftGame.config required; skip Minecraft / junk
        #[cfg(windows)]
        {
            for pkg in &appx_packages {
                if pkg.is_framework == Some(true) {
                    continue;
                }
                let Some(install) = pkg.install_location.as_ref().map(PathBuf::from) else {
                    continue;
                };
                if !install.is_dir() {
                    continue;
                }

                let family = pkg
                    .package_family_name
                    .clone()
                    .unwrap_or_else(|| pkg.name.clone().unwrap_or_else(|| "unknown".into()));
                let pkg_name = pkg.name.clone().unwrap_or_default();
                let display = resolve_display_name(pkg, &install);

                if is_minecraft_related(&pkg_name, &family, &display, &install) {
                    continue;
                }
                if is_junk_package(&pkg_name, &family, &display) {
                    continue;
                }

                // Prefer real PC Game Pass / Xbox titles with MicrosoftGame.config
                let has_game_config = install.join("MicrosoftGame.config").is_file()
                    || install.join("Content").join("MicrosoftGame.config").is_file();
                if !has_game_config {
                    continue;
                }

                if !seen_ids.insert(family.clone()) {
                    continue;
                }
                let name_key = normalize_name(&display);
                if !name_key.is_empty() && !seen_names.insert(name_key) {
                    // Already listed from XboxGames folder
                    continue;
                }

                let manifest = install.join("AppxManifest.xml");
                let config = install.join("MicrosoftGame.config");
                let config_alt = install.join("Content").join("MicrosoftGame.config");
                let from_config = parse_microsoft_game_config(&config)
                    .or_else(|| parse_microsoft_game_config(&config_alt));

                // Prefer Executable Id from MicrosoftGame.config over generic App
                let app_id = from_config
                    .as_ref()
                    .and_then(|c| c.app_id.clone())
                    .or_else(|| parse_app_id(&manifest))
                    .unwrap_or_else(|| "App".into());

                let game_dir = if install.join("Content").is_dir()
                    && install.join("Content").join("MicrosoftGame.config").is_file()
                {
                    install.join("Content")
                } else {
                    install.clone()
                };
                let (exe, _) = resolve_xbox_launch(&game_dir, from_config.as_ref());

                let mut game = Game::new(Platform::Xbox, &family, display);
                game.install_path = Some(game_dir.clone());
                game.executable = exe;
                // AppX/MSIX: shell protocol is the reliable launch path
                game.launch = LaunchTarget::Protocol {
                    uri: format!("shell:appsFolder\\{family}!{app_id}"),
                };
                game.tags.push("xbox".into());
                if options.compute_size {
                    game.size_bytes = util::dir_size(&game_dir);
                }
                games.push(game);
            }
        }

        games.sort_by(|a, b| a.name.to_lowercase().cmp(&b.name.to_lowercase()));
        Ok(games)
    }
}

fn xbox_games_roots() -> Vec<PathBuf> {
    let mut roots = Vec::new();
    for drive in paths::fixed_drive_roots() {
        let p = drive.join("XboxGames");
        if p.is_dir() {
            roots.push(p);
        }
    }
    roots
}

fn scan_xbox_games_folder(
    root: &Path,
    options: &ScanOptions,
    games: &mut Vec<Game>,
    seen_ids: &mut std::collections::HashSet<String>,
    seen_names: &mut std::collections::HashSet<String>,
    appx_packages: &[AppxPackage],
) {
    let Ok(entries) = fs::read_dir(root) else {
        return;
    };
    for entry in entries.flatten() {
        let path = entry.path();
        if !path.is_dir() {
            continue;
        }
        let name = entry.file_name().to_string_lossy().to_string();

        if is_minecraft_related("", "", &name, &path) {
            continue;
        }
        if is_junk_package("", "", &name) {
            continue;
        }

        let content = path.join("Content");
        let game_dir = if content.is_dir() {
            content
        } else {
            path.clone()
        };

        let store_id = format!("xboxgames:{name}");
        if !seen_ids.insert(store_id.clone()) {
            continue;
        }

        let config_path = game_dir.join("MicrosoftGame.config");
        let has_config = config_path.is_file();
        let from_config = parse_microsoft_game_config(&config_path);
        let (exe, app_id) = resolve_xbox_launch(&game_dir, from_config.as_ref());

        if exe.is_none() && !has_config {
            continue;
        }

        let name_key = normalize_name(&name);
        if !name_key.is_empty() {
            seen_names.insert(name_key);
        }

        // Prefer shell protocol when we can resolve package family (Game Pass)
        let identity_name = from_config
            .as_ref()
            .and_then(|_| parse_identity_name(&config_path));
        let family = parse_package_family_from_appx(&game_dir.join("appxmanifest.xml"))
            .or_else(|| parse_package_family_from_appx(&game_dir.join("AppxManifest.xml")))
            .or_else(|| {
                identity_name
                    .as_ref()
                    .and_then(|id| find_appx_family_by_identity(id, appx_packages))
            });

        let mut game = Game::new(Platform::Xbox, &store_id, name);
        game.install_path = Some(game_dir.clone());
        game.executable = exe.clone();
        game.launch = resolve_launch_target(&game_dir, family.as_deref(), app_id.as_deref(), exe);
        game.tags.push("xbox_games_folder".into());
        if options.compute_size {
            game.size_bytes = util::dir_size(&game_dir);
        }
        games.push(game);
    }
}

#[derive(Debug, Clone)]
struct MsGameExecutable {
    /// Relative path from game root, e.g. `Discovery.exe` or `FortniteGame/Binaries/Win64/GDKLauncher.exe`
    exe_name: String,
    app_id: Option<String>,
}

/// Parse `<Executable Name="..." Id="..."/>` from MicrosoftGame.config.
fn parse_microsoft_game_config(path: &Path) -> Option<MsGameExecutable> {
    let text = fs::read_to_string(path).ok()?;
    // Prefer TargetDeviceFamily="PC" entry when multiple exist
    let mut fallback: Option<MsGameExecutable> = None;
    let mut search = text.as_str();
    while let Some(idx) = search.find("<Executable ") {
        let slice = &search[idx..];
        let end = slice.find("/>").or_else(|| slice.find('>'))?;
        let tag = &slice[..end];
        let name = attr_value(tag, "Name")?;
        let id = attr_value(tag, "Id");
        let entry = MsGameExecutable {
            exe_name: name,
            app_id: id,
        };
        if tag.contains("TargetDeviceFamily=\"PC\"") || tag.contains("TargetDeviceFamily='PC'") {
            return Some(entry);
        }
        if fallback.is_none() {
            fallback = Some(entry);
        }
        search = &slice[1..];
    }
    fallback
}

fn attr_value(tag: &str, key: &str) -> Option<String> {
    for quote in ['"', '\''] {
        let needle = format!("{key}={quote}");
        if let Some(start) = tag.find(&needle) {
            let rest = &tag[start + needle.len()..];
            if let Some(end) = rest.find(quote) {
                let v = rest[..end].trim();
                if !v.is_empty() {
                    return Some(v.to_string());
                }
            }
        }
    }
    None
}

/// Resolve executable from MicrosoftGame.config first, then smart fallbacks.
fn resolve_xbox_launch(
    game_dir: &Path,
    from_config: Option<&MsGameExecutable>,
) -> (Option<PathBuf>, Option<String>) {
    if let Some(cfg) = from_config {
        let rel = cfg.exe_name.replace('/', "\\");
        let full = game_dir.join(&rel);
        if full.is_file() {
            return (Some(full), cfg.app_id.clone());
        }
        // Sometimes path is relative to Content parent
        if let Some(parent) = game_dir.parent() {
            let alt = parent.join(&rel);
            if alt.is_file() {
                return (Some(alt), cfg.app_id.clone());
            }
        }
        // Bare filename search one level
        if let Some(file_name) = Path::new(&rel).file_name() {
            let shallow = game_dir.join(file_name);
            if shallow.is_file() {
                return (Some(shallow), cfg.app_id.clone());
            }
        }
        return (find_xbox_game_exe(game_dir), cfg.app_id.clone());
    }
    (find_xbox_game_exe(game_dir), None)
}

/// Prefer real game binaries over helpers / anti-cheat installers.
fn find_xbox_game_exe(dir: &Path) -> Option<PathBuf> {
    // Well-known preferred names first
    const PREFERRED: &[&str] = &[
        "gamelaunchhelper.exe",
        "Discovery.exe",
        "GDKLauncher.exe",
        "GameLaunchHelper.exe",
    ];
    for name in PREFERRED {
        let p = dir.join(name);
        if p.is_file() {
            return Some(p);
        }
    }

    // Walk but skip installers / anti-cheat
    let skip = [
        "anticheat",
        "easyanticheat",
        "battleye",
        "installer",
        "uninstall",
        "redist",
        "vcredist",
        "setup",
        "crash",
        "cefsharp",
        "notification",
        "prereq",
        "dotnet",
        "dxsetup",
        "msiexec",
        "elytra",
        "denuvo",
    ];

    let mut best: Option<(i32, u64, PathBuf)> = None; // score, size, path
    for entry in walkdir::WalkDir::new(dir)
        .max_depth(5)
        .into_iter()
        .filter_map(|e| e.ok())
    {
        let path = entry.path();
        if path
            .extension()
            .and_then(|e| e.to_str())
            .map(|e| e.eq_ignore_ascii_case("exe"))
            != Some(true)
        {
            continue;
        }
        let name = path
            .file_stem()
            .and_then(|s| s.to_str())
            .unwrap_or("")
            .to_ascii_lowercase();
        let full_lower = path.to_string_lossy().to_ascii_lowercase();
        if skip.iter().any(|s| name.contains(s) || full_lower.contains(&format!("\\{s}"))) {
            continue;
        }
        // Prefer not under Installers/
        let mut score = 0i32;
        if full_lower.contains("\\installers\\") {
            score -= 100;
        }
        if name.contains("launch") || name.contains("shipping") || name.contains("game") {
            score += 20;
        }
        if name == "discovery" || name == "gdklauncher" || name == "gamelaunchhelper" {
            score += 50;
        }
        let size = entry.metadata().map(|m| m.len()).unwrap_or(0);
        // Prefer larger among same score (real game binaries tend to be big)
        let key = (score, size);
        match &best {
            None => best = Some((score, size, path.to_path_buf())),
            Some((bs, bsz, _)) if key > (*bs, *bsz) => {
                best = Some((score, size, path.to_path_buf()))
            }
            _ => {}
        }
    }
    best.map(|(_, _, p)| p)
}

fn resolve_launch_target(
    game_dir: &Path,
    family: Option<&str>,
    app_id: Option<&str>,
    exe: Option<PathBuf>,
) -> LaunchTarget {
    // Shell protocol is most reliable for Game Pass / MSIX-style installs
    if let (Some(family), Some(app_id)) = (family, app_id) {
        if !family.is_empty() && !app_id.is_empty() {
            return LaunchTarget::Protocol {
                uri: format!("shell:appsFolder\\{family}!{app_id}"),
            };
        }
    }
    if let Some(exe) = exe {
        // Avoid launching anti-cheat installers as the game
        let n = exe
            .file_name()
            .and_then(|s| s.to_str())
            .unwrap_or("")
            .to_ascii_lowercase();
        if n.contains("anticheat")
            || (n.contains("installer") && !n.contains("gamelaunchhelper"))
        {
            // fall through to gamelaunchhelper if present
            let helper = game_dir.join("gamelaunchhelper.exe");
            if helper.is_file() {
                return LaunchTarget::Executable {
                    path: helper,
                    args: vec![],
                    cwd: Some(game_dir.to_path_buf()),
                };
            }
        } else {
            return LaunchTarget::Executable {
                path: exe,
                args: vec![],
                cwd: Some(game_dir.to_path_buf()),
            };
        }
    }
    let helper = game_dir.join("gamelaunchhelper.exe");
    if helper.is_file() {
        return LaunchTarget::Executable {
            path: helper,
            args: vec![],
            cwd: Some(game_dir.to_path_buf()),
        };
    }
    LaunchTarget::Unknown
}

/// PackageFamilyName from AppxManifest when present.
fn parse_package_family_from_appx(path: &Path) -> Option<String> {
    let text = fs::read_to_string(path).ok()?;
    if let Some(idx) = text.find("PackageFamilyName=\"") {
        let rest = &text[idx + "PackageFamilyName=\"".len()..];
        if let Some(end) = rest.find('"') {
            return Some(rest[..end].to_string());
        }
    }
    None
}

/// `<Identity Name="Embark.THEFINALS" .../>` from MicrosoftGame.config
fn parse_identity_name(config_path: &Path) -> Option<String> {
    let text = fs::read_to_string(config_path).ok()?;
    let id_block_start = text.find("<Identity ")?;
    let id_block = &text[id_block_start..];
    let id_end = id_block.find("/>").or_else(|| id_block.find('>'))?;
    attr_value(&id_block[..id_end], "Name")
}

/// Match AppX PackageFamilyName by Identity Name prefix (e.g. Embark.THEFINALS → Embark.THEFINALS_xxxx).
fn find_appx_family_by_identity(identity_name: &str, packages: &[AppxPackage]) -> Option<String> {
    let identity_lower = identity_name.to_ascii_lowercase();
    for pkg in packages {
        let family = pkg.package_family_name.as_deref().unwrap_or("");
        let name = pkg.name.as_deref().unwrap_or("");
        if name.eq_ignore_ascii_case(identity_name)
            || family
                .to_ascii_lowercase()
                .starts_with(&format!("{identity_lower}_"))
        {
            if !family.is_empty() {
                return Some(family.to_string());
            }
        }
    }
    None
}

/// Minecraft is owned by the dedicated Minecraft scanner — never list under Xbox.
fn is_minecraft_related(pkg_name: &str, family: &str, display: &str, path: &Path) -> bool {
    let blob = format!(
        "{} {} {} {}",
        pkg_name.to_ascii_lowercase(),
        family.to_ascii_lowercase(),
        display.to_ascii_lowercase(),
        path.to_string_lossy().to_ascii_lowercase()
    );
    blob.contains("minecraft")
        || blob.contains("microsoft.minecraftuwp")
        || blob.contains("microsoft.4297127d64ec6") // official Minecraft Launcher MSIX
}

fn is_junk_package(pkg_name: &str, family: &str, display: &str) -> bool {
    let blob = format!(
        "{} {} {}",
        pkg_name.to_ascii_lowercase(),
        family.to_ascii_lowercase(),
        display.to_ascii_lowercase()
    );

    const JUNK_SUBSTRINGS: &[&str] = &[
        "gamingservices",
        "microsoft.gamingservices",
        "xboxidentityprovider",
        "xboxgameoverlay",
        "xboxspeechtotextoverlay",
        "xbox.tcui",
        "xboxapp",
        "xbox.callableui",
        "gamingapp",
        "microsoftbubble",
        "microsoft.microsoftbubble",
        "gethelp",
        "yourphone",
        "windows.photos",
        "zunevideo",
        "bingweather",
        "solitaire",
        // Services / non-games often mis-tagged
        "edge.gameassist",
        "copilot",
    ];

    JUNK_SUBSTRINGS.iter().any(|s| blob.contains(s))
}

fn normalize_name(name: &str) -> String {
    name.chars()
        .filter(|c| c.is_alphanumeric())
        .flat_map(|c| c.to_lowercase())
        .collect()
}

fn list_appx_packages() -> Vec<AppxPackage> {
    #[cfg(not(windows))]
    {
        return Vec::new();
    }
    #[cfg(windows)]
    {
    let ps = r#"Get-AppxPackage | Where-Object { -not $_.IsFramework } | Select-Object Name, PackageFamilyName, InstallLocation, IsFramework | ConvertTo-Json -Compress -Depth 3"#;
    let output = Command::new("powershell")
        .args([
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-Command",
            ps,
        ])
        .output();

    let Ok(output) = output else {
        return Vec::new();
    };
    if !output.status.success() {
        return Vec::new();
    }
    let text = String::from_utf8_lossy(&output.stdout);
    let text = text.trim();
    if text.is_empty() {
        return Vec::new();
    }

    if let Ok(list) = serde_json::from_str::<Vec<AppxPackage>>(text) {
        return list;
    }
    if let Ok(one) = serde_json::from_str::<AppxPackage>(text) {
        return vec![one];
    }
    if let Ok(val) = serde_json::from_str::<Value>(text) {
        let arr = if let Some(a) = val.as_array() {
            a.clone()
        } else {
            vec![val]
        };
        return arr
            .into_iter()
            .filter_map(|v| {
                Some(AppxPackage {
                    name: v.get("Name").and_then(|x| x.as_str()).map(|s| s.to_string()),
                    package_family_name: v
                        .get("PackageFamilyName")
                        .and_then(|x| x.as_str())
                        .map(|s| s.to_string()),
                    install_location: v
                        .get("InstallLocation")
                        .and_then(|x| x.as_str())
                        .map(|s| s.to_string()),
                    is_framework: v.get("IsFramework").and_then(|x| x.as_bool()),
                    display_name: None,
                })
            })
            .collect();
    }
    Vec::new()
    } // cfg(windows)
}

fn resolve_display_name(pkg: &AppxPackage, install: &Path) -> String {
    if let Some(d) = &pkg.display_name {
        if !d.is_empty() && !d.starts_with("ms-resource:") {
            return d.clone();
        }
    }
    let manifest = install.join("AppxManifest.xml");
    if let Some(n) = parse_manifest_display_name(&manifest) {
        if !n.starts_with("ms-resource:") {
            return n;
        }
    }
    pkg.name
        .clone()
        .unwrap_or_else(|| "Xbox Game".into())
}

fn parse_manifest_display_name(path: &Path) -> Option<String> {
    let text = fs::read_to_string(path).ok()?;
    let marker = "<DisplayName>";
    let start = text.find(marker)? + marker.len();
    let end = text[start..].find("</DisplayName>")? + start;
    let name = text[start..end].trim();
    if name.is_empty() {
        None
    } else {
        Some(name.to_string())
    }
}

fn parse_app_id(manifest: &Path) -> Option<String> {
    let text = fs::read_to_string(manifest).ok()?;
    let key = "Application Id=\"";
    let start = text.find(key)? + key.len();
    let end = text[start..].find('"')? + start;
    Some(text[start..end].to_string())
}
