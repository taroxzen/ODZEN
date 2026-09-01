# ODZEN — Mimari ve Açık Kaynak Geliştirici Kılavuzu

ODZEN, **Windows ve Linux (Masaüstü / Proton / Lutris)** ortamlarında yüksek performanslı, birleşik modüllerden oluşan açık kaynaklı modern bir oyun kütüphanesi yöneticisidir.

---

## 🏛️ Mimari Katmanları (Cross-Platform Architecture)

* **Frontend (Arayüz Katmanı):** `Odzen.Avalonia` — X11, Wayland ve Win32 üzerinde sıfır kod değişikliğiyle çalışan Avalonia UI .NET 10 motoru.
* **Unified Backend Core:** `engine/odzen-core.exe` (`odzen-core`) — Saf Rust ile yazılmış, tüm arka plan görevlerini tek çatı altında toplayan evrensel çekirdek:
  * 🔍 **`odzen-core scan` (ODZEN Core Scanner V1):** Steam, Epic, EA App, Riot, Metin2, Minecraft, Xbox, GOG, Ubisoft ve yerel disk oyunlarını milisaniyeler içinde JSON formatında tarar.
  * 🎨 **`odzen-core artwork`:** `image` ve `reqwest` kütüphaneleriyle saf Rust ortamında 4K şeffaf logoları indirir, kenar piksellerini otomatik kırpar ve 512x280 tuvalde merkezler.
  * 🚀 **`odzen-core launch`:** Oyunları resmi mağaza protokolleri (`steam://`, `origin2://`, `shell:appsFolder`) veya doğrudan `.exe` yoluyla güvenle başlatır.
  * ⚡ **`odzen-core sysinfo`:** İşletim sistemi, çekirdek sayısı ve arka plan çalışma durumunu raporlar.

---

## 📂 Depo (Repository) Klasörleme Sistemi

```text
ODZEN/
├── Odzen.Avalonia/                   🖥️ Arayüz Katmanı (Cross-Platform Avalonia UI)
│   ├── Views/
│   ├── ViewModels/
│   ├── Models/
│   └── Services/
│
├── odzen-game-scanner/               🦀 Birleşik Rust Çekirdeği (odzen-core)
│   ├── Cargo.toml
│   └── src/
│       ├── main.rs                   (CLI Subcommands: scan, artwork, launch, sysinfo)
│       ├── artwork.rs                (Saf Rust Görsel İşleme & 4K Logo Pipeline)
│       ├── scanners/                 (Steam, EA, Riot, Metin2, Local vb.)
│       └── music.rs                  (Müzik Servisleri Bulucu)
│
├── assets/                           🎨 Paylaşılan Varlıklar (SVG & ICO)
│   ├── icons/
│   └── platforms/
│
├── docs/                             📚 Mimari ve Katkı Kılavuzları
│   └── ARCHITECTURE.md
│
└── ODZEN_FINAL/                      📦 Nihai Dağıtım Klasörü
    ├── ODZEN.exe                     ⭐ Tek Dosya Arayüz (Single-File)
    ├── engine/
    │   └── odzen-core.exe            🦀 Tek Birleşik Rust Çekirdeği
    ├── assets/                       🎨 Varlıklar
    └── runtimes/                     💻 Skia & HarfBuzz Grafik Kütüphaneleri
```
