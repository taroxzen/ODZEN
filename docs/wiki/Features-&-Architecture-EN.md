> 🇬🇧 **English** | 🇹🇷 [[Features-&-Architecture|Türkçe'ye Geç]]

# ⚙️ Features & Architecture

ONYX Launcher is architected with a decoupled, high-performance hybrid model:
1. **Frontend Presentation:** C# .NET 10 using Avalonia UI and CommunityToolkit.Mvvm.
2. **Core Scanning Engine:** Native binary written in Rust (onyx-game-scanner.exe).

---

## 🏗️ Architectural Diagram

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

## 🧩 Core Subsystems

### 1. The Rust Scanner Engine (onyx-game-scanner)
* Zero overhead native execution reading Windows Registry, Steam VDF, and manifest databases.
* Streams clean JSON adhering to strict models.

### 2. Avalonia UI Frontend & Reactive MVVM
* Cross-resolution responsive Cyberpunk UI with SVG vectors.
* Item virtualization ensures smooth 60–144 FPS scrolling with 1,000+ games.

### 3. Memory Optimizer Service
* Automatically trims memory footprint to <20 MB when minimized to the notification tray.