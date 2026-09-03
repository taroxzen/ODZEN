// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
use std::path::{Path, PathBuf};

use crate::error::Result;
use crate::model::{Game, LaunchTarget, Platform, ScanOptions};
use crate::scanners::Scanner;
use crate::util::{self, paths};

pub struct AmazonScanner;

impl Scanner for AmazonScanner {
    fn platform(&self) -> Platform {
        Platform::Amazon
    }

    fn is_available(&self) -> bool {
        game_install_db().is_file() || amazon_app_dir().is_dir() || amazon_data_dir().is_dir()
    }

    fn scan(&self, options: &ScanOptions) -> Result<Vec<Game>> {
        let mut games = Vec::new();
        let mut seen = std::collections::HashSet::new();

        let db = game_install_db();
        if db.is_file() {
            for row in query_install_db(&db) {
                if !seen.insert(row.id.clone()) {
                    continue;
                }
                // Prefer install dir on disk when known
                if let Some(ref install) = row.install_dir {
                    if !install.is_dir() {
                        continue;
                    }
                } else {
                    // Installed flag without path — still allow if we only have id
                    // but require Installed=1 from query; skip if no path (strict)
                    continue;
                }

                let install = row.install_dir.clone().unwrap();
                let exe = util::find_main_exe(&install);
                let mut game = Game::new(Platform::Amazon, &row.id, &row.title);
                game.install_path = Some(install.clone());
                game.executable = exe;
                game.launch = LaunchTarget::Protocol {
                    uri: format!("amazon-games://play/{}", row.id),
                };
                if options.compute_size {
                    game.size_bytes = util::dir_size(&install);
                }
                games.push(game);
            }
        }

        games.sort_by(|a, b| a.name.to_lowercase().cmp(&b.name.to_lowercase()));
        Ok(games)
    }
}

struct AmazonRow {
    id: String,
    title: String,
    install_dir: Option<PathBuf>,
}

fn amazon_data_dir() -> PathBuf {
    paths::local_app_data().join("Amazon Games").join("Data")
}

fn amazon_app_dir() -> PathBuf {
    paths::local_app_data().join("Amazon Games").join("App")
}

fn game_install_db() -> PathBuf {
    amazon_data_dir()
        .join("Games")
        .join("Sql")
        .join("GameInstallInfo.sqlite")
}

fn query_install_db(db_path: &Path) -> Vec<AmazonRow> {
    let Ok(conn) = rusqlite::Connection::open_with_flags(
        db_path,
        rusqlite::OpenFlags::SQLITE_OPEN_READ_ONLY | rusqlite::OpenFlags::SQLITE_OPEN_NO_MUTEX,
    ) else {
        return Vec::new();
    };

    // Discover table/columns — Amazon schema has varied across versions
    let tables: Vec<String> = conn
        .prepare("SELECT name FROM sqlite_master WHERE type='table'")
        .ok()
        .and_then(|mut s| {
            s.query_map([], |r| r.get::<_, String>(0))
                .ok()
                .map(|rows| rows.filter_map(|x| x.ok()).collect())
        })
        .unwrap_or_default();

    let table = ["DbSet", "GameInstallInfo", "InstalledGames", "Games"]
        .into_iter()
        .find(|t| tables.iter().any(|x| x.eq_ignore_ascii_case(t)))
        .unwrap_or("DbSet");

    let cols: Vec<String> = conn
        .prepare(&format!("PRAGMA table_info({table})"))
        .ok()
        .and_then(|mut s| {
            s.query_map([], |r| r.get::<_, String>(1))
                .ok()
                .map(|rows| rows.filter_map(|x| x.ok()).collect())
        })
        .unwrap_or_default();

    let id_col = pick_col(&cols, &["Id", "GameId", "ProductId", "id"]);
    let title_col = pick_col(&cols, &["ProductTitle", "Title", "Name", "GameName"]);
    let install_col = pick_col(
        &cols,
        &[
            "InstallDirectory",
            "InstallDir",
            "InstallPath",
            "Folder",
            "Path",
        ],
    );
    let installed_col = pick_col(&cols, &["Installed", "IsInstalled", "installed"]);

    let Some(id_col) = id_col else {
        return Vec::new();
    };
    let title_col = title_col.unwrap_or_else(|| id_col.clone());

    let mut sql = format!("SELECT {id_col}, {title_col}");
    if let Some(ref ic) = install_col {
        sql.push_str(&format!(", {ic}"));
    }
    sql.push_str(&format!(" FROM {table}"));
    if let Some(ref icol) = installed_col {
        sql.push_str(&format!(" WHERE {icol} = 1 OR {icol} = '1' OR {icol} = true"));
    }

    let mut out = Vec::new();
    let Ok(mut stmt) = conn.prepare(&sql) else {
        return out;
    };

    let has_install = install_col.is_some();
    let mapped = stmt.query_map([], |row| {
        let id: String = row
            .get::<_, String>(0)
            .or_else(|_| row.get::<_, i64>(0).map(|n| n.to_string()))
            .unwrap_or_default();
        let title: String = row.get::<_, String>(1).unwrap_or_else(|_| id.clone());
        let install = if has_install {
            row.get::<_, Option<String>>(2)
                .ok()
                .flatten()
                .map(|s| PathBuf::from(s.replace('/', "\\")))
        } else {
            None
        };
        Ok((id, title, install))
    });

    if let Ok(iter) = mapped {
        for item in iter.flatten() {
            let (id, title, install) = item;
            if id.is_empty() {
                continue;
            }
            out.push(AmazonRow {
                id,
                title,
                install_dir: install,
            });
        }
    }
    out
}

fn pick_col(cols: &[String], candidates: &[&str]) -> Option<String> {
    for c in candidates {
        if let Some(found) = cols.iter().find(|x| x.eq_ignore_ascii_case(c)) {
            return Some(found.clone());
        }
    }
    None
}
