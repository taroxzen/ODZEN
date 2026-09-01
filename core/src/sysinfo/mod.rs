// ============================================================================
// ODZEN Core — System & Hardware Diagnostics Engine (Rust)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================

use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SysInfoReport {
    pub os: String,
    pub arch: String,
    pub logical_cores: usize,
    pub version: String,
}

pub struct SysinfoEngine;

impl SysinfoEngine {
    pub fn get_info() -> SysInfoReport {
        SysInfoReport {
            os: std::env::consts::OS.to_string(),
            arch: std::env::consts::ARCH.to_string(),
            logical_cores: std::thread::available_parallelism().map(|n| n.get()).unwrap_or(1),
            version: env!("CARGO_PKG_VERSION").to_string(),
        }
    }
}
