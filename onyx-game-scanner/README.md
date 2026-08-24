# ONYX Game Scanner (Oyun Tarama Çekirdeği)

**ONYX Oyun Kütüphanesi** için özel olarak hazırlanmış, Windows işletim sisteminde kurulu olan tüm oyunları bulan **bağımsız yerel oyun tarama çekirdeğidir**.

---

## 🚀 Özellikler

- **%100 Yerel Tarama**: İnternet bağlantısı veya mağaza girişi gerektirmez. Doğrudan Windows Kayıt Defteri (Registry), VDF, SQLite ve manifest dosyalarından okuma yapar.
- **Desteklenen Platformlar (12 Adet)**:
  - 💚 **Xbox / Game Pass**
  - 🟦 **Steam**
  - 🟪 **Epic Games**
  - 🔴 **EA App**
  - 🔴 **Riot Games**
  - 🟩 **Minecraft** (Bedrock + Java)
  - 📁 **Lokal / Mağazasız Oyunlar** (`D:\Games`, `C:\Oyunlar`, vb.)
  - 🕹️ **Battle.net, GOG Galaxy, Ubisoft Connect, Rockstar Games, Amazon Games**

---

## 🛠️ Entegrasyon ve Kullanım

### 1. Rust Projelerine Kütüphane Olarak Ekleme (Tauri / Custom Rust UI)
`Cargo.toml` dosyanıza ekleyin:
```toml
[dependencies]
onyx-game-scanner = { path = "../onyx-game-scanner" }
```

Kod kullanımı:
```rust
use onyx_game_scanner::{GameFindEngine, ScanOptions};

fn main() {
    let engine = GameFindEngine::new();
    let report = engine.scan(ScanOptions::default()).unwrap();

    for game in report.games {
        println!("{} [{}] -> {:?}", game.name, game.platform, game.install_path);
    }
}
```

### 2. Dış Arayüzler İçin CLI / JSON Kullanımı (Electron, React, Python, C#)
```bash
# JSON Formatında Oyun Listesini Alır
onyx_scanner.exe scan --json
```
