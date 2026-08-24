// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
//! # gamefind
//!
//! Local Windows game discovery engine. Scans installed games from Steam, Epic,
//! Xbox, EA, Riot, Rockstar, and Minecraft (including CurseForge / Prism instances).
//!
//! This crate is **library-first**: UI authors consume [`GameFindEngine`] and
//! [`ScanReport`] (or shell out to the `gamefind` CLI with `--json`).
//!
//! ## Example
//!
//! ```no_run
//! use gamefind::{GameFindEngine, ScanOptions};
//!
//! let engine = GameFindEngine::new();
//! let report = engine.scan(ScanOptions::default()).unwrap();
//! for game in report.games {
//!     println!("{} [{}]", game.name, game.platform);
//! }
//! ```

#![cfg_attr(not(windows), allow(dead_code, unused_imports))]

mod engine;
mod error;
mod model;
pub mod music;
mod scanners;
mod util;

pub use engine::GameFindEngine;
pub use error::{GameFindError, Result};
pub use model::{
    Game, LaunchTarget, Platform, PlatformPresence, PlatformStatus, ScanOptions, ScanReport,
    SearchOptions,
};
pub use music::{MusicApp, MusicAppCategory, MusicLaunchTarget, MusicScanReport, MusicScanner};

