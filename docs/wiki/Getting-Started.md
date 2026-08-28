# 🚀 Getting Started with ONYX Launcher

This guide walks you through system requirements, downloading, installing, and running **ONYX Launcher** for the first time.

---

## 📋 System Requirements

| Component | Minimum Requirement | Recommended |
| :--- | :--- | :--- |
| **Operating System** | Windows 10 (64-bit, Build 1809+) | Windows 11 (64-bit) |
| **Architecture** | x64 (AMD64 / Intel 64) | x64 |
| **Runtime** | .NET Desktop Runtime 10.0 | Included in Self-Contained Release |
| **Memory (RAM)** | 2 GB | 4 GB+ |
| **Disk Space** | ~150 MB free space | SSD recommended |
| **Graphics** | DirectX 11 / OpenGL 3.0+ compatible GPU | Modern Dedicated / Integrated GPU |

---

## 📦 Installation Options

Visit the official [GitHub Releases Page](https://github.com/taroxzen/ONYX-Launcher/releases/latest) to download the latest version.

### Option 1: Installer Setup (ONYX_Setup_v1.1.0.exe) — Recommended
1. Download **ONYX_Setup_v1.1.0.exe**.
2. Run the setup wizard (Admin rights are **not** required; it installs safely into %LOCALAPPDATA%\Programs\ONYX Launcher).
3. Select your language and check whether you want a Desktop Shortcut and Windows Startup option.
4. Click **Install**. ONYX Launcher will be ready with Start Menu shortcuts.

### Option 2: Portable Package (ONYX-Launcher-v1.1.0-win-x64.zip)
1. Download **ONYX-Launcher-v1.1.0-win-x64.zip**.
2. Extract the archive into any folder of your choice (e.g. C:\Games\ONYX or D:\Tools\ONYX).
3. Double-click **Onyx.Avalonia.exe** to launch directly without any installation steps.

---

## 🎮 First Launch & Automatic Library Discovery

1. When ONYX Launcher starts, it will automatically invoke the Rust game scanner.
2. Within milliseconds, installed games from detected platforms (**Steam, Epic, EA, Ubisoft, GOG, Battle.net, Xbox, Minecraft, Metin2**) will appear in your library.
3. High-resolution logos and artworks will be downloaded in the background from SteamGridDB and Open CDN pipelines.
4. Click on any game card to launch it directly, or click **"OYNA / PLAY"** from the hero banner!

---

## 🔔 System Tray & Background Operation

* Clicking the **Close (X)** or minimizing the window keeps ONYX Launcher active in the Windows notification tray.
* Double-clicking the tray icon restores the window instantly.
* Right-clicking the tray icon opens quick actions (Open Launcher, Exit) translated into your selected language.