# 🦀 ODZEN Core (odzen-core)

> Unified High-Performance Native Core Engine for **ODZEN Gaming Platform**  
> Written in **Rust (2024 Edition)** • Multi-Threaded • Zero External Runtime Dependencies

---

## 🌟 Overview

`odzen-core` is the native engine powering ODZEN's game discovery, 4K artwork pipeline, process execution, and system diagnostics. It provides both a high-performance **Rust Library (`odzen_core`)** and a **Standalone CLI (`odzen-core.exe`)** with JSON output designed for seamless integration with frontend clients (Avalonia C#, Tauri, Web).

---

## 📂 Architecture & Directory Structure

```text
core/
├── Cargo.toml                           # Rust 2024 Edition Package Manifest
├── README.md                            # Core Documentation
└── src/
    ├── main.rs                          # CLI Entry Point & Subcommand Router
    ├── lib.rs                           # Clean Library API Exports
    ├── error.rs                         # Error Handling & Result Types
    │
    ├── models/                          # Data Structures & Protocols
    │   └── mod.rs                       # Game, LaunchTarget, Platform, ScanReport
    │
    ├── scanner/                         # 1. Multi-Platform Game Discovery Engine
    │   ├── mod.rs                       # Parallel Scanner Coordinator (Rayon)
    │   ├── engine.rs                    # GameFindEngine implementation
    │   ├── steam.rs                     # Steam VDF / AppManifest parser
    │   ├── epic.rs                      # Epic Games Manifest parser
    │   ├── ea.rs                        # EA App & Origin parser
    │   ├── riot.rs                      # Riot Games client & embedded SQLite parser
    │   ├── xbox.rs                      # Xbox / Microsoft Store parser
    │   ├── gog.rs                       # GOG Galaxy parser
    │   ├── ubisoft.rs                   # Ubisoft Connect parser
    │   ├── battlenet.rs                 # Battle.net product.db parser
    │   ├── rockstar.rs                  # Rockstar Games Launcher parser
    │   ├── minecraft.rs                 # Minecraft Java, Bedrock & Modded Launchers
    │   ├── metin2.rs                    # Metin2 Private Server / Client detector
    │   └── local.rs                     # Local drive heuristic executable scanner
    │
    ├── artwork/                         # 2. 4K Artwork & Transparent Media Engine
    │   └── mod.rs                       # SIMD Transparency Cropper & Multi-Tier CDN Fetcher
    │
    ├── launcher/                        # 3. Process & Protocol Execution Engine
    │   └── mod.rs                       # Native Executable & Protocol Launchers
    │
    ├── sysinfo/                         # 4. Hardware & System Diagnostics
    │   └── mod.rs                       # OS, Architecture & Core Detection
    │
    ├── music/                           # 5. Music Integration
    │   └── mod.rs                       # Desktop Apps & Web Players (Spotify, YouTube, etc.)
    │
    └── util/                            # 6. Low-Level System Utilities
        ├── mod.rs
        ├── paths.rs                     # Cross-drive resolution & sanitization
        ├── registry.rs                  # Windows Registry scanner helpers
        └── vdf.rs                       # Valve KeyValues (VDF) text parser
```

---

## 🚀 CLI Commands & Usage

### 1. Full Multi-Platform Game Scan
```bash
odzen-core.exe scan --json
```

### 2. Specific Platform Filter Scan
```bash
odzen-core.exe scan -p steam,epic,riot,local --json
```

### 3. HD/4K Transparent Artwork Logo Resolver
```bash
odzen-core.exe artwork --id "steam:730" --name "Counter-Strike 2" --platform "steam" --store-id "730" --json
```

### 4. Game & Launcher Protocol Execution
```bash
odzen-core.exe launch --target "steam://rungameid/730" --launch-type "protocol"
```

### 5. Hardware Diagnostics
```bash
odzen-core.exe sysinfo --json
```

---

## 🛠️ Building from Source

### Prerequisites
- [Rust 1.85+](https://rustup.rs/) (Rust 2024 Edition support)

### Build Release Binary
```bash
cargo build --release
```

The optimized binary will be located at `target/release/odzen-core.exe`.

---

## 📄 License
Licensed under the [MIT License](../LICENSE).
