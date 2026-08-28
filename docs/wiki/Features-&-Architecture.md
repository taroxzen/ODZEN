# ⚙️ Features & Architecture

ONYX Launcher is architected with a decoupled, high-performance hybrid model:
1. **Frontend Presentation & Interaction:** C# .NET 10 using Avalonia UI and CommunityToolkit.Mvvm.
2. **Core Scanning & Registry Extraction Engine:** Standalone native binary written in Rust (onyx-game-scanner.exe).

---

## 🏗️ Architectural Overview

`mermaid
sequenceDiagram
    participant UI as Avalonia Frontend (C#)
    participant Core as Scanner Core (Rust)
    participant Disk as OS Filesystem / Registry
    participant CDN as SteamGridDB / Cloud CDN

    UI->>Core: Process.Start("onyx-game-scanner.exe scan --json")
    Core->>Disk: Query Steam VDF, Registry HKLM/HKCU, SQLite DBs
    Disk-->>Core: Raw Platform Data
    Core-->>UI: Structured Game JSON Stream
    UI->>UI: Bind to ObservableCollection<GameItem>
    UI->>CDN: Async Logo & Artwork Pipeline
    CDN-->>UI: Cache PNGs & Render SkiaSharp Bitmaps
`

---

## 🧩 Key Subsystems

### 1. The Rust Scanner Engine (onyx-game-scanner)
* **Pure Native Performance:** Compiled to zero-overhead machine code.
* **Direct OS Registry & Filesystem Access:** Reads Windows Registry keys via winreg, parses Valve Data Format (df), deserializes Epic Games .item manifests, and inspects GOG SQLite databases.
* **JSON Streaming:** Outputs clean JSON adhering to strict models for launch targets, executables, categories, and estimated file sizes.

### 2. Avalonia UI Frontend & MVVM Pattern
* **Reactive MVVM:** Powered by CommunityToolkit.Mvvm (ObservableObject, RelayCommand).
* **Cross-Resolution Cyberpunk UI:** Custom vector SVG assets, Fluent styling, neon glow accents, and responsive grid layouts.
* **Virtualization:** High-performance item virtualizing controls ensure silky smooth 60–144 FPS scrolling even with 1,000+ games.

### 3. Memory Optimizer Service (MemoryOptimizerService.cs)
* **Automatic Working Set Trimming:** Leverages SetProcessWorkingSetSize and .NET 10 GC adaptation modes.
* **Minimization Cleanup:** When ONYX Launcher is minimized to the system tray, memory is trimmed immediately, reducing RAM footprint to <20 MB.

### 4. Audio Hub Integration (MusicService.cs)
* Integrated quick-access links for Spotify, Apple Music, YouTube Music, Deezer, and Tidal.
* Launches via hardened Windows undll32 url.dll,FileProtocolHandler calls.