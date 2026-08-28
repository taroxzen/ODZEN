> 🇬🇧 **English** | 🇹🇷 [[Artwork-&-Customization|Türkçe'ye Geç]]

# 🎨 Artwork & Customization Pipeline

ONYX Launcher includes a multi-tiered artwork pipeline that ensures every game in your library has crisp, high-resolution visual branding.

---

## 🖼️ Multi-Tier Artwork Pipeline

`mermaid
graph TD
    Game[Detected Game] --> CheckLocal[1. Local High-Res Icon Extraction]
    CheckLocal -->|Found| Cache[Save to %LOCALAPPDATA%/ONYX/icons]
    CheckLocal -->|Not Found| CheckSteamGrid[2. SteamGridDB Open API]
    CheckSteamGrid -->|Found| Crop[3. SkiaSharp Smart Transparent Crop]
    CheckSteamGrid -->|Not Found| CheckDuckDuckGo[4. DuckDuckGo Logo Scraper]
    CheckDuckDuckGo -->|Found| Crop
    CheckDuckDuckGo -->|Not Found| BuiltIn[5. Built-in Vector Platform Icons]
    Crop --> Cache
    BuiltIn --> Render[Render Avalonia Bitmap]
    Cache --> Render
`

---

## ✂️ SkiaSharp Smart Transparent Cropping

ONYX Launcher includes an integrated **SkiaSharp** post-processing filter that crops out redundant transparent padding from web logos to ensure uniform visual alignment.