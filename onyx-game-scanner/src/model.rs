// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
use serde::{Deserialize, Serialize};
use std::path::PathBuf;
use std::time::{Duration, SystemTime};

/// Supported game platforms / stores.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum Platform {
    Steam,
    Epic,
    Xbox,
    Ea,
    Riot,
    Rockstar,
    Minecraft,
    BattleNet,
    Ubisoft,
    Gog,
    Amazon,
    /// Folder/heuristic scan for installs not claimed by store scanners.
    Local,
    /// Metin2 private server / yan sunucu scanner
    Metin2,
    Unknown,
}

impl Platform {
    pub const ALL: [Platform; 13] = [
        Platform::Steam,
        Platform::Epic,
        Platform::Xbox,
        Platform::Ea,
        Platform::Riot,
        Platform::Rockstar,
        Platform::Minecraft,
        Platform::BattleNet,
        Platform::Ubisoft,
        Platform::Gog,
        Platform::Amazon,
        Platform::Local,
        Platform::Metin2,
    ];

    pub fn as_str(self) -> &'static str {
        match self {
            Platform::Steam => "steam",
            Platform::Epic => "epic",
            Platform::Xbox => "xbox",
            Platform::Ea => "ea",
            Platform::Riot => "riot",
            Platform::Rockstar => "rockstar",
            Platform::Minecraft => "minecraft",
            Platform::BattleNet => "battlenet",
            Platform::Ubisoft => "ubisoft",
            Platform::Gog => "gog",
            Platform::Amazon => "amazon",
            Platform::Local => "local",
            Platform::Metin2 => "metin2",
            Platform::Unknown => "unknown",
        }
    }

    pub fn display_name(self) -> &'static str {
        match self {
            Platform::Steam => "Steam",
            Platform::Epic => "Epic Games",
            Platform::Xbox => "Xbox / Microsoft Store",
            Platform::Ea => "EA App",
            Platform::Riot => "Riot Games",
            Platform::Rockstar => "Rockstar Games",
            Platform::Minecraft => "Minecraft",
            Platform::BattleNet => "Battle.net",
            Platform::Ubisoft => "Ubisoft Connect",
            Platform::Gog => "GOG Galaxy",
            Platform::Amazon => "Amazon Games",
            Platform::Local => "Yerel",
            Platform::Metin2 => "Metin2 Yan Sunucu",
            Platform::Unknown => "Unknown",
        }
    }

    pub fn parse_list(s: &str) -> Vec<Platform> {
        s.split(',')
            .filter_map(|p| match p.trim().to_ascii_lowercase().as_str() {
                "steam" => Some(Platform::Steam),
                "epic" => Some(Platform::Epic),
                "xbox" | "microsoft" | "msstore" => Some(Platform::Xbox),
                "ea" | "origin" => Some(Platform::Ea),
                "riot" => Some(Platform::Riot),
                "rockstar" | "r*" => Some(Platform::Rockstar),
                "minecraft" | "mc" => Some(Platform::Minecraft),
                "battlenet" | "battle_net" | "battle.net" | "bnet" | "blizzard" => {
                    Some(Platform::BattleNet)
                }
                "ubisoft" | "uplay" | "ubi" => Some(Platform::Ubisoft),
                "gog" | "gog_galaxy" | "galaxy" => Some(Platform::Gog),
                "amazon" | "amazon_games" | "nile" => Some(Platform::Amazon),
                "local" | "yerel" | "folder" | "folders" => Some(Platform::Local),
                "metin2" | "metin" | "metin2_pserver" | "pserver" => Some(Platform::Metin2),
                _ => None,
            })
            .collect()
    }
}

impl std::fmt::Display for Platform {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{}", self.as_str())
    }
}

/// How a UI should start the game (data only — library does not spawn processes).
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(tag = "type", rename_all = "snake_case")]
pub enum LaunchTarget {
    /// URI protocol, e.g. `steam://rungameid/730`
    Protocol { uri: String },
    /// Direct executable launch.
    Executable {
        path: PathBuf,
        #[serde(default, skip_serializing_if = "Vec::is_empty")]
        args: Vec<String>,
        #[serde(default, skip_serializing_if = "Option::is_none")]
        cwd: Option<PathBuf>,
    },
    /// No known launch method.
    Unknown,
}

/// A discovered installed game.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Game {
    /// Stable id: `{platform}:{store_id}` e.g. `steam:271590`
    pub id: String,
    pub name: String,
    pub platform: Platform,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub install_path: Option<PathBuf>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub executable: Option<PathBuf>,
    pub launch: LaunchTarget,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub store_id: Option<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub size_bytes: Option<u64>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub last_played: Option<SystemTime>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub tags: Vec<String>,
}

impl Game {
    pub fn new(platform: Platform, store_id: impl Into<String>, name: impl Into<String>) -> Self {
        let store_id = store_id.into();
        let id = format!("{}:{}", platform.as_str(), store_id);
        Self {
            id,
            name: name.into(),
            platform,
            install_path: None,
            executable: None,
            launch: LaunchTarget::Unknown,
            store_id: Some(store_id),
            size_bytes: None,
            last_played: None,
            tags: Vec::new(),
        }
    }
}

/// Result of probing whether a launcher/platform is present.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "snake_case")]
pub enum PlatformPresence {
    Present,
    Empty,
    Missing,
    Error,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PlatformStatus {
    pub platform: Platform,
    pub status: PlatformPresence,
    pub game_count: usize,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub message: Option<String>,
}

/// Full scan output (primary exit point for UIs).
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct ScanReport {
    pub games: Vec<Game>,
    pub platforms: Vec<PlatformStatus>,
    pub duration_ms: u64,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub warnings: Vec<String>,
}

impl ScanReport {
    pub fn with_duration(mut self, d: Duration) -> Self {
        self.duration_ms = d.as_millis() as u64;
        self
    }
}

/// Options for [`crate::GameFindEngine::scan`].
#[derive(Debug, Clone)]
pub struct ScanOptions {
    pub platforms: Vec<Platform>,
    /// Include Steam tools / redistributables when true.
    pub include_tools: bool,
    pub compute_size: bool,
    /// Extra folders treated as local game containers (UI / CLI `--folder`).
    pub extra_folders: Vec<PathBuf>,
}

impl Default for ScanOptions {
    fn default() -> Self {
        Self {
            platforms: Platform::ALL.to_vec(),
            include_tools: false,
            compute_size: false,
            extra_folders: Vec::new(),
        }
    }
}

/// Options for in-memory search.
#[derive(Debug, Clone, Default)]
pub struct SearchOptions {
    pub platform: Option<Platform>,
    pub limit: Option<usize>,
}
