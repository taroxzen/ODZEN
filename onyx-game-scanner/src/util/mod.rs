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

/// Pick a reasonable main `.exe` under `dir` (largest non-helper binary).
pub fn find_main_exe(dir: &Path) -> Option<PathBuf> {
    if !dir.is_dir() {
        return None;
    }

    let skip_names = [
        "unitycrashhandler",
        "crashpad",
        "crashreporter",
        "uninstall",
        "unins",
        "redist",
        "vcredist",
        "dxsetup",
        "dotnet",
        "easyanticheat",
        "battleye",
        "launcher",
        "cefsharp",
        "notification_helper",
        "crashhandler",
    ];

    let mut best: Option<(u64, PathBuf)> = None;

    for entry in WalkDir::new(dir)
        .max_depth(3)
        .into_iter()
        .filter_map(|e| e.ok())
    {
        let path = entry.path();
        if path.extension().and_then(|e| e.to_str()).map(|e| e.eq_ignore_ascii_case("exe")) != Some(true)
        {
            continue;
        }
        let name = path
            .file_stem()
            .and_then(|s| s.to_str())
            .unwrap_or("")
            .to_ascii_lowercase();
        if skip_names.iter().any(|s| name.contains(s)) {
            continue;
        }
        let size = entry.metadata().map(|m| m.len()).unwrap_or(0);
        match &best {
            None => best = Some((size, path.to_path_buf())),
            Some((bs, _)) if size > *bs => best = Some((size, path.to_path_buf())),
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
