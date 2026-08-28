# 🎨 Artwork & Customization Pipeline

ONYX Launcher includes a multi-tiered artwork pipeline that ensures every game in your library has crisp, high-resolution visual branding.

---

## 🖼️ Multi-Tier Artwork Resolution Pipeline

`mermaid
graph TD
    Game[Detected Game] --> CheckLocal[1. Local High-Res Icon/Asset Extraction]
    CheckLocal -->|Found| Cache[Save to %LOCALAPPDATA%/ONYX/icons]
    CheckLocal -->|Not Found| CheckSteamGrid[2. SteamGridDB Open API Engine]
    CheckSteamGrid -->|Found| Crop[3. SkiaSharp Smart Transparent Crop]
    CheckSteamGrid -->|Not Found| CheckDuckDuckGo[4. DuckDuckGo PNG Logo Scraper]
    CheckDuckDuckGo -->|Found| Crop
    CheckDuckDuckGo -->|Not Found| BuiltIn[5. Built-in High-Res Vector SVG Platform Icons]
    Crop --> Cache
    BuiltIn --> Render[Render Avalonia Bitmap]
    Cache --> Render
`

---

## ✂️ SkiaSharp Smart Transparent Cropping

Many game logos downloaded from public web sources contain unwanted empty transparent margins or black rectangular borders.

ONYX Launcher includes an integrated **SkiaSharp** post-processing filter:
1. Iterates over bitmap pixels to determine the precise bounding box [minX, minY, maxX, maxY] of visible, non-transparent pixels.
2. Crops out redundant transparent padding.
3. Automatically normalizes aspect ratios so logos look visually aligned inside cards and banners.

---

## 💾 Local Cache Directories

Artwork is cached locally to prevent redundant network requests:
* **Game Icons:** %LOCALAPPDATA%\ONYX\icons\
* **SteamGridDB Logos:** %LOCALAPPDATA%\ONYX\steamgriddb_logos\
* **Cloud Cached Art:** %LOCALAPPDATA%\ONYX\cloud_artwork\

All filenames are strictly sanitized using alphanumeric regex and SHA-256 to prevent path traversal.