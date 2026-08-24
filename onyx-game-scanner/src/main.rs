// ============================================================================
// ONYX Launcher — Game Scanner Engine (Rust Core)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================
use std::io::{self, Write};
use std::process::ExitCode;

use clap::{Parser, Subcommand};
use onyx_game_scanner::{GameFindEngine, Platform, ScanOptions, SearchOptions};

#[derive(Parser, Debug)]
#[command(
    name = "gamefind",
    version,
    about = "Local Windows game discovery engine (Steam, Epic, Xbox, EA, Riot, Rockstar, Minecraft)",
    long_about = "Scans installed games on this PC. UI-agnostic: use --json as the exit point for other apps."
)]
struct Cli {
    #[command(subcommand)]
    command: Commands,
}

#[derive(Subcommand, Debug)]
enum Commands {
    /// Scan installed games
    Scan {
        /// Output JSON (primary integration surface for UIs)
        #[arg(long)]
        json: bool,
        /// Comma-separated platforms: steam,epic,xbox,ea,riot,rockstar,minecraft
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
        Commands::Music { json } => {
            let scanner = onyx_game_scanner::MusicScanner::new();
            let report = scanner.scan();
            if json {
                println!("{}", serde_json::to_string_pretty(&report)?);
            } else {
                println!(
                    "ONYX Müzik Bulucu — {} uygulama ({}/{} yüklü) [{} ms]\n",
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
            let scanner = onyx_game_scanner::MusicScanner::new();
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

fn print_human_scan(report: &onyx_game_scanner::ScanReport) {
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
