// ============================================================================
// ODZEN Core — Unified Rust Core Engine
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
//! # ODZEN Core
//!
//! Unified, high-performance native engine for ODZEN Gaming Platform:
//! - Multi-threaded game discovery across 13+ launchers and local directories.
//! - 4K artwork & logo resolving pipeline with SIMD transparency auto-cropping.
//! - Process and protocol game execution with argument escaping and CWD resolution.
//! - Hardware diagnostics and music streaming launcher integrations.

#![cfg_attr(not(windows), allow(dead_code, unused_imports))]

pub mod artwork;
pub mod error;
pub mod launcher;
pub mod models;
pub mod music;
pub mod scanner;
pub mod sysinfo;
pub mod util;

// Backward-compatibility and ergonomic module aliases
pub use models as model;
pub use scanner as scanners;

// Public Top-Level API Exports
pub use artwork::ArtworkEngine;
pub use error::{GameFindError, Result};
pub use launcher::LauncherEngine;
pub use models::{
    Game, LaunchTarget, Platform, PlatformPresence, PlatformStatus, ScanOptions, ScanReport,
    SearchOptions,
};
pub use music::{MusicApp, MusicAppCategory, MusicLaunchTarget, MusicScanReport, MusicScanner};
pub use scanner::GameFindEngine;
pub use sysinfo::{SysInfoReport, SysinfoEngine};
