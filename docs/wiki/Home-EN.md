> 🇬🇧 **English** | 🇹🇷 [[Home|Türkçe'ye Geç]]

# 🎮 Welcome to the ONYX Launcher Wiki

Welcome to the comprehensive documentation for **ONYX Launcher** — an ultra-modern, cyberpunk-themed universal game launcher and platform hub engineered for Windows 10 & 11.

---

## 🌟 What is ONYX Launcher?

ONYX Launcher unifies your scattered game libraries from multiple storefronts, local folders, private servers, and emulators into a single, blazing-fast cyberpunk cockpit. It combines a sleek **Avalonia UI (.NET 10)** frontend with an ultra-optimized **Rust core engine (onyx-game-scanner)** capable of scanning hundreds of games across 10+ platforms in under a second.

`mermaid
graph TD
    UI[Avalonia UI Frontend - C# .NET 10] -->|JSON IPC Stream| RustCore[Rust Game Scanner Core]
    UI --> ArtEngine[Dynamic High-Res Artwork Pipeline]
    UI --> HwMon[Real-time Hardware Monitor]
    UI --> MusicHub[Integrated Streaming Audio Hub]
    RustCore --> Steam[Steam VDF & AppManifest]
    RustCore --> Epic[Epic Games Manifests]
    RustCore --> EA[EA App & Origin Data]
    RustCore --> Ubi[Ubisoft Connect Registry]
    RustCore --> GOG[GOG Galaxy Database]
    RustCore --> BNet[Battle.net ProductDB]
    RustCore --> Xbox[Windows Apps & Xbox Registry]
    RustCore --> Local[Local Drives & Custom Folders]
`

---

## 📚 Wiki Navigation

| Guide | Description |
| :--- | :--- |
| 🚀 **[[Getting-Started-EN|Getting Started]]** | System requirements, installation methods (Installer vs Portable), and first launch. |
| ⚙️ **[[Features-&-Architecture-EN|Features & Architecture]]** | Deep dive into the architecture, MVVM design, Rust scanning engine, and memory optimization. |
| 🕹️ **[[Supported-Platforms-EN|Supported Platforms]]** | Detailed list of supported platforms (Steam, Epic, EA, Ubisoft, GOG, Battle.net, Xbox, Minecraft, etc.). |
| 🎨 **[[Artwork-&-Customization-EN|Artwork & Customization]]** | High-res logo retrieval, smart transparent border cropping with SkiaSharp, and custom covers. |
| 📊 **[[Hardware-Monitor-&-Performance-EN|Hardware Monitor & Performance]]** | Sidebar CPU, RAM, GPU, and VRAM monitoring, zero-overhead idle trimming. |
| 🛡️ **[[Security-&-Privacy-EN|Security & Privacy]]** | Security audit details, script injection prevention, path traversal defense, and local privacy. |
| ❓ **[[Troubleshooting-&-FAQ-EN|Troubleshooting & FAQ]]** | Solutions for missing games, scanner paths, notification troubleshooting, and FAQs. |

---

## ⚡ Quick Specifications

* **Operating System:** Windows 10 / 11 (64-bit)
* **Frontend Framework:** Avalonia UI 11.2 (.NET 10 / Modern C#)
* **Backend Core Engine:** Rust (edition 2021)
* **Memory Footprint:** ~35–65 MB active (trims to <20 MB in tray)
* **Scan Latency:** ~100–350 ms for 200+ installed games
* **Languages Supported:** English, Turkish, German, French, Spanish, Russian, Dutch, Bulgarian