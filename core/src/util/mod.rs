// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
pub mod paths;
pub mod vdf;

#[cfg(windows)]
pub mod registry;

use std::path::{Path, PathBuf};
use walkdir::WalkDir;

/// Pick the authentic main `.exe` under `dir` (highest score, filtering out helper/redist binaries).
pub fn find_main_exe(dir: &Path) -> Option<PathBuf> {
    if !dir.is_dir() {
        return None;
    }

    let skip_names = [
        "unitycrashhandler",
        "crashpad",
        "crashreporter",
        "crashhandler",
        "uninstall",
        "unins",
        "redist",
        "vcredist",
        "dxsetup",
        "dxwebsetup",
        "quicksfv",
        "yamakaldır",
        "yamakaldir",
        "dotnet",
        "easyanticheat",
        "battleye",
        "cefsharp",
        "notification_helper",
        "report",
        "patcher",
        "setup",
        "installer",
        "helper",
        "config",
        "autoupdate",
    ];

    let skip_dir_parts = [
        "_redist",
        "\\redist",
        "/redist",
        "directx",
        "support",
        "prerequisites",
        "installer",
        "dependencies",
        "$recycle.bin",
    ];

    let dir_name = dir
        .file_name()
        .and_then(|s| s.to_str())
        .unwrap_or("")
        .to_ascii_lowercase();

    let mut best: Option<(i64, PathBuf)> = None;

    for entry in WalkDir::new(dir)
        .max_depth(5)
        .into_iter()
        .filter_map(|e| e.ok())
    {
        let path = entry.path();
        if path.extension().and_then(|e| e.to_str()).map(|e| e.eq_ignore_ascii_case("exe")) != Some(true)
        {
            continue;
        }

        let path_str = path.to_string_lossy().to_ascii_lowercase();
        if skip_dir_parts.iter().any(|d| path_str.contains(d)) {
            continue;
        }

        let stem = path
            .file_stem()
            .and_then(|s| s.to_str())
            .unwrap_or("")
            .to_ascii_lowercase();

        if skip_names.iter().any(|s| stem.contains(s)) {
            continue;
        }

        let size = entry.metadata().map(|m| m.len()).unwrap_or(0);
        if size < 50_000 {
            continue;
        }

        let mut score = size as i64;

        // Unreal Engine / Modern Shipping 64-bit Game Binary Priority
        if stem.ends_with("-win64-shipping") || stem.ends_with("_shipping") || stem.ends_with("shipping") {
            score += 150_000_000;
        }
        // Direct match with directory title
        if !dir_name.is_empty() && stem == dir_name {
            score += 100_000_000;
        }
        // Preferred subdirectories
        if path_str.contains("binaries\\win64") || path_str.contains("binaries/win64") {
            score += 50_000_000;
        }

        match &best {
            None => best = Some((score, path.to_path_buf())),
            Some((bs, _)) if score > *bs => best = Some((score, path.to_path_buf())),
            _ => {}
        }
    }

    best.map(|(_, p)| p)
}

/// Best-effort directory size (may be slow; only used when compute_size is on).
pub fn dir_size(path: &Path) -> Option<u64> {
    if !path.exists() {
        return None;
    }
    let mut total = 0u64;
    for entry in WalkDir::new(path).into_iter().filter_map(|e| e.ok()) {
        if entry.file_type().is_file() {
            total = total.saturating_add(entry.metadata().map(|m| m.len()).unwrap_or(0));
        }
    }
    Some(total)
}
