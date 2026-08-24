// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
//! Thin Windows registry helpers.

use std::path::PathBuf;
use winreg::enums::*;
use winreg::RegKey;

pub fn hkcu() -> RegKey {
    RegKey::predef(HKEY_CURRENT_USER)
}

pub fn hklm() -> RegKey {
    RegKey::predef(HKEY_LOCAL_MACHINE)
}

pub fn string_value(key: &RegKey, name: &str) -> Option<String> {
    key.get_value::<String, _>(name).ok()
}

pub fn open_subkey(root: &RegKey, path: &str) -> Option<RegKey> {
    root.open_subkey(path).ok()
}

/// Read a string value from HKCU or HKLM path.
pub fn read_string(hive: Hive, path: &str, value: &str) -> Option<String> {
    let root = match hive {
        Hive::CurrentUser => hkcu(),
        Hive::LocalMachine => hklm(),
    };
    let key = open_subkey(&root, path)?;
    string_value(&key, value)
}

#[derive(Clone, Copy)]
pub enum Hive {
    CurrentUser,
    LocalMachine,
}

pub fn steam_install_path() -> Option<PathBuf> {
    read_string(Hive::CurrentUser, r"Software\Valve\Steam", "SteamPath")
        .or_else(|| read_string(Hive::LocalMachine, r"SOFTWARE\Valve\Steam", "InstallPath"))
        .or_else(|| read_string(Hive::LocalMachine, r"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"))
        .map(|p| PathBuf::from(p.replace('/', "\\")))
}

pub fn rockstar_launcher_path() -> Option<PathBuf> {
    read_string(
        Hive::LocalMachine,
        r"SOFTWARE\WOW6432Node\Rockstar Games\Launcher",
        "InstallFolder",
    )
    .or_else(|| {
        read_string(
            Hive::LocalMachine,
            r"SOFTWARE\Rockstar Games\Launcher",
            "InstallFolder",
        )
    })
    .map(PathBuf::from)
}

/// Enumerate subkeys under a path and collect a named string value from each.
pub fn enum_subkey_string_values(
    hive: Hive,
    path: &str,
    value_name: &str,
) -> Vec<(String, String)> {
    let root = match hive {
        Hive::CurrentUser => hkcu(),
        Hive::LocalMachine => hklm(),
    };
    let Ok(key) = root.open_subkey(path) else {
        return Vec::new();
    };
    let mut out = Vec::new();
    for name in key.enum_keys().filter_map(|k| k.ok()) {
        if let Ok(sub) = key.open_subkey(&name) {
            if let Some(val) = string_value(&sub, value_name) {
                out.push((name, val));
            }
        }
    }
    out
}

/// List immediate subkey names.
pub fn subkey_names(hive: Hive, path: &str) -> Vec<String> {
    let root = match hive {
        Hive::CurrentUser => hkcu(),
        Hive::LocalMachine => hklm(),
    };
    let Ok(key) = root.open_subkey(path) else {
        return Vec::new();
    };
    key.enum_keys().filter_map(|k| k.ok()).collect()
}

/// One entry from Windows Uninstall registry.
#[derive(Debug, Clone)]
pub struct UninstallEntry {
    pub key_name: String,
    pub display_name: Option<String>,
    pub publisher: Option<String>,
    pub install_location: Option<String>,
    pub display_icon: Option<String>,
    pub uninstall_string: Option<String>,
}

/// Enumerate Uninstall subkeys under a hive path.
pub fn enum_uninstall_entries(hive: Hive, path: &str) -> Vec<UninstallEntry> {
    let root = match hive {
        Hive::CurrentUser => hkcu(),
        Hive::LocalMachine => hklm(),
    };
    let Ok(key) = root.open_subkey(path) else {
        return Vec::new();
    };
    let mut out = Vec::new();
    for name in key.enum_keys().filter_map(|k| k.ok()) {
        let Ok(sub) = key.open_subkey(&name) else {
            continue;
        };
        out.push(UninstallEntry {
            key_name: name,
            display_name: string_value(&sub, "DisplayName"),
            publisher: string_value(&sub, "Publisher"),
            install_location: string_value(&sub, "InstallLocation"),
            display_icon: string_value(&sub, "DisplayIcon"),
            uninstall_string: string_value(&sub, "UninstallString"),
        });
    }
    out
}
