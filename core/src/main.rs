// ============================================================================
// ODZEN Core — Unified Rust Engine (Scanner, Artwork, Launcher, Sysinfo)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
use std::io::{self, Write};
use std::process::ExitCode;

use clap::{Parser, Subcommand};
use odzen_core::{GameFindEngine, Platform, ScanOptions, SearchOptions};

#[derive(Parser, Debug)]
#[command(
    name = "odzen-core",
    version,
    about = "Unified Rust Core Engine for ODZEN (Scanner, 4K Artwork, Game Launcher, System Diagnostics)",
    long_about = "High-performance native backend for ODZEN. Use --json as integration surface."
)]
struct Cli {
    #[command(subcommand)]
    command: Commands,
}

#[derive(Subcommand, Debug)]
enum Commands {
    /// Scan installed games across all platforms (Steam, Epic, EA, Riot, Metin2, Local)
    Scan {
        /// Output JSON (primary integration surface for UIs)
        #[arg(long)]
        json: bool,
        /// Comma-separated platforms: steam,epic,xbox,ea,riot,rockstar,minecraft,metin2
        #[arg(long, short = 'p')]
        platform: Option<String>,
        /// Include Steam tools / redistributables
        #[arg(long)]
        include_tools: bool,
        /// Compute install folder sizes (slower)
        #[arg(long)]
        sizes: bool,
        /// Extra local folders to scan (repeatable)
        #[arg(long)]
        folder: Vec<std::path::PathBuf>,
    },
    /// Search games (scans first, then filters)
    Search {
        query: String,
        #[arg(long)]
        json: bool,
        #[arg(long, short = 'p')]
        platform: Option<String>,
        #[arg(long, short = 'n', default_value_t = 50)]
        limit: usize,
    },
    /// List which launchers/platforms are detected
    Platforms {
        #[arg(long)]
        json: bool,
    },
    /// Resolve and download HD/4K transparent logo for a game
    Artwork {
        /// Game unique ID
        #[arg(long)]
        id: String,
        /// Game title / display name
        #[arg(long)]
        name: String,
        /// Platform (e.g. steam, epic, ea, local, metin2)
        #[arg(long, default_value = "local")]
        platform: String,
        /// Store App ID (optional, e.g. 730 for CS2)
        #[arg(long)]
        store_id: Option<String>,
        /// Output JSON response
        #[arg(long)]
        json: bool,
    },
    /// Launch a game directly or via official launcher protocol
    Launch {
        /// Game executable or protocol URI
        #[arg(long)]
        target: String,
        /// Launch type: "protocol" or "executable"
        #[arg(long, default_value = "executable")]
        launch_type: String,
        /// Optional working directory
        #[arg(long)]
        work_dir: Option<std::path::PathBuf>,
        /// Arguments
        #[arg(long)]
        arg: Vec<String>,
    },
    /// System and background process diagnostic
    Sysinfo {
        #[arg(long)]
        json: bool,
    },
    /// Scan music applications and streaming services on Windows
    Music {
        #[arg(long)]
        json: bool,
    },
    /// Launch a music application by ID (Desktop app if installed, else Web Player)
    LaunchMusic {
        #[arg(long)]
        id: String,
    },
    /// Metin Search: Metin2 yan sunucularını ve PvP istemcilerini tara
    MetinSearch {
        #[arg(long)]
        json: bool,
        /// Compute install folder sizes (slower)
        #[arg(long)]
        sizes: bool,
    },
}

fn main() -> ExitCode {
    if let Err(e) = run() {
        eprintln!("error: {e:#}");
        return ExitCode::FAILURE;
    }
    ExitCode::SUCCESS
}

fn run() -> anyhow::Result<()> {
    let cli = Cli::parse();
    let engine = GameFindEngine::new();

    match cli.command {
        Commands::Scan {
            json,
            platform,
            include_tools,
            sizes,
            folder,
        } => {
            let platforms = parse_platforms(platform.as_deref())?;
            let options = ScanOptions {
                platforms,
                include_tools,
                compute_size: sizes,
                extra_folders: folder,
            };
            let report = engine.scan(options)?;
            if json {
                println!("{}", serde_json::to_string_pretty(&report)?);
            } else {
                print_human_scan(&report);
            }
        }
        Commands::Search {
            query,
            json,
            platform,
            limit,
        } => {
            let platforms = parse_platforms(platform.as_deref())?;
            let report = engine.scan(ScanOptions {
                platforms: platforms.clone(),
                ..Default::default()
            })?;
            let platform_filter = if platforms.len() == 1 {
                Some(platforms[0])
            } else {
                None
            };
            let hits = engine.search_in(
                &report.games,
                &query,
                SearchOptions {
                    platform: platform_filter,
                    limit: Some(limit),
                },
            );
            if json {
                let owned: Vec<_> = hits.into_iter().cloned().collect();
                println!("{}", serde_json::to_string_pretty(&owned)?);
            } else {
                println!("Found {} match(es) for \"{query}\":\n", hits.len());
                for g in hits {
                    println!(
                        "  {:<40} [{:<10}] {}",
                        truncate(&g.name, 40),
                        g.platform.as_str(),
                        g.id
                    );
                }
            }
        }
        Commands::Platforms { json } => {
            let statuses = engine.detect_platforms();
            if json {
                println!("{}", serde_json::to_string_pretty(&statuses)?);
            } else {
                println!("Platform detection:\n");
                for s in statuses {
                    println!(
                        "  {:<12} {:?}",
                        s.platform.as_str(),
                        s.status
                    );
                }
            }
        }
        Commands::Artwork {
            id,
            name,
            platform,
            store_id,
            json,
        } => {
            let engine = odzen_core::ArtworkEngine::new();
            let success = engine.resolve_and_download(&id, &name, &platform, store_id.as_deref());
            if json {
                println!(
                    "{}",
                    serde_json::json!({
                        "id": id,
                        "name": name,
                        "platform": platform,
                        "success": success
                    })
                );
            } else if success {
                println!("✅ Logo successfully resolved and saved for: {name}");
            } else {
                println!("✕ Logo could not be resolved for: {name}");
            }
        }
        Commands::Launch {
            target,
            launch_type,
            work_dir,
            arg,
        } => {
            #[cfg(windows)]
            {
                use std::process::Command;
                if launch_type.eq_ignore_ascii_case("protocol") {
                    Command::new("cmd")
                        .args(["/c", "start", "", &format!("\"{target}\"")])
                        .spawn()?;
                } else {
                    let mut cmd = Command::new(&target);
                    if let Some(wd) = work_dir {
                        cmd.current_dir(wd);
                    } else if let Some(parent) = std::path::Path::new(&target).parent() {
                        cmd.current_dir(parent);
                    }
                    cmd.args(&arg);
                    cmd.spawn()?;
                }
                println!("🚀 Launched: {target}");
            }
            #[cfg(not(windows))]
            {
                use std::process::Command;
                if launch_type.eq_ignore_ascii_case("protocol") {
                    Command::new("xdg-open").arg(&target).spawn()?;
                } else {
                    let mut cmd = Command::new(&target);
                    if let Some(wd) = work_dir {
                        cmd.current_dir(wd);
                    } else if let Some(parent) = std::path::Path::new(&target).parent() {
                        cmd.current_dir(parent);
                    }
                    cmd.args(&arg);
                    cmd.spawn()?;
                }
                println!("🚀 Launched: {target}");
            }
        }
        Commands::Sysinfo { json } => {
            let os = std::env::consts::OS;
            let arch = std::env::consts::ARCH;
            let num_cpus = std::thread::available_parallelism()
                .map(|p| p.get())
                .unwrap_or(1);
            if json {
                println!(
                    "{}",
                    serde_json::json!({
                        "os": os,
                        "arch": arch,
                        "logical_cores": num_cpus,
                        "engine_version": "1.3.0",
                        "core_type": "odzen-unified-rust-core"
                    })
                );
            } else {
                println!("ODZEN Core Sysinfo: OS={os}, Arch={arch}, Cores={num_cpus}, Version=1.3.0");
            }
        }
        Commands::Music { json } => {
            let scanner = odzen_core::MusicScanner::new();
            let report = scanner.scan();
            if json {
                println!("{}", serde_json::to_string_pretty(&report)?);
            } else {
                println!(
                    "ODZEN Müzik Bulucu — {} uygulama ({}/{} yüklü) [{} ms]\n",
                    report.total_count, report.installed_count, report.total_count, report.duration_ms
                );
                for app in &report.apps {
                    let status_str = if app.is_installed {
                        "🟢 Masaüstü Uygulaması (Yüklü)"
                    } else {
                        "🌐 Web Player (Tarayıcı)"
                    };
                    println!("  {:<25} {:<30} {}", app.name, status_str, app.id);
                }
            }
        }
        Commands::LaunchMusic { id } => {
            let scanner = odzen_core::MusicScanner::new();
            let report = scanner.scan();
            if let Some(app) = report.apps.into_iter().find(|a| a.id.eq_ignore_ascii_case(&id)) {
                let msg = app.launch().map_err(|e| anyhow::anyhow!(e))?;
                println!("{msg}");
            } else {
                anyhow::bail!("Müzik uygulaması bulunamadı: '{id}'");
            }
        }
        Commands::MetinSearch { json, sizes } => {
            let options = ScanOptions {
                platforms: vec![Platform::Metin2],
                include_tools: false,
                compute_size: sizes,
                extra_folders: Vec::new(),
            };
            let report = engine.scan(options)?;
            if json {
                println!("{}", serde_json::to_string_pretty(&report)?);
            } else {
                println!("🔎 Metin Search (Metin2 Yan Sunucu Taraması)\n");
                print_human_scan(&report);
            }
        }
    }

    Ok(())
}


fn parse_platforms(spec: Option<&str>) -> anyhow::Result<Vec<Platform>> {
    match spec {
        None => Ok(Platform::ALL.to_vec()),
        Some(s) => {
            let list = Platform::parse_list(s);
            if list.is_empty() {
                anyhow::bail!(
                    "no valid platforms in '{s}'. use: steam,epic,xbox,ea,riot,rockstar,minecraft,metin2"
                );
            }
            Ok(list)
        }
    }
}

fn print_human_scan(report: &odzen_core::ScanReport) {
    let mut out = io::stdout().lock();
    let _ = writeln!(
        out,
        "gamefind — {} game(s) in {} ms\n",
        report.games.len(),
        report.duration_ms
    );

    for p in &report.platforms {
        let _ = writeln!(
            out,
            "  {:<12} {:?} ({} games){}",
            p.platform.as_str(),
            p.status,
            p.game_count,
            p.message
                .as_ref()
                .map(|m| format!(" — {m}"))
                .unwrap_or_default()
        );
    }
    let _ = writeln!(out);

    if !report.warnings.is_empty() {
        let _ = writeln!(out, "Warnings:");
        for w in &report.warnings {
            let _ = writeln!(out, "  - {w}");
        }
        let _ = writeln!(out);
    }

    for g in &report.games {
        let path = g
            .install_path
            .as_ref()
            .map(|p| p.display().to_string())
            .unwrap_or_else(|| "-".into());
        let _ = writeln!(
            out,
            "  {:<40} [{:<10}] {}",
            truncate(&g.name, 40),
            g.platform.as_str(),
            truncate(&path, 60)
        );
    }
}

fn truncate(s: &str, max: usize) -> String {
    if s.chars().count() <= max {
        return s.to_string();
    }
    let t: String = s.chars().take(max.saturating_sub(1)).collect();
    format!("{t}…")
}
