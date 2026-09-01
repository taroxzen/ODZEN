// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
use std::env;
use std::path::PathBuf;

pub fn program_data() -> PathBuf {
    env::var_os("ProgramData")
        .map(PathBuf::from)
        .unwrap_or_else(|| PathBuf::from(r"C:\ProgramData"))
}

pub fn local_app_data() -> PathBuf {
    env::var_os("LOCALAPPDATA")
        .map(PathBuf::from)
        .unwrap_or_else(|| {
            let home = env::var_os("USERPROFILE").map(PathBuf::from).unwrap_or_default();
            home.join("AppData").join("Local")
        })
}

pub fn roaming_app_data() -> PathBuf {
    env::var_os("APPDATA")
        .map(PathBuf::from)
        .unwrap_or_else(|| {
            let home = env::var_os("USERPROFILE").map(PathBuf::from).unwrap_or_default();
            home.join("AppData").join("Roaming")
        })
}

pub fn user_profile() -> PathBuf {
    env::var_os("USERPROFILE")
        .map(PathBuf::from)
        .unwrap_or_else(|| PathBuf::from(r"C:\Users\Default"))
}

pub fn program_files() -> PathBuf {
    env::var_os("ProgramFiles")
        .map(PathBuf::from)
        .unwrap_or_else(|| PathBuf::from(r"C:\Program Files"))
}

pub fn program_files_x86() -> PathBuf {
    env::var_os("ProgramFiles(x86)")
        .map(PathBuf::from)
        .unwrap_or_else(|| PathBuf::from(r"C:\Program Files (x86)"))
}

/// Fixed drive roots like `C:\`, `D:\` (Windows).
pub fn fixed_drive_roots() -> Vec<PathBuf> {
    let mut roots = Vec::new();
    for letter in b'A'..=b'Z' {
        let root = PathBuf::from(format!("{}:\\", letter as char));
        if root.exists() {
            roots.push(root);
        }
    }
    if roots.is_empty() {
        roots.push(PathBuf::from(r"C:\"));
    }
    roots
}
