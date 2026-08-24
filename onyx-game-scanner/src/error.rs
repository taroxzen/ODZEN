// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
use thiserror::Error;

/// Library-level error type.
#[derive(Debug, Error)]
pub enum GameFindError {
    #[error("I/O error: {0}")]
    Io(#[from] std::io::Error),

    #[error("JSON error: {0}")]
    Json(#[from] serde_json::Error),

    #[error("XML error: {0}")]
    Xml(String),

    #[error("platform not supported on this OS: {0}")]
    UnsupportedOs(String),

    #[error("{0}")]
    Message(String),
}

pub type Result<T> = std::result::Result<T, GameFindError>;
