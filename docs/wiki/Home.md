> 🇹🇷 **Türkçe (Varsayılan)** | 🇬🇧 [[Home-EN|Switch to English]]

# 🎮 ONYX Launcher Wiki'sine Hoş Geldiniz

Windows 10 ve 11 için geliştirilmiş, siberpunk temalı evrensel oyun kütüphanesi ve platform merkezi **ONYX Launcher**'ın resmi Türkçe dokümantasyonuna hoş geldiniz.

---

## 🌟 ONYX Launcher Nedir?

ONYX Launcher, farklı platformlara (Steam, Epic Games, EA App, Ubisoft, GOG, Battle.net, Xbox, Minecraft, Metin2) dağılmış oyunlarınızı tek bir modern ve yüksek performanslı merkezde toplar. **Avalonia UI (.NET 10)** ile hazırlanan akıcı arayüzü ve **Rust çekirdek motoru (onyx-game-scanner)** sayesinde yüzlerce oyunu 1 saniyenin altında tarayarak kütüphanenize ekler.

`mermaid
graph TD
    UI[Avalonia UI Arayüzü - C# .NET 10] -->|JSON IPC Akışı| RustCore[Rust Oyun Tarama Motoru]
    UI --> ArtEngine[Dinamik Yüksek Çözünürlüklü Logo Motoru]
    UI --> HwMon[Gerçek Zamanlı Donanım İzleme]
    UI --> MusicHub[Entegre Müzik ve Yayın Akışı]
    RustCore --> Steam[Steam VDF & AppManifest]
    RustCore --> Epic[Epic Games Manifestleri]
    RustCore --> EA[EA App & Origin Verisi]
    RustCore --> Ubi[Ubisoft Connect Kayıt Defteri]
    RustCore --> GOG[GOG Galaxy Veritabanı]
    RustCore --> BNet[Battle.net ProductDB]
    RustCore --> Xbox[Windows Mağazası & Xbox]
    RustCore --> Local[Yerel Diskler & Özel Klasörler]
`

---

## 📚 Dokümantasyon Rehberleri

| Bölüm | Açıklama |
| :--- | :--- |
| 🚀 **[[Getting-Started|Başlangıç Rehberi]]** | Sistem gereksinimleri, kurulum seçenekleri (Setup vs Taşınabilir) ve ilk çalıştırma. |
| ⚙️ **[[Features-&-Architecture|Özellikler ve Mimari]]** | MVVM mimarisi, Rust tarayıcı motoru ve bellek optimizasyonu detayları. |
| 🕹️ **[[Supported-Platforms|Desteklenen Platformlar]]** | 10'dan fazla platformun tespit kuralları ve başlatma protokolleri. |
| 🎨 **[[Artwork-&-Customization|Görsel ve Logo Motoru]]** | SteamGridDB, DuckDuckGo scraper ve SkiaSharp akıllı şeffaf kırpma sistemi. |
| 📊 **[[Hardware-Monitor-&-Performance|Donanım İzleme]]** | Yan panelde anlık CPU, RAM, GPU ve VRAM kullanımı; sıfır arka plan yükü. |
| 🛡️ **[[Security-&-Privacy|Güvenlik ve Gizlilik]]** | Komut enjeksiyonu önlemleri, path traversal koruması ve sıfır telemetri garantisi. |
| ❓ **[[Troubleshooting-&-FAQ|Sorun Giderme & SSS]]** | Sık karşılaşılan sorular ve hata çözüm adımları. |

---

## ⚡ Hızlı Sistem Özellikleri

* **İşletim Sistemi:** Windows 10 / 11 (64-bit)
* **Arayüz Altyapısı:** Avalonia UI 11.2 (.NET 10)
* **Çekirdek Tarayıcı:** Rust (2021 edition)
* **Bellek Kullanımı:** ~35–65 MB aktif (Simge durumunda <20 MB'a düşürülür)
* **Desteklenen Diller:** Türkçe, İngilizce, Almanca, Fransızca, İspanyolca, Rusça, Felemenkçe, Bulgarca