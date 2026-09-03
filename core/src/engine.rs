// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
use std::collections::HashSet;
use std::time::Instant;

use rayon::prelude::*;

use crate::error::Result;
use crate::model::{
    Game, Platform, PlatformPresence, PlatformStatus, ScanOptions, ScanReport, SearchOptions,
};
use crate::scanners::{self, Scanner};

/// High-level entry point for UIs and the CLI.
#[derive(Default)]
pub struct GameFindEngine {
    /// Optional progress callback: (platform_name, message)
    progress: Option<Box<dyn Fn(&str, &str) + Send + Sync>>,
}

impl GameFindEngine {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn with_progress<F>(mut self, f: F) -> Self
    where
        F: Fn(&str, &str) + Send + Sync + 'static,
    {
        self.progress = Some(Box::new(f));
        self
    }

    /// Scan all (or selected) platforms and return a merged report.
    pub fn scan(&self, options: ScanOptions) -> Result<ScanReport> {
        let start = Instant::now();
        let platforms: Vec<Platform> = if options.platforms.is_empty() {
            Platform::ALL.to_vec()
        } else {
            options.platforms.clone()
        };

        let want_local = platforms.contains(&Platform::Local);
        let store_platforms: Vec<Platform> = platforms
            .iter()
            .copied()
            .filter(|p| *p != Platform::Local)
            .collect();

        // 1) Store / launcher scanners first (parallel)
        let store_scanners: Vec<Box<dyn Scanner>> = store_platforms
            .iter()
            .filter_map(|p| scanners::scanner_for(*p))
            .collect();

        let store_results: Vec<(Platform, bool, Result<Vec<Game>>)> = store_scanners
            .par_iter()
            .map(|scanner| {
                let platform = scanner.platform();
                if let Some(cb) = &self.progress {
                    cb(platform.as_str(), "scanning");
                }
                let available = scanner.is_available();
                let games = if available {
                    scanner.scan(&options)
                } else {
                    Ok(Vec::new())
                };
                (platform, available, games)
            })
            .collect();

        let mut all_games = Vec::new();
        let mut statuses = Vec::new();
        let mut warnings = Vec::new();

        for (platform, available, result) in store_results {
            push_platform_result(
                platform,
                available,
                result,
                &mut all_games,
                &mut statuses,
                &mut warnings,
            );
        }

        // Paths already claimed by store scanners
        let known_paths = collect_known_paths(&all_games);

        // 2) Local (Yerel) — only installs not already listed
        if want_local {
            if let Some(scanner) = scanners::scanner_for(Platform::Local) {
                if let Some(cb) = &self.progress {
                    cb("local", "scanning");
                }
                let available = scanner.is_available();
                let result = if available {
                    scanner.scan(&options).map(|games| {
                        games
                            .into_iter()
                            .filter(|g| !game_path_known(g, &known_paths))
                            .collect::<Vec<_>>()
                    })
                } else {
                    Ok(Vec::new())
                };
                push_platform_result(
                    Platform::Local,
                    available,
                    result,
                    &mut all_games,
                    &mut statuses,
                    &mut warnings,
                );
            }
        }

        // Stable sort
        statuses.sort_by_key(|s| s.platform.as_str());
        all_games = dedupe_games(all_games);
        // Prefer store entry over local for same path (extra safety)
        all_games = prefer_store_over_local(all_games);
        all_games.sort_by(|a, b| {
            a.name
                .to_lowercase()
                .cmp(&b.name.to_lowercase())
                .then(a.platform.as_str().cmp(b.platform.as_str()))
        });

        Ok(ScanReport {
            games: all_games,
            platforms: statuses,
            duration_ms: 0,
            warnings,
        }
        .with_duration(start.elapsed()))
    }

    /// Scan a single platform.
    pub fn scan_platform(&self, platform: Platform) -> Result<Vec<Game>> {
        let options = ScanOptions {
            platforms: vec![platform],
            ..Default::default()
        };
        Ok(self.scan(options)?.games)
    }

    /// Probe which platforms appear installed (cheap).
    pub fn detect_platforms(&self) -> Vec<PlatformStatus> {
        scanners::all_scanners()
            .into_iter()
            .map(|s| {
                let available = s.is_available();
                PlatformStatus {
                    platform: s.platform(),
                    status: if available {
                        PlatformPresence::Present
                    } else {
                        PlatformPresence::Missing
                    },
                    game_count: 0,
                    message: None,
                }
            })
            .collect()
    }

    /// Search within a previous scan report (in-memory).
    pub fn search_in<'a>(
        &self,
        games: &'a [Game],
        query: &str,
        options: SearchOptions,
    ) -> Vec<&'a Game> {
        let q = query.trim().to_ascii_lowercase();
        if q.is_empty() {
            return games
                .iter()
                .filter(|g| options.platform.map(|p| g.platform == p).unwrap_or(true))
                .take(options.limit.unwrap_or(usize::MAX))
                .collect();
        }

        let mut scored: Vec<(i32, &Game)> = games
            .iter()
            .filter(|g| options.platform.map(|p| g.platform == p).unwrap_or(true))
            .filter_map(|g| {
                let name = g.name.to_ascii_lowercase();
                let id = g.id.to_ascii_lowercase();
                let platform = g.platform.as_str();
                let score = fuzzy_score(&q, &name)
                    .max(fuzzy_score(&q, &id))
                    .max(if platform.contains(&q) { 50 } else { 0 });
                if score > 0 {
                    Some((score, g))
                } else {
                    None
                }
            })
            .collect();

        scored.sort_by(|a, b| b.0.cmp(&a.0).then(a.1.name.cmp(&b.1.name)));
        let limit = options.limit.unwrap_or(usize::MAX);
        scored.into_iter().take(limit).map(|(_, g)| g).collect()
    }

    /// Convenience: scan then search.
    pub fn search(&self, query: &str, scan: ScanOptions, search: SearchOptions) -> Result<Vec<Game>> {
        let report = self.scan(scan)?;
        Ok(self
            .search_in(&report.games, query, search)
            .into_iter()
            .cloned()
            .collect())
    }
}

fn push_platform_result(
    platform: Platform,
    available: bool,
    result: Result<Vec<Game>>,
    all_games: &mut Vec<Game>,
    statuses: &mut Vec<PlatformStatus>,
    warnings: &mut Vec<String>,
) {
    match result {
        Ok(games) => {
            let count = games.len();
            let status = if !available {
                PlatformPresence::Missing
            } else if count == 0 {
                PlatformPresence::Empty
            } else {
                PlatformPresence::Present
            };
            statuses.push(PlatformStatus {
                platform,
                status,
                game_count: count,
                message: None,
            });
            all_games.extend(games);
        }
        Err(e) => {
            let msg = e.to_string();
            warnings.push(format!("{platform}: {msg}"));
            statuses.push(PlatformStatus {
                platform,
                status: PlatformPresence::Error,
                game_count: 0,
                message: Some(msg),
            });
        }
    }
}

fn dedupe_games(games: Vec<Game>) -> Vec<Game> {
    let mut seen_ids = HashSet::new();
    let mut seen_paths = HashSet::new();
    let mut out = Vec::with_capacity(games.len());
    for g in games {
        if !seen_ids.insert(g.id.clone()) {
            continue;
        }
        if let Some(ref p) = g.install_path {
            let key = format!("{}:{}", g.platform.as_str(), normalize_path_key(p));
            if !seen_paths.insert(key) {
                continue;
            }
        }
        out.push(g);
    }
    out
}

fn normalize_path_key(p: &std::path::Path) -> String {
    p.to_string_lossy()
        .to_ascii_lowercase()
        .replace('/', "\\")
        .trim_end_matches('\\')
        .to_string()
}

fn collect_known_paths(games: &[Game]) -> HashSet<String> {
    let mut set = HashSet::new();
    for g in games {
        if g.platform == Platform::Local {
            continue;
        }
        if let Some(ref p) = g.install_path {
            set.insert(normalize_path_key(p));
            // Also parent of install if nested (common\Game)
            if let Some(parent) = p.parent() {
                set.insert(normalize_path_key(parent));
            }
        }
        if let Some(ref e) = g.executable {
            if let Some(parent) = e.parent() {
                set.insert(normalize_path_key(parent));
            }
        }
    }
    set
}

fn game_path_known(game: &Game, known: &HashSet<String>) -> bool {
    if let Some(ref p) = game.install_path {
        let key = normalize_path_key(p);
        if known.contains(&key) {
            return true;
        }
        // Local game under a known store root (e.g. inside steamapps\common\X)
        for k in known {
            if key.starts_with(k) || k.starts_with(&key) {
                // Only treat as known if one path is prefix of the other with boundary
                if key == *k
                    || key.starts_with(&format!("{k}\\"))
                    || k.starts_with(&format!("{key}\\"))
                {
                    return true;
                }
            }
        }
    }
    if let Some(ref e) = game.executable {
        if let Some(parent) = e.parent() {
            let key = normalize_path_key(parent);
            if known.contains(&key) {
                return true;
            }
        }
    }
    false
}

/// Drop local entries that share install path with a store game.
fn prefer_store_over_local(games: Vec<Game>) -> Vec<Game> {
    let store_paths: HashSet<String> = games
        .iter()
        .filter(|g| g.platform != Platform::Local)
        .filter_map(|g| g.install_path.as_ref().map(|p| normalize_path_key(p)))
        .collect();

    games
        .into_iter()
        .filter(|g| {
            if g.platform != Platform::Local {
                return true;
            }
            match &g.install_path {
                Some(p) => !store_paths.contains(&normalize_path_key(p)),
                None => true,
            }
        })
        .collect()
}

/// Simple substring / prefix scoring (no external fuzzy crate required).
fn fuzzy_score(query: &str, text: &str) -> i32 {
    if text == query {
        return 1000;
    }
    if text.starts_with(query) {
        return 800;
    }
    if text.contains(query) {
        return 600;
    }
    // token match
    let tokens: Vec<&str> = text.split_whitespace().collect();
    for t in tokens {
        if t.starts_with(query) {
            return 400;
        }
        if t.contains(query) {
            return 200;
        }
    }
    // subsequence soft match
    if is_subsequence(query, text) {
        return 100;
    }
    0
}

fn is_subsequence(query: &str, text: &str) -> bool {
    let mut it = text.chars();
    for qc in query.chars() {
        loop {
            match it.next() {
                Some(tc) if tc == qc => break,
                Some(_) => continue,
                None => return false,
            }
        }
    }
    true
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn fuzzy_prefers_prefix() {
        assert!(fuzzy_score("gta", "gta v") > fuzzy_score("gta", "something gta"));
    }
}
