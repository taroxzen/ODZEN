> 🇹🇷 **Türkçe** | 🇬🇧 [[Features-&-Architecture-EN|Switch to English]]

# ⚙️ Özellikler ve Mimari

ONYX Launcher, modern masaüstü teknolojilerini bir araya getiren hibrit bir mimari üzerine kurulmuştur:
1. **Kullanıcı Arayüzü & Sunum:** Avalonia UI (.NET 10) ve CommunityToolkit.Mvvm ile C# tabanlı yüksek performanslı MVVM yapısı.
2. **Çekirdek Tarayıcı Motoru:** Doğrudan yerel makine koduna derlenen Rust (onyx-game-scanner.exe).

---

## 🏗️ Mimari Şema

`mermaid
sequenceDiagram
    participant UI as Avalonia Arayüzü (C#)
    participant Core as Tarayıcı Çekirdeği (Rust)
    participant Disk as Dosya Sistemi / Kayıt Defteri
    participant CDN as SteamGridDB / Bulut CDN

    UI->>Core: Process.Start("onyx-game-scanner.exe scan --json")
    Core->>Disk: Steam VDF, Kayıt Defteri HKLM/HKCU, SQLite DB Sorguları
    Disk-->>Core: Ham Platform Verileri
    Core-->>UI: Yapılandırılmış JSON Akışı
    UI->>UI: ObservableCollection<GameItem> Bağlama
    UI->>CDN: Asenkron Logo & Görsel İndirme
    CDN-->>UI: PNG Önbelleği & SkiaSharp Çizimi
`

---

## 🧩 Temel Alt Sistemler

### 1. Rust Tarama Motoru (onyx-game-scanner)
* **Sıfır Gecikme:** C# tarafına yük bindirmeden doğrudan Windows Registry, Steam VDF ve SQLite veri tabanlarını okur.
* **JSON Çıktısı:** Tespit edilen tüm oyunları standart bir veri yapısıyla arayüze iletir.

### 2. Avalonia UI & MVVM Altyapısı
* **Vektörel Siberpunk Arayüz:** Tüm çözünürlüklerde keskin kalan SVG ikonlar ve pürüzsüz animasyonlar.
* **Sanal Liste (Virtualization):** 1000+ oyunlu dev kütüphanelerde dahi 60–144 FPS akıcı kaydırma.

### 3. Bellek Optimizasyon Servisi (MemoryOptimizerService.cs)
* **Otomatik RAM Boşaltma:** Launcher tepsiye küçültüldüğünde veya boşa çıktığında gereksiz bellek alanını Windows'a geri iade eder (<20 MB).