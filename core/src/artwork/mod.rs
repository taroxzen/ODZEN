// ============================================================================
// ODZEN Core — High-Performance Artwork & Logo Engine (Rust)
// Developed by Taroxzen (https://github.com/taroxzen)
// Copyright (c) 2026 Taroxzen. All rights reserved.
// Licensed under the MIT License.
// ============================================================================

use std::fs::{self, File};
use std::path::{Path, PathBuf};
use std::time::Duration;

use image::imageops::FilterType;
use image::{ImageFormat, Rgba, RgbaImage};
use reqwest::blocking::Client;
use serde_json::Value;

pub struct ArtworkEngine {
    client: Client,
    storage_dir: PathBuf,
}

impl ArtworkEngine {
    pub fn new() -> Self {
        let storage_dir = default_storage_dir();
        let _ = fs::create_dir_all(&storage_dir);

        let client = Client::builder()
            .timeout(Duration::from_secs(8))
            .user_agent("ODZEN-ArtworkEngine/1.3.0 (+https://github.com/taroxzen/ODZEN)")
            .build()
            .unwrap_or_else(|_| Client::new());

        Self {
            client,
            storage_dir,
        }
    }

    pub fn resolve_and_download(&self, id: &str, name: &str, platform: &str, store_id: Option<&str>) -> bool {
        let safe_id = sanitize_filename(id);
        let target_path = self.storage_dir.join(format!("{safe_id}.png"));

        // Validasyon 1: Dosya zaten varsa ve boyutu 0'dan büyükse başarılı dön
        if target_path.is_file() {
            if let Ok(meta) = fs::metadata(&target_path) {
                if meta.len() > 100 {
                    return true;
                }
            }
            let _ = fs::remove_file(&target_path);
        }

        let clean_name = clean_title(name);

        // 0. Doğrudan Eşleşen Yüksek Çözünürlüklü Küratörlü CDN Logoları
        if let Some(curated_url) = get_curated_logo_url(&clean_name) {
            if self.download_and_process(curated_url, &target_path) {
                return true;
            }
        }

        // 1. Steam Direct Store ID
        if platform.eq_ignore_ascii_case("steam") {
            if let Some(sid) = store_id {
                let steam_url = format!("https://cdn.cloudflare.steamstatic.com/steam/apps/{sid}/logo.png");
                if self.download_and_process(&steam_url, &target_path) {
                    return true;
                }
            }
        }

        // 2. Steam Store Search API
        let search_url = format!(
            "https://store.steampowered.com/api/storesearch/?term={}&l=english&cc=US",
            urlencoding(&clean_name)
        );
        if let Ok(resp) = self.client.get(&search_url).send() {
            if resp.status().is_success() {
                if let Ok(json_text) = resp.text() {
                    if let Ok(val) = serde_json::from_str::<Value>(&json_text) {
                        if let Some(items) = val.get("items").and_then(|i| i.as_array()) {
                            if let Some(first) = items.first() {
                                if let Some(app_id) = first.get("id").and_then(|id| id.as_i64()) {
                                    let url = format!("https://cdn.cloudflare.steamstatic.com/steam/apps/{app_id}/logo.png");
                                    if self.download_and_process(&url, &target_path) {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // 3. Wikipedia Open API (PageImages)
        let wiki_url = format!(
            "https://en.wikipedia.org/w/api.php?action=query&titles={}&prop=pageimages&format=json&pithumbsize=600",
            urlencoding(&clean_name)
        );
        if let Ok(resp) = self.client.get(&wiki_url).send() {
            if resp.status().is_success() {
                if let Ok(json_text) = resp.text() {
                    if let Ok(val) = serde_json::from_str::<Value>(&json_text) {
                        if let Some(pages) = val.pointer("/query/pages").and_then(|p| p.as_object()) {
                            for (_k, page) in pages {
                                if let Some(src) = page.pointer("/thumbnail/source").and_then(|s| s.as_str()) {
                                    if self.download_and_process(src, &target_path) {
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // 4. DuckDuckGo Instant API
        let ddg_url = format!(
            "https://api.duckduckgo.com/?q={}+video+game&format=json",
            urlencoding(&clean_name)
        );
        if let Ok(resp) = self.client.get(&ddg_url).send() {
            if resp.status().is_success() {
                if let Ok(json_text) = resp.text() {
                    if let Ok(val) = serde_json::from_str::<Value>(&json_text) {
                        if let Some(img) = val.get("Image").and_then(|i| i.as_str()) {
                            if !img.is_empty() {
                                let full_url = if img.starts_with("http") {
                                    img.to_string()
                                } else {
                                    format!("https://duckduckgo.com{img}")
                                };
                                if self.download_and_process(&full_url, &target_path) {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
        }

        false
    }

    fn download_and_process(&self, url: &str, target_path: &Path) -> bool {
        let Ok(resp) = self.client.get(url).send() else {
            return false;
        };
        if !resp.status().is_success() {
            return false;
        }
        let Ok(bytes) = resp.bytes() else {
            return false;
        };
        if bytes.len() < 200 {
            return false;
        }

        let Ok(img) = image::load_from_memory(&bytes) else {
            return false;
        };

        if img.width() < 8 || img.height() < 8 {
            return false;
        }

        let rgba = img.to_rgba8();
        let cropped = auto_crop_transparent_pixels(&rgba);
        let final_canvas = fit_and_center_canvas(&cropped, 512, 280);

        if let Ok(mut out_file) = File::create(target_path) {
            if final_canvas.write_to(&mut out_file, ImageFormat::Png).is_ok() {
                return true;
            }
            // Yazma hatası oluştuysa bozuk dosyayı temizle
            let _ = fs::remove_file(target_path);
        }

        false
    }
}

fn get_curated_logo_url(title: &str) -> Option<&'static str> {
    let t = title.to_lowercase();
    match t.as_str() {
        "valorant" => Some("https://cdn2.steamgriddb.com/logo/7c3ad1efdb58bc59e87515ee3c02ca4a.png"),
        "ea sports fc 26" | "fc 26" | "ea sports fc 25" | "fc 25" => Some("https://cdn2.steamgriddb.com/logo/439294aeec82cba1b9d4f09d84637651.png"),
        "fortnite" => Some("https://cdn2.steamgriddb.com/logo/5a4a5840caec0e026117b18e7e1136b6.png"),
        "the finals" => Some("https://cdn2.steamgriddb.com/logo/4908990ca385cf5ec7ca6c1b3f71c4c8.png"),
        "dirt 5" => Some("https://cdn2.steamgriddb.com/logo/d8f07096e2be6b5f4be8cce1a7c50a1d.png"),
        "donut county" => Some("https://cdn2.steamgriddb.com/logo/5e7ce9633e3878b30d31e94ba32e3a13.png"),
        "marvel rivals" => Some("https://cdn.cloudflare.steamstatic.com/steam/apps/2767030/logo.png"),
        "project zomboid" => Some("https://cdn.cloudflare.steamstatic.com/steam/apps/108600/logo.png"),
        "grand theft auto: san andreas" | "gta san andreas" | "gta: san andreas" => Some("https://cdn2.steamgriddb.com/logo/6226ea51c360be1b6c7a31f6f8ba29d6.png"),
        "rinamt2" | "metin2" => Some("https://assets.metin2.dev/logo/metin2_logo_hd.png"),
        _ => None,
    }
}

fn auto_crop_transparent_pixels(img: &RgbaImage) -> RgbaImage {
    let (width, height) = img.dimensions();
    let mut min_x = width;
    let mut min_y = height;
    let mut max_x = 0;
    let mut max_y = 0;
    let mut has_visible = false;

    for y in 0..height {
        for x in 0..width {
            let pixel = img.get_pixel(x, y);
            if pixel[3] > 15 {
                has_visible = true;
                if x < min_x { min_x = x; }
                if x > max_x { max_x = x; }
                if y < min_y { min_y = y; }
                if y > max_y { max_y = y; }
            }
        }
    }

    if !has_visible || min_x >= max_x || min_y >= max_y {
        return img.clone();
    }

    let crop_w = (max_x - min_x + 1).min(width);
    let crop_h = (max_y - min_y + 1).min(height);

    image::imageops::crop_imm(img, min_x, min_y, crop_w, crop_h).to_image()
}

fn fit_and_center_canvas(cropped: &RgbaImage, canvas_w: u32, canvas_h: u32) -> RgbaImage {
    let src_w = cropped.width() as f32;
    let src_h = cropped.height() as f32;

    let scale_x = canvas_w as f32 / src_w;
    let scale_y = canvas_h as f32 / src_h;
    let scale = scale_x.min(scale_y).min(1.0);

    let new_w = (src_w * scale).round() as u32;
    let new_h = (src_h * scale).round() as u32;

    let resized = image::imageops::resize(cropped, new_w.max(1), new_h.max(1), FilterType::Lanczos3);

    let mut canvas = RgbaImage::from_pixel(canvas_w, canvas_h, Rgba([0, 0, 0, 0]));
    let offset_x = (canvas_w - new_w) / 2;
    let offset_y = (canvas_h - new_h) / 2;

    image::imageops::overlay(&mut canvas, &resized, offset_x as i64, offset_y as i64);
    canvas
}

fn default_storage_dir() -> PathBuf {
    #[cfg(windows)]
    {
        if let Ok(appdata) = std::env::var("LOCALAPPDATA") {
            return PathBuf::from(appdata).join("ODZEN").join("artwork").join("logos");
        }
    }
    #[cfg(not(windows))]
    {
        if let Ok(home) = std::env::var("HOME") {
            return PathBuf::from(home).join(".local").join("share").join("ODZEN").join("artwork").join("logos");
        }
    }
    PathBuf::from("artwork").join("logos")
}

fn clean_title(title: &str) -> String {
    let noise = [
        "Digital Deluxe", "Collector's Edition", "Gold Edition",
        "Ultimate Edition", "Game of the Year", "GOTY Edition",
        "Remastered", "Director's Cut", "Steam Edition",
        "Bedrock Edition", "Java Edition", "Standard Edition",
        "(TM)", "(R)", "®", "™"
    ];
    let mut cleaned = title.to_string();
    for n in noise {
        cleaned = cleaned.replace(n, "");
    }
    cleaned.trim().to_string()
}

fn sanitize_filename(name: &str) -> String {
    name.chars()
        .map(|c| if c.is_alphanumeric() || c == '_' || c == '-' { c } else { '_' })
        .collect()
}

fn urlencoding(s: &str) -> String {
    let mut out = String::new();
    for b in s.bytes() {
        match b {
            b'a'..=b'z' | b'A'..=b'Z' | b'0'..=b'9' | b'-' | b'_' | b'.' | b'~' => {
                out.push(b as char);
            }
            b' ' => out.push('+'),
            _ => out.push_str(&format!("%{:02X}", b)),
        }
    }
    out
}
