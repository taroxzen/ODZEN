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
            .timeout(Duration::from_secs(10))
            .user_agent("ODZEN-ArtworkEngine/1.4.0 (+https://github.com/taroxzen/ODZEN)")
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

        // Validasyon 1: Dosya zaten varsa ve boyutu 100 bayttan büyükse başarılı dön
        if target_path.is_file() {
            if let Ok(meta) = fs::metadata(&target_path) {
                if meta.len() > 100 {
                    return true;
                }
            }
            let _ = fs::remove_file(&target_path);
        }

        let clean_name = clean_title(name);
        if clean_name.is_empty() {
            return false;
        }

        // 0. DOĞRUDAN EŞLEŞEN KÜRATÖRLÜ ÇEVRİMİÇİ LOGOLAR (Riot, Epic, Metin2, Popüler Oyunlar)
        if let Some(curated_url) = get_curated_logo_url(&clean_name, id) {
            if self.download_and_process(curated_url, &target_path) {
                return true;
            }
        }

        // 1. STEAM DOĞRUDAN STORE ID (Eğer platform Steam ve store_id biliniyorsa)
        if platform.eq_ignore_ascii_case("steam") {
            if let Some(sid) = store_id {
                let steam_url = format!("https://cdn.cloudflare.steamstatic.com/steam/apps/{sid}/logo.png");
                if self.download_and_process(&steam_url, &target_path) {
                    return true;
                }
            }
        }

        // 2. WIKIMEDIA COMMONS OPEN MEDIA API (Klasik, Retro ve Steam Dışı Oyunlar İçin Şeffaf Vektör/PNG)
        if self.try_resolve_wikimedia_commons(&clean_name, &target_path) {
            return true;
        }

        // 3. STEAM STORE SEARCH API (SIKI EŞLEŞME GÜVENLİĞİ / STRICT SIMILARITY GUARD İLE)
        // Yanlış devam oyunlarını (örn: NFS Underground 2 yerine NFS Unbound) engeller.
        if self.try_resolve_steam_guarded(&clean_name, &target_path) {
            return true;
        }

        // 4. ROMA RAKAMI / ALTERNATİF SORGULARLA WIKIMEDIA COMMONS & STEAM TEKRAR DENEME
        let alternate_queries = generate_alternate_titles(&clean_name);
        for alt in alternate_queries {
            if self.try_resolve_wikimedia_commons(&alt, &target_path) {
                return true;
            }
            if self.try_resolve_steam_guarded(&alt, &target_path) {
                return true;
            }
        }

        // 5. DUCKDUCKGO INSTANT GAMES API (Son Fallback)
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

    /// Wikimedia Commons MediaWiki API üzerinden şeffaf PNG / SVG logo arar
    fn try_resolve_wikimedia_commons(&self, game_title: &str, target_path: &Path) -> bool {
        // Tek kelimelik veya kısa başlıklar için "video game logo" ekle ki BM kalkınma hedefleri vb. çıkmasın
        let query = if game_title.len() <= 6 || !game_title.contains(' ') {
            format!("{game_title} video game logo")
        } else {
            format!("{game_title} logo")
        };

        if self.query_commons_api(&query, game_title, target_path) {
            return true;
        }

        // Eğer bulunamadıysa ve "video game logo" denenmediyse, video game logo ile tekrar dene
        if !query.contains("video game") {
            let alt_query = format!("{game_title} video game logo");
            if self.query_commons_api(&alt_query, game_title, target_path) {
                return true;
            }
        }

        false
    }

    fn query_commons_api(&self, query: &str, game_title: &str, target_path: &Path) -> bool {
        let search_url = format!(
            "https://commons.wikimedia.org/w/api.php?action=query&list=search&srsearch={}&srnamespace=6&format=json",
            urlencoding(query)
        );

        let Ok(resp) = self.client.get(&search_url).send() else { return false; };
        if !resp.status().is_success() { return false; }
        let Ok(json_text) = resp.text() else { return false; };
        let Ok(val) = serde_json::from_str::<Value>(&json_text) else { return false; };

        let Some(search_items) = val.pointer("/query/search").and_then(|s| s.as_array()) else { return false; };

        // İlk 5 arama sonucunda .svg veya .png içeren ve oyunla uyumlu olan dosyayı seç
        for item in search_items.iter().take(5) {
            let Some(title) = item.get("title").and_then(|t| t.as_str()) else { continue; };
            let title_lower = title.to_lowercase();
            if !title_lower.ends_with(".svg") && !title_lower.ends_with(".png") {
                continue;
            }

            // GÜVENLİK: BM Kalkınma Hedefleri, siyasi, organizasyon vb. alakasız logoları filtrele
            if !is_plausible_wiki_logo(title, game_title) {
                continue;
            }

            // Imageinfo endpoint'inden doğrudan CDN / thumb URL'ini çek
            let info_url = format!(
                "https://commons.wikimedia.org/w/api.php?action=query&titles={}&prop=imageinfo&iiprop=url&iiurlwidth=600&format=json",
                urlencoding(title)
            );

            let Ok(info_resp) = self.client.get(&info_url).send() else { continue; };
            if !info_resp.status().is_success() { continue; }
            let Ok(info_json) = info_resp.text() else { continue; };
            let Ok(info_val) = serde_json::from_str::<Value>(&info_json) else { continue; };

            if let Some(pages) = info_val.pointer("/query/pages").and_then(|p| p.as_object()) {
                for (_k, page) in pages {
                    if let Some(imageinfo) = page.get("imageinfo").and_then(|ii| ii.as_array()) {
                        if let Some(first_info) = imageinfo.first() {
                            let img_url = first_info.get("thumburl")
                                .or_else(|| first_info.get("url"))
                                .and_then(|u| u.as_str());

                            if let Some(url) = img_url {
                                if self.download_and_process(url, target_path) {
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

    /// Steam Store Search API ile arama yapar ancak dönen oyun adının aranan oyunla
    /// örtüştüğünü doğrular (Strict Similarity Guard)
    fn try_resolve_steam_guarded(&self, clean_name: &str, target_path: &Path) -> bool {
        let search_url = format!(
            "https://store.steampowered.com/api/storesearch/?term={}&l=english&cc=US",
            urlencoding(clean_name)
        );

        let Ok(resp) = self.client.get(&search_url).send() else { return false; };
        if !resp.status().is_success() { return false; }
        let Ok(json_text) = resp.text() else { return false; };
        let Ok(val) = serde_json::from_str::<Value>(&json_text) else { return false; };

        let Some(items) = val.get("items").and_then(|i| i.as_array()) else { return false; };

        for item in items.iter().take(3) {
            let Some(item_name) = item.get("name").and_then(|n| n.as_str()) else { continue; };
            let Some(app_id) = item.get("id").and_then(|id| id.as_i64()) else { continue; };

            // SIKI EŞLEŞME GÜVENLİĞİ:
            // Eğer aranan isimde kritik belirteçler varsa (örn: 2, 3, underground, vice city)
            // Steam sonucunun da bunu içermesi zorunludur!
            if is_acceptable_game_match(clean_name, item_name) {
                let url = format!("https://cdn.cloudflare.steamstatic.com/steam/apps/{app_id}/logo.png");
                if self.download_and_process(&url, target_path) {
                    return true;
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

        // 1. ŞEFFAFLIK KONTROLÜ (Düz kapak fotoğraflarını ve kare posterleri reddet)
        let total_pixels = img.width() * img.height();
        let rgba = img.to_rgba8();
        let mut transparent_count = 0;
        for p in rgba.pixels() {
            if p[3] < 30 {
                transparent_count += 1;
            }
        }
        let trans_ratio = transparent_count as f32 / total_pixels as f32;
        if trans_ratio < 0.05 {
            return false;
        }

        // 2. EN-BOY ORANI KONTROLÜ (Aşırı dikey afişleri reddet)
        if img.height() as f32 > (img.width() as f32 * 1.35) {
            return false;
        }

        let cropped = auto_crop_transparent_pixels(&rgba);
        let final_canvas = fit_and_center_canvas(&cropped, 512, 280);

        if let Ok(mut out_file) = File::create(target_path) {
            if final_canvas.write_to(&mut out_file, ImageFormat::Png).is_ok() {
                return true;
            }
            let _ = fs::remove_file(target_path);
        }

        false
    }
}

/// Sıkı devam oyunu ve isim uyuşmazlığı denetleyicisi
fn is_acceptable_game_match(query: &str, result: &str) -> bool {
    let q = query.to_lowercase();
    let r = result.to_lowercase();

    if q == r {
        return true;
    }

    let q_tokens: Vec<&str> = q.split(|c: char| !c.is_alphanumeric())
        .filter(|s| !s.is_empty() && !is_noise_word(s))
        .collect();

    let r_tokens: Vec<&str> = r.split(|c: char| !c.is_alphanumeric())
        .filter(|s| !s.is_empty() && !is_noise_word(s))
        .collect();

    if q_tokens.is_empty() || r_tokens.is_empty() {
        return false;
    }

    // Tek kelimelik sorgularda (örn: "dispatch") r içinde kesinlikle tam kelime bulunmalı
    if q_tokens.len() == 1 {
        let single = q_tokens[0];
        if !r_tokens.contains(&single) {
            return false;
        }
        if r_tokens.len() > 3 {
            return false;
        }
    }

    // Kritik devam oyunu sayılarını ve kelimelerini denetle (2, 3, 4, underground, san andreas, vb.)
    for token in &q_tokens {
        if is_critical_identifier(token) && !r_tokens.contains(token) {
            return false;
        }
    }

    // En az %70 token örtüşmesi gereklidir
    let mut matches = 0;
    for token in &q_tokens {
        if r_tokens.contains(token) {
            matches += 1;
        }
    }

    (matches as f32 / q_tokens.len() as f32) >= 0.70
}

fn is_critical_identifier(token: &str) -> bool {
    matches!(
        token,
        "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9"
            | "ii" | "iii" | "iv" | "v" | "vi" | "vii" | "viii" | "ix"
            | "underground" | "unbound" | "heat" | "payback" | "rivals"
            | "vice" | "city" | "san" | "andreas" | "liberty"
            | "eternal" | "2016" | "infinite" | "remake" | "reborn"
    )
}

fn is_noise_word(word: &str) -> bool {
    matches!(
        word,
        "the" | "a" | "an" | "of" | "and" | "in" | "on" | "at" | "to" | "for" | "game" | "edition"
    )
}

fn get_curated_logo_url(title: &str, id: &str) -> Option<&'static str> {
    let t = title.to_lowercase();
    let i = id.to_lowercase();

    // 1. Popüler Çevrimiçi & Rekabetçi Oyunlar
    if t.contains("valorant") || i.contains("valorant") {
        return Some("https://cdn2.steamgriddb.com/logo/7c3ad1efdb58bc59e87515ee3c02ca4a.png");
    }
    if t.contains("league of legends") || t == "lol" || i.contains("riot:lol") {
        return Some("https://cdn2.steamgriddb.com/logo/9ebc82cba727df5eb38d2a6a617a268b.png");
    }
    if t.contains("fortnite") || i.contains("fortnite") {
        return Some("https://cdn2.steamgriddb.com/logo/5a4a5840caec0e026117b18e7e1136b6.png");
    }
    if t.contains("ea sports fc") || t.contains("fc 26") || t.contains("fc 25") || t.contains("fc 24") {
        return Some("https://cdn2.steamgriddb.com/logo/439294aeec82cba1b9d4f09d84637651.png");
    }
    if t.contains("genshin impact") || i.contains("genshin") {
        return Some("https://upload.wikimedia.org/wikipedia/en/5/5d/Genshin_Impact_logo.svg");
    }
    if t.contains("minecraft java edition") || i.contains("minecraft_java") {
        return Some("https://upload.wikimedia.org/wikipedia/commons/c/cb/Minecraft_Logo-en.svg");
    }
    if t.contains("minecraft bedrock edition") || i.contains("minecraft_bedrock") {
        return Some("https://cdn2.steamgriddb.com/logo/0dbeab53488cfdae8e040058ec0ff734.png");
    }
    if t.contains("minecraft") || i.contains("minecraft") {
        return Some("https://cdn2.steamgriddb.com/logo/0dbeab53488cfdae8e040058ec0ff734.png");
    }
    if t.contains("the finals") || i.contains("thefinals") {
        return Some("https://cdn.cloudflare.steamstatic.com/steam/apps/2073850/logo.png");
    }
    if t.contains("dirt 5") {
        return Some("https://cdn2.steamgriddb.com/logo/d8f07096e2be6b5f4be8cce1a7c50a1d.png");
    }
    if t.contains("donut county") {
        return Some("https://cdn2.steamgriddb.com/logo/5e7ce9633e3878b30d31e94ba32e3a13.png");
    }
    if t.contains("marvel rivals") {
        return Some("https://cdn.cloudflare.steamstatic.com/steam/apps/2767030/logo.png");
    }
    if t.contains("project zomboid") {
        return Some("https://cdn.cloudflare.steamstatic.com/steam/apps/108600/logo.png");
    }
    if t.contains("counter-strike 2") || t == "cs2" {
        return Some("https://cdn.cloudflare.steamstatic.com/steam/apps/730/logo.png");
    }
    if t.contains("rainbow six siege") || t == "r6" {
        return Some("https://cdn.cloudflare.steamstatic.com/steam/apps/359550/logo.png");
    }
    if t == "goals" || t.starts_with("goals ") || i.contains("goals") {
        return Some("https://upload.wikimedia.org/wikipedia/commons/f/f0/GOALS_Logo.png");
    }

    // 2. Klasik & Retro PC Oyunları
    if t.contains("underground 2") {
        return Some("https://upload.wikimedia.org/wikipedia/commons/4/48/NFSU2.svg");
    }
    if t.contains("vice city") {
        return Some("https://upload.wikimedia.org/wikipedia/commons/e/ea/Grand_Theft_Auto_Vice_City_logo.svg");
    }
    if t.contains("san andreas") || t.contains("gta sa") {
        return Some("https://cdn2.steamgriddb.com/logo/6226ea51c360be1b6c7a31f6f8ba29d6.png");
    }
    if t.contains("diablo ii") || t.contains("diablo 2") {
        return Some("https://upload.wikimedia.org/wikipedia/commons/0/0e/Diablo_II_logo.png");
    }
    if t.contains("max payne") && !t.contains("3") {
        return Some("https://upload.wikimedia.org/wikipedia/commons/1/1a/Max_Payne_Logo.svg");
    }

    // 3. Yerel & Metin2 PvP Sunucuları
    if t.contains("metin2") || t.contains("rinamt2") || t.contains("astra2") || t.contains("rohan2") {
        return Some("https://assets.metin2.dev/logo/metin2_logo_hd.png");
    }

    None
}

fn generate_alternate_titles(title: &str) -> Vec<String> {
    let mut alts = Vec::new();
    let lower = title.to_lowercase();

    // Roma Rakamı <-> Sayı dönüşümleri
    if lower.contains(" 2") {
        alts.push(title.replace(" 2", " II"));
    } else if lower.contains(" ii") {
        alts.push(title.replace(" ii", " 2").replace(" II", " 2"));
    }

    if lower.contains(" 3") {
        alts.push(title.replace(" 3", " III"));
    } else if lower.contains(" iii") {
        alts.push(title.replace(" iii", " 3").replace(" III", " 3"));
    }

    if lower.contains(" 4") {
        alts.push(title.replace(" 4", " IV"));
    } else if lower.contains(" iv") {
        alts.push(title.replace(" iv", " 4").replace(" IV", " 4"));
    }

    if lower.contains("grand theft auto") {
        alts.push(title.replace("Grand Theft Auto", "GTA").replace("grand theft auto", "GTA"));
    } else if lower.starts_with("gta ") {
        alts.push(title.replacen("gta ", "Grand Theft Auto ", 1).replacen("GTA ", "Grand Theft Auto ", 1));
    }

    if lower.contains("need for speed") {
        alts.push(title.replace("Need for Speed", "NFS").replace("need for speed", "NFS"));
    } else if lower.starts_with("nfs ") {
        alts.push(title.replacen("nfs ", "Need for Speed ", 1).replacen("NFS ", "Need for Speed ", 1));
    }

    alts
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

    // KOYU TEMA KONTRAST ADAPTÖRÜ: Siyah/çok karanlık şeffaf logoları tespit edip aydınlat
    let mut visible_pixels = 0;
    let mut total_lum: u64 = 0;
    for p in resized.pixels() {
        if p[3] > 40 {
            visible_pixels += 1;
            let lum = (0.299 * p[0] as f32 + 0.587 * p[1] as f32 + 0.114 * p[2] as f32) as u64;
            total_lum += lum;
        }
    }
    let avg_lum = if visible_pixels > 0 { total_lum / visible_pixels } else { 255 };

    let mut adapted = resized;
    if avg_lum < 65 {
        // Koyu arka planda kaybolacak siyah pikselleri parlak platine çevir
        for p in adapted.pixels_mut() {
            if p[3] > 20 {
                p[0] = 255 - p[0];
                p[1] = 255 - p[1];
                p[2] = 255 - p[2];
            }
        }
    }

    let mut canvas = RgbaImage::from_pixel(canvas_w, canvas_h, Rgba([0, 0, 0, 0]));
    let offset_x = (canvas_w - new_w) / 2;
    let offset_y = (canvas_h - new_h) / 2;

    image::imageops::overlay(&mut canvas, &adapted, offset_x as i64, offset_y as i64);
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
        "Ultimate Edition", "Game of the Year", "GOTY Edition", "GOTY",
        "Remastered", "Director's Cut", "Steam Edition",
        "Bedrock Edition", "Java Edition", "Standard Edition",
        "Definitive Edition", "Complete Edition", "Anniversary Edition",
        "Enchanted Edition", "Enhanced Edition", "Day One Edition",
        "FitGirl Repack", "FitGirl", "DODI Repack", "DODI", "ElAmigos",
        "CPY", "CODEX", "Razor1911", "SKIDROW", "PLAZA", "PROPHET",
        "Portable", "Repack", "Setup", "Installer", "Steam-Rip",
        "(TM)", "(R)", "®", "™"
    ];
    let mut cleaned = title.to_string();

    // Parantez içi gürültüleri temizle: (500MB), (x64), (v1.0), [Repack] vb.
    while let Some(start) = cleaned.find('(') {
        if let Some(end) = cleaned[start..].find(')') {
            cleaned.replace_range(start..start + end + 1, " ");
        } else {
            break;
        }
    }
    while let Some(start) = cleaned.find('[') {
        if let Some(end) = cleaned[start..].find(']') {
            cleaned.replace_range(start..start + end + 1, " ");
        } else {
            break;
        }
    }

    for n in noise {
        cleaned = cleaned.replace(n, " ");
    }

    // Tireden sonra gelen dosya boyutu veya sürüm eklerini temizle (örn: "- 500MB", "- v1.0")
    if let Some(idx) = cleaned.rfind('-') {
        let suffix = &cleaned[idx + 1..].trim();
        if suffix.contains("MB") || suffix.contains("GB") || suffix.starts_with('v') || suffix.starts_with("build") {
            cleaned.truncate(idx);
        }
    }

    // Çift boşlukları temizle
    cleaned = cleaned.split_whitespace().collect::<Vec<_>>().join(" ");
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

fn is_plausible_wiki_logo(file_title: &str, game_name: &str) -> bool {
    let lower_title = file_title.to_lowercase();
    let lower_game = game_name.to_lowercase();

    // Siyasi / Organizasyon / BM / Tren / Trafik / Dizi Alakasız konuları kesinlikle engelle
    let bad_words = [
        "development goals", "sustainable", "agenda 2030", "organization",
        "council", "university", "political", "coat of arms", "flag of",
        "election", "government", "ministry", "treaty", "convention", "association",
        "railway", "train", "s-bahn", "suburban", "metro", "linea", "line ", "traffic", "road sign",
        "season", "season 2", "season 1", "s01", "s02", "s03", "series", "episode",
        "album", "soundtrack", "tour", "concert", "station", "bus", "transport"
    ];
    for bad in bad_words {
        if lower_title.contains(bad) && !lower_game.contains(bad) {
            return false;
        }
    }

    // 3 karakter veya daha kısa oyunlar için (örn: "S2", "CS", "BF")
    // Başlık açıkça bir video oyunu olduğunu belirtmeli
    let game_trimmed = lower_game.trim();
    if game_trimmed.len() <= 3 {
        let is_explicit_game = lower_title.contains("video game") 
            || lower_title.contains("game logo") 
            || lower_title.contains("computerspiel") 
            || lower_title.contains("jeu vidéo");
        if !is_explicit_game {
            return false;
        }
    }

    // Başlık aranan oyunun ilk anahtar kelimesini TAM KELİME olarak içermeli
    let first_token = lower_game.split(|c: char| !c.is_alphanumeric()).find(|s| !s.is_empty());
    if let Some(token) = first_token {
        let title_tokens: Vec<&str> = lower_title.split(|c: char| !c.is_alphanumeric()).collect();
        if !title_tokens.contains(&token) {
            return false;
        }
    }

    true
}

