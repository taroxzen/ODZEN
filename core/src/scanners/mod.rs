// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
mod amazon;
mod battlenet;
mod ea;
mod epic;
mod gog;
mod local;
mod metin2;
mod minecraft;
mod riot;
mod rockstar;
mod steam;
mod ubisoft;
mod xbox;

use crate::error::Result;
use crate::model::{Game, Platform, ScanOptions};

/// Trait every platform scanner implements.
pub trait Scanner: Send + Sync {
    fn platform(&self) -> Platform;

    /// Cheap probe: is the launcher / data source present?
    fn is_available(&self) -> bool;

    /// Discover installed games.
    fn scan(&self, options: &ScanOptions) -> Result<Vec<Game>>;
}

pub fn all_scanners() -> Vec<Box<dyn Scanner>> {
    vec![
        Box::new(steam::SteamScanner),
        Box::new(epic::EpicScanner),
        Box::new(xbox::XboxScanner),
        Box::new(ea::EaScanner),
        Box::new(riot::RiotScanner),
        Box::new(rockstar::RockstarScanner),
        Box::new(minecraft::MinecraftScanner),
        Box::new(battlenet::BattleNetScanner),
        Box::new(ubisoft::UbisoftScanner),
        Box::new(gog::GogScanner),
        Box::new(amazon::AmazonScanner),
        Box::new(metin2::Metin2Scanner),
        // Local runs last in registration; engine still filters against store paths
        Box::new(local::LocalScanner),
    ]
}

pub fn scanner_for(platform: Platform) -> Option<Box<dyn Scanner>> {
    all_scanners()
        .into_iter()
        .find(|s| s.platform() == platform)
}
