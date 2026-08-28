> 🇹🇷 **Türkçe** | 🇬🇧 [Switch to English](Artwork-&-Customization-EN)

# 🎨 Görsel ve Logo Motoru

ONYX Launcher, kütüphanenizdeki her oyunun en yüksek kalitede logoya ve kapağa sahip olmasını sağlayan çok aşamalı bir görsel işlem hattı barındırır.

---

## 🖼️ Çok Kademeli Görsel Pipeline

`mermaid
graph TD
    Game[Tespit Edilen Oyun] --> CheckLocal[1. Yerel Yüksek Çözünürlüklü İkon / Dosya Çıkarma]
    CheckLocal -->|Bulundu| Cache[%LOCALAPPDATA%/ONYX/icons İçine Kaydet]
    CheckLocal -->|Bulunamadı| CheckSteamGrid[2. SteamGridDB Açık API Motoru]
    CheckSteamGrid -->|Bulundu| Crop[3. SkiaSharp Akıllı Şeffaf Kenar Kırpma]
    CheckSteamGrid -->|Bulunamadı| CheckDuckDuckGo[4. DuckDuckGo PNG Logo Scraper]
    CheckDuckDuckGo -->|Bulundu| Crop
    CheckDuckDuckGo -->|Bulunamadı| BuiltIn[5. Dahili Vektörel SVG Platform İkonları]
    Crop --> Cache
    BuiltIn --> Render[Avalonia Arayüzünde Göster]
    Cache --> Render
`

---

## ✂️ SkiaSharp Akıllı Şeffaf Kenar Kırpma

İnternetten indirilen oyun logoları genellikle kenarlarında gereksiz boş şeffaf alanlar veya siyah çerçeveler barındırır.

ONYX Launcher içerisindeki **SkiaSharp** motoru:
1. Görseldeki görünür pikselleri tarar.
2. Fazlalık boş şeffaf kenar boşluklarını otomatik olarak kırpar.
3. Kartlar ve afişler içinde logonun her zaman tam hizalı ve orantılı görünmesini sağlar.