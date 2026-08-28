> 🇹🇷 **Türkçe (Varsayılan)** | 🇬🇧 [Switch to English](Home-EN)

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
| 🚀 **[Başlangıç Rehberi](Getting-Started)** | Sistem gereksinimleri, kurulum seçenekleri (Setup vs Taşınabilir) ve ilk çalıştırma. |
| ⚙️ **[Özellikler ve Mimari](Features-&-Architecture)** | MVVM mimarisi, Rust tarayıcı motoru ve bellek optimizasyonu detayları. |
| 🕹️ **[Desteklenen Platformlar](Supported-Platforms)** | 10'dan fazla platformun tespit kuralları ve başlatma protokolleri. |
| 🎨 **[Görsel ve Logo Motoru](Artwork-&-Customization)** | SteamGridDB, DuckDuckGo scraper ve SkiaSharp akıllı şeffaf kırpma sistemi. |
| 📊 **[Donanım İzleme](Hardware-Monitor-&-Performance)** | Yan panelde anlık CPU, RAM, GPU ve VRAM kullanımı; sıfır arka plan yükü. |
| 🛡️ **[Güvenlik ve Gizlilik](Security-&-Privacy)** | Komut enjeksiyonu önlemleri, path traversal koruması ve sıfır telemetri garantisi. |
| ❓ **[Sorun Giderme & SSS](Troubleshooting-&-FAQ)** | Sık karşılaşılan sorular ve hata çözüm adımları. |

---

## ⚡ Hızlı Sistem Özellikleri

* **İşletim Sistemi:** Windows 10 / 11 (64-bit)
* **Arayüz Altyapısı:** Avalonia UI 11.2 (.NET 10)
* **Çekirdek Tarayıcı:** Rust (2021 edition)
* **Bellek Kullanımı:** ~35–65 MB aktif (Simge durumunda <20 MB'a düşürülür)
* **Desteklenen Diller:** Türkçe, İngilizce, Almanca, Fransızca, İspanyolca, Rusça, Felemenkçe, Bulgarca