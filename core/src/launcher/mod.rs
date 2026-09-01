// ============================================================================
// ODZEN Core — Process & Protocol Launcher Engine (Rust)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================

use std::path::Path;
use std::process::Command;

pub struct LauncherEngine;

impl LauncherEngine {
    pub fn launch(
        target: &str,
        launch_type: &str,
        work_dir: Option<&Path>,
        args: &[String],
    ) -> Result<String, String> {
        let launch_type_lower = launch_type.to_ascii_lowercase();

        if launch_type_lower == "protocol" {
            #[cfg(windows)]
            {
                let mut cmd = Command::new("cmd");
                cmd.args(["/c", "start", "", &format!("\"{target}\"")]);
                cmd.spawn()
                    .map_err(|e| format!("Protocol launch failed: {e}"))?;
                Ok(format!("Protocol launched successfully: {target}"))
            }
            #[cfg(not(windows))]
            {
                let mut cmd = Command::new("xdg-open");
                cmd.arg(target);
                cmd.spawn()
                    .map_err(|e| format!("Protocol launch failed: {e}"))?;
                Ok(format!("Protocol launched successfully: {target}"))
            }
        } else {
            let path = Path::new(target);
            if !path.exists() {
                return Err(format!("Executable not found: {target}"));
            }
            let mut cmd = Command::new(path);
            if let Some(wd) = work_dir {
                cmd.current_dir(wd);
            } else if let Some(parent) = path.parent() {
                cmd.current_dir(parent);
            }
            cmd.args(args);
            cmd.spawn()
                .map_err(|e| format!("Executable launch failed: {e}"))?;
            Ok(format!("Executable launched successfully: {target}"))
        }
    }
}
