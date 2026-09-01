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

pub struct EaScanner;

impl Scanner for EaScanner {
    fn platform(&self) -> Platform {
        Platform::Ea
    }

    fn is_available(&self) -> bool {
        #[cfg(windows)]
        {
            !candidate_roots().is_empty()
                || registry_has_ea()
                || !ea_uninstall_candidates().is_empty()
        }
        #[cfg(not(windows))]
        {
            !candidate_roots().is_empty()
        }
    }

    fn scan(&self, options: &ScanOptions) -> Result<Vec<Game>> {
        let mut games = Vec::new();
        let mut seen_ids = std::collections::HashSet::new();
        let mut seen_paths = std::collections::HashSet::new();

        // 1) Windows Uninstall — primary for modern EA Desktop installs (FC26, etc.)
        #[cfg(windows)]
        {
            for entry in ea_uninstall_candidates() {
                let Some(install) = entry.install_path.filter(|p| is_installed_game_dir(p)) else {
                    continue;
                };
                let path_key = normalize_path_key(&install);
                if !seen_paths.insert(path_key) {
                    continue;
                }

                let (name, content_id) = resolve_name_and_id(&install, &entry.display_name);
                let store_id = content_id
                    .clone()
                    .unwrap_or_else(|| entry.key_name.clone());
                if !seen_ids.insert(store_id.clone()) {
                    continue;
                }

                let exe = entry
                    .exe
                    .filter(|e| e.is_file())
                    .or_else(|| util::find_main_exe(&install));

                games.push(make_game(
                    &store_id,
                    &name,
                    install,
                    exe,
                    content_id,
                    options,
                ));
            }
        }

        // 2) Drive-root folders with __Installer (e.g. E:\EA SPORTS FC 26)
        for install in drive_root_ea_installs() {
            let path_key = normalize_path_key(&install);
            if !seen_paths.insert(path_key) {
                continue;
            }
            let (name, content_id) = resolve_name_and_id(&install, &None);
            let store_id = content_id
                .clone()
                .unwrap_or_else(|| folder_store_id(&install));
            if !seen_ids.insert(store_id.clone()) {
                continue;
            }
            let exe = util::find_main_exe(&install);
            games.push(make_game(
                &store_id,
                &name,
                install,
                exe,
                content_id,
                options,
            ));
        }

        // 3) Known EA Games / Electronic Arts library roots
        for root in candidate_roots() {
            scan_root(&root, options, &mut games, &mut seen_ids, &mut seen_paths);
        }

        // 4) Origin registry only if InstallDir actually exists on disk
        #[cfg(windows)]
        {
            for (id, display) in util::registry::enum_subkey_string_values(
                util::registry::Hive::LocalMachine,
                r"SOFTWARE\WOW6432Node\Origin Games",
                "DisplayName",
            ) {
                let install = util::registry::read_string(
                    util::registry::Hive::LocalMachine,
                    &format!(r"SOFTWARE\WOW6432Node\Origin Games\{id}"),
                    "InstallDir",
                )
                .or_else(|| {
                    util::registry::read_string(
                        util::registry::Hive::LocalMachine,
                        &format!(r"SOFTWARE\WOW6432Node\Origin Games\{id}"),
                        "InstallPath",
                    )
                })
                .map(|s| PathBuf::from(clean_path(&s)));

                let Some(install_path) = install else {
                    continue;
                };
                if !is_installed_game_dir(&install_path) {
                    continue;
                }
                let path_key = normalize_path_key(&install_path);
                if !seen_paths.insert(path_key) {
                    continue;
                }
                if !seen_ids.insert(id.clone()) {
                    continue;
                }
                let exe = util::find_main_exe(&install_path);
                games.push(make_game(
                    &id,
                    &display,
                    install_path,
                    exe,
                    Some(id.clone()),
                    options,
                ));
            }
        }

        games.sort_by(|a, b| a.name.to_lowercase().cmp(&b.name.to_lowercase()));
        Ok(games)
    }
}

struct UninstallGame {
    key_name: String,
    display_name: Option<String>,
    install_path: Option<PathBuf>,
    exe: Option<PathBuf>,
}

#[cfg(windows)]
fn ea_uninstall_candidates() -> Vec<UninstallGame> {
    use util::registry::{enum_uninstall_entries, Hive};

    let paths = [
        (Hive::LocalMachine, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        (
            Hive::LocalMachine,
            r"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        ),
        (Hive::CurrentUser, r"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
    ];

    let mut out = Vec::new();
    for (hive, path) in paths {
        for e in enum_uninstall_entries(hive, path) {
            if !is_ea_uninstall_entry(&e) {
                continue;
            }
            let display = e.display_name.clone().unwrap_or_default();
            if is_ea_launcher_name(&display) {
                continue;
            }

            let install_path = e
                .install_location
                .as_ref()
                .map(|s| PathBuf::from(clean_path(s)))
                .filter(|p| !p.as_os_str().is_empty());

            let exe = e
                .display_icon
                .as_ref()
                .map(|s| PathBuf::from(clean_icon_path(s)))
                .filter(|p| p.extension().and_then(|x| x.to_str()).map(|x| x.eq_ignore_ascii_case("exe")) == Some(true));

            out.push(UninstallGame {
                key_name: e.key_name,
                display_name: e.display_name,
                install_path,
                exe,
            });
        }
    }
    out
}

#[cfg(windows)]
fn is_ea_uninstall_entry(e: &util::registry::UninstallEntry) -> bool {
    let un = e.uninstall_string.as_deref().unwrap_or("").to_ascii_lowercase();
    let pub_ = e.publisher.as_deref().unwrap_or("").to_ascii_lowercase();
    let name = e.display_name.as_deref().unwrap_or("").to_ascii_lowercase();

    if un.contains("eainstaller") || un.contains("uninstall_game") {
        return true;
    }
    if pub_.contains("electronic arts") || pub_.contains("ea swiss") {
        // Need a game-like install location signal (not just EA app MSI)
        if e.install_location
            .as_ref()
            .map(|s| !clean_path(s).is_empty())
            .unwrap_or(false)
        {
            return true;
        }
    }
    // Fallback: cleanup path naming
    if un.contains("cleanup.exe") && (name.contains("ea sports") || name.contains("battlefield") || name.contains("apex"))
    {
        return true;
    }
    false
}

fn is_ea_launcher_name(name: &str) -> bool {
    let n = name.trim().to_ascii_lowercase();
    n == "ea app"
        || n == "ea desktop"
        || n == "origin"
        || n.starts_with("ea app ")
        || n == "electronic arts desktop"
}

/// True only when the folder looks like a real on-disk EA install.
fn is_installed_game_dir(path: &Path) -> bool {
    if !path.is_dir() {
        return false;
    }
    if path.join("__Installer").join("installerdata.xml").is_file() {
        return true;
    }
    util::find_main_exe(path).is_some()
}

fn registry_has_ea() -> bool {
    #[cfg(windows)]
    {
        util::registry::open_subkey(
            &util::registry::hklm(),
            r"SOFTWARE\WOW6432Node\Origin Games",
        )
        .is_some()
            || util::registry::open_subkey(
                &util::registry::hklm(),
                r"SOFTWARE\WOW6432Node\Electronic Arts",
            )
            .is_some()
            || PathBuf::from(r"C:\Program Files\Common Files\EAInstaller").is_dir()
    }
    #[cfg(not(windows))]
    {
        false
    }
}

fn candidate_roots() -> Vec<PathBuf> {
    let mut roots = vec![
        paths::program_files().join("EA Games"),
        paths::program_files_x86().join("EA Games"),
        paths::program_files().join("Electronic Arts"),
        paths::program_files_x86().join("Electronic Arts"),
    ];

    for drive in paths::fixed_drive_roots() {
        for rel in ["EA Games", r"Games\EA Games", r"Program Files\EA Games"] {
            let cand = drive.join(rel);
            if cand.is_dir() {
                roots.push(cand);
            }
        }
    }

    roots.retain(|p| p.is_dir());
    // Don't scan EA Desktop app folder as a game library root
    roots.retain(|p| {
        let s = p.to_string_lossy().to_ascii_lowercase();
        !s.ends_with("electronic arts\\ea desktop") && !s.ends_with("electronic arts/ea desktop")
    });
    roots.sort();
    roots.dedup();
    roots
}

/// Scan `X:\GameName\__Installer\installerdata.xml` one level under each drive root.
fn drive_root_ea_installs() -> Vec<PathBuf> {
    let mut out = Vec::new();
    for drive in paths::fixed_drive_roots() {
        let Ok(entries) = fs::read_dir(&drive) else {
            continue;
        };
        for entry in entries.flatten() {
            let path = entry.path();
            if !path.is_dir() {
                continue;
            }
            if is_installed_game_dir(&path)
                && path.join("__Installer").join("installerdata.xml").is_file()
            {
                out.push(path);
            }
        }
    }
    out
}

fn scan_root(
    root: &Path,
    options: &ScanOptions,
    games: &mut Vec<Game>,
    seen_ids: &mut std::collections::HashSet<String>,
    seen_paths: &mut std::collections::HashSet<String>,
) {
    let Ok(entries) = fs::read_dir(root) else {
        return;
    };
    for entry in entries.flatten() {
        let path = entry.path();
        if !path.is_dir() {
            continue;
        }
        let folder_name = entry.file_name().to_string_lossy().to_string();
        if is_skip_folder(&folder_name) {
            continue;
        }
        if !is_installed_game_dir(&path) {
            continue;
        }
        let path_key = normalize_path_key(&path);
        if !seen_paths.insert(path_key) {
            continue;
        }

        let (name, content_id) = resolve_name_and_id(&path, &Some(folder_name.clone()));
        let store_id = content_id
            .clone()
            .unwrap_or_else(|| folder_name.clone());
        if !seen_ids.insert(store_id.clone()) {
            continue;
        }

        let exe = util::find_main_exe(&path);
        games.push(make_game(
            &store_id,
            &name,
            path,
            exe,
            content_id,
            options,
        ));
    }
}

fn resolve_name_and_id(install: &Path, fallback_name: &Option<String>) -> (String, Option<String>) {
    let installer_xml = install.join("__Installer").join("installerdata.xml");
    if installer_xml.is_file() {
        if let Some((name, id)) = parse_installerdata(&installer_xml) {
            return (name, id);
        }
    }
    let name = fallback_name
        .clone()
        .or_else(|| {
            install
                .file_name()
                .map(|s| s.to_string_lossy().into_owned())
        })
        .unwrap_or_else(|| "EA Game".into());
    (name, None)
}

fn make_game(
    store_id: &str,
    name: &str,
    install: PathBuf,
    exe: Option<PathBuf>,
    content_id: Option<String>,
    options: &ScanOptions,
) -> Game {
    let mut game = Game::new(Platform::Ea, store_id, name);
    game.install_path = Some(install.clone());
    game.executable = exe.clone();
    game.launch = if let Some(id) = content_id {
        LaunchTarget::Protocol {
            uri: format!("origin2://game/launch?offerIds={id}"),
        }
    } else if let Some(e) = exe {
        LaunchTarget::Executable {
            path: e,
            args: vec![],
            cwd: Some(install.clone()),
        }
    } else {
        LaunchTarget::Unknown
    };
    if options.compute_size {
        game.size_bytes = util::dir_size(&install);
    }
    game
}

fn is_skip_folder(name: &str) -> bool {
    let n = name.to_ascii_lowercase();
    n == "ea desktop"
        || n == "directx"
        || n == "electronic arts"
        || n.contains("redist")
        || n.contains("redistributable")
        || n == "support"
        || n == "installer"
        || n == "ea services"
}

fn parse_installerdata(path: &Path) -> Option<(String, Option<String>)> {
    let text = fs::read_to_string(path).ok()?;
    let content_id = extract_xml_text(&text, "contentID")
        .or_else(|| extract_xml_text(&text, "contentId"));

    // Prefer en_US gameTitle, then any gameTitle, then title
    let title = extract_xml_attr_locale(&text, "gameTitle", "en_US")
        .or_else(|| extract_xml_text(&text, "gameTitle"))
        .or_else(|| extract_xml_text(&text, "title"))
        .unwrap_or_else(|| {
            path.parent()
                .and_then(|p| p.parent())
                .and_then(|p| p.file_name())
                .map(|s| s.to_string_lossy().into_owned())
                .unwrap_or_else(|| "EA Game".into())
        });
    let title = strip_trailing_parens(&title);
    Some((title, content_id))
}

fn extract_xml_text(xml: &str, tag: &str) -> Option<String> {
    let open = format!("<{tag}");
    let start_tag = xml.find(&open)?;
    let after = &xml[start_tag..];
    let gt = after.find('>')?;
    if after[..gt].ends_with('/') {
        return None;
    }
    let content_start = start_tag + gt + 1;
    let close = format!("</{tag}>");
    let end = xml[content_start..].find(&close)? + content_start;
    let val = xml[content_start..end].trim();
    // If nested / empty
    if val.is_empty() || val.starts_with('<') {
        // try simple pattern
        let open2 = format!("<{tag}>");
        let s = xml.find(&open2)? + open2.len();
        let e = xml[s..].find(&close)? + s;
        let v = xml[s..e].trim();
        if v.is_empty() {
            None
        } else {
            Some(v.to_string())
        }
    } else {
        Some(val.to_string())
    }
}

fn extract_xml_attr_locale(xml: &str, tag: &str, locale: &str) -> Option<String> {
    // <gameTitle locale="en_US">EA SPORTS FC 26</gameTitle>
    let needle = format!("<{tag} locale=\"{locale}\">");
    let start = xml.find(&needle)? + needle.len();
    let close = format!("</{tag}>");
    let end = xml[start..].find(&close)? + start;
    let val = xml[start..end].trim();
    if val.is_empty() {
        None
    } else {
        Some(val.to_string())
    }
}

fn strip_trailing_parens(s: &str) -> String {
    let mut out = s.trim().to_string();
    while let Some(start) = out.rfind('(') {
        if out.ends_with(')') {
            out = out[..start].trim().to_string();
        } else {
            break;
        }
    }
    out
}

fn clean_path(s: &str) -> String {
    s.trim()
        .trim_matches('"')
        .trim_end_matches(['\\', '/'])
        .replace('/', "\\")
}

fn clean_icon_path(s: &str) -> String {
    let s = s.trim().trim_matches('"');
    // Strip ",0" icon index
    if let Some((path, _idx)) = s.rsplit_once(',') {
        if _idx.chars().all(|c| c.is_ascii_digit() || c == '-') {
            return path.trim().trim_matches('"').replace('/', "\\");
        }
    }
    s.replace('/', "\\")
}

fn normalize_path_key(path: &Path) -> String {
    path.to_string_lossy().to_ascii_lowercase().replace('/', "\\")
}

fn folder_store_id(path: &Path) -> String {
    path.file_name()
        .map(|s| s.to_string_lossy().into_owned())
        .unwrap_or_else(|| "ea_game".into())
}
