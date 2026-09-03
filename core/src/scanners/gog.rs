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

pub struct GogScanner;

impl Scanner for GogScanner {
    fn platform(&self) -> Platform {
        Platform::Gog
    }

    fn is_available(&self) -> bool {
        galaxy_db().is_file()
            || galaxy_dir().is_dir()
            || paths::local_app_data().join("GOG.com").is_dir()
    }

    fn scan(&self, options: &ScanOptions) -> Result<Vec<Game>> {
        let mut games = Vec::new();
        let mut seen = std::collections::HashSet::new();

        // 1) Galaxy SQLite
        if let Some(db) = galaxy_db().is_file().then(galaxy_db) {
            for row in query_galaxy_db(&db) {
                if !seen.insert(row.product_id.clone()) {
                    continue;
                }
                if let Some(ref install) = row.install_path {
                    if !install.is_dir() {
                        continue;
                    }
                }
                let exe = row
                    .exe_path
                    .as_ref()
                    .filter(|p| p.is_file())
                    .cloned()
                    .or_else(|| {
                        row.install_path
                            .as_ref()
                            .and_then(|p| util::find_main_exe(p))
                    });
                // Require real exe for "installed"
                let Some(exe) = exe else {
                    continue;
                };

                let mut game = Game::new(Platform::Gog, &row.product_id, &row.title);
                game.install_path = row.install_path.clone();
                game.executable = Some(exe.clone());
                game.launch = LaunchTarget::Executable {
                    path: exe,
                    args: row.args,
                    cwd: row.install_path.clone(),
                };
                if options.compute_size {
                    if let Some(ref p) = row.install_path {
                        game.size_bytes = util::dir_size(p);
                    }
                }
                games.push(game);
            }
        }

        // 2) Fallback: goggame-*.info under common roots (DRM-free offline installs)
        for root in gog_game_roots() {
            scan_goggame_infos(&root, options, &mut games, &mut seen);
        }

        games.sort_by(|a, b| a.name.to_lowercase().cmp(&b.name.to_lowercase()));
        Ok(games)
    }
}

struct GogRow {
    product_id: String,
    title: String,
    install_path: Option<PathBuf>,
    exe_path: Option<PathBuf>,
    args: Vec<String>,
}

fn galaxy_dir() -> PathBuf {
    paths::program_files_x86().join("GOG Galaxy")
}

fn galaxy_db() -> PathBuf {
    paths::program_data()
        .join("GOG.com")
        .join("Galaxy")
        .join("storage")
        .join("galaxy-2.0.db")
}

fn query_galaxy_db(db_path: &Path) -> Vec<GogRow> {
    let Ok(conn) = rusqlite::Connection::open_with_flags(
        db_path,
        rusqlite::OpenFlags::SQLITE_OPEN_READ_ONLY | rusqlite::OpenFlags::SQLITE_OPEN_NO_MUTEX,
    ) else {
        return Vec::new();
    };

    // Primary query (Galaxy 2.x)
    let sql = r#"
        SELECT
            ibp.productId,
            ld.title,
            ibp.installationPath,
            ptl.executablePath,
            ptl.commandLineArgs
        FROM InstalledBaseProducts ibp
        JOIN LimitedDetails ld
            ON ibp.productId = ld.productId
        LEFT JOIN PlayTasks pt
            ON pt.gameReleaseKey = 'gog_' || ibp.productId
           AND pt.isPrimary = 1
        LEFT JOIN PlayTaskLaunchParameters ptl
            ON ptl.playTaskId = pt.id
        WHERE ld.is_production = 1
    "#;

    let mut rows_out = Vec::new();
    if let Ok(mut stmt) = conn.prepare(sql) {
        let mapped = stmt.query_map([], |row| {
            Ok((
                row.get::<_, i64>(0).unwrap_or(0),
                row.get::<_, String>(1).unwrap_or_default(),
                row.get::<_, Option<String>>(2).ok().flatten(),
                row.get::<_, Option<String>>(3).ok().flatten(),
                row.get::<_, Option<String>>(4).ok().flatten(),
            ))
        });
        if let Ok(iter) = mapped {
            for item in iter.flatten() {
                let (pid, title, install, exe, args) = item;
                if title.is_empty() {
                    continue;
                }
                rows_out.push(GogRow {
                    product_id: pid.to_string(),
                    title,
                    install_path: install.map(|s| PathBuf::from(s.replace('/', "\\"))),
                    exe_path: exe.map(|s| PathBuf::from(s.replace('/', "\\"))),
                    args: args
                        .map(|a| a.split_whitespace().map(|s| s.to_string()).collect())
                        .unwrap_or_default(),
                });
            }
            return rows_out;
        }
    }

    // Fallback simpler query
    let simple = r#"
        SELECT productId, installationPath FROM InstalledBaseProducts
    "#;
    if let Ok(mut stmt) = conn.prepare(simple) {
        let mapped = stmt.query_map([], |row| {
            Ok((
                row.get::<_, i64>(0).unwrap_or(0),
                row.get::<_, Option<String>>(1).ok().flatten(),
            ))
        });
        if let Ok(iter) = mapped {
            for item in iter.flatten() {
                let (pid, install) = item;
                let install_path = install.map(|s| PathBuf::from(s.replace('/', "\\")));
                let title = install_path
                    .as_ref()
                    .and_then(|p| p.file_name())
                    .map(|s| s.to_string_lossy().into_owned())
                    .unwrap_or_else(|| format!("GOG {pid}"));
                rows_out.push(GogRow {
                    product_id: pid.to_string(),
                    title,
                    install_path,
                    exe_path: None,
                    args: vec![],
                });
            }
        }
    }
    rows_out
}

fn gog_game_roots() -> Vec<PathBuf> {
    let mut roots = vec![
        paths::program_files_x86().join("GOG Galaxy").join("Games"),
        paths::program_files().join("GOG Galaxy").join("Games"),
        paths::program_files_x86().join("GOG Games"),
        paths::program_files().join("GOG Games"),
    ];
    for drive in paths::fixed_drive_roots() {
        for rel in ["GOG Games", "Games\\GOG Games", "GOG Galaxy\\Games"] {
            let p = drive.join(rel);
            if p.is_dir() {
                roots.push(p);
            }
        }
    }
    roots.retain(|p| p.is_dir());
    roots.sort();
    roots.dedup();
    roots
}

fn scan_goggame_infos(
    root: &Path,
    options: &ScanOptions,
    games: &mut Vec<Game>,
    seen: &mut std::collections::HashSet<String>,
) {
    // One level of game folders
    let Ok(entries) = fs::read_dir(root) else {
        return;
    };
    for entry in entries.flatten() {
        let path = entry.path();
        if !path.is_dir() {
            continue;
        }
        // goggame-*.info in folder
        let Ok(files) = fs::read_dir(&path) else {
            continue;
        };
        for f in files.flatten() {
            let fp = f.path();
            let name = f.file_name().to_string_lossy().to_string();
            if !(name.starts_with("goggame-") && name.ends_with(".info")) {
                continue;
            }
            let Ok(text) = fs::read_to_string(&fp) else {
                continue;
            };
            let Ok(v) = serde_json::from_str::<serde_json::Value>(&text) else {
                continue;
            };
            let game_id = v
                .get("gameId")
                .or_else(|| v.get("rootGameId"))
                .and_then(|x| x.as_str().map(|s| s.to_string()).or_else(|| x.as_u64().map(|n| n.to_string())))
                .unwrap_or_else(|| name.clone());
            if !seen.insert(game_id.clone()) {
                continue;
            }
            let title = v
                .get("name")
                .and_then(|x| x.as_str())
                .unwrap_or(path.file_name().and_then(|s| s.to_str()).unwrap_or("GOG Game"))
                .to_string();
            let exe = util::find_main_exe(&path);
            let Some(exe) = exe else {
                continue;
            };
            let mut game = Game::new(Platform::Gog, &game_id, title);
            game.install_path = Some(path.clone());
            game.executable = Some(exe.clone());
            game.launch = LaunchTarget::Executable {
                path: exe,
                args: vec![],
                cwd: Some(path.clone()),
            };
            if options.compute_size {
                game.size_bytes = util::dir_size(&path);
            }
            games.push(game);
        }
    }
}
