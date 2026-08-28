> 🇹🇷 **Türkçe** | 🇬🇧 [[Supported-Platforms-EN|Switch to English]]

# 🕹️ Desteklenen Platformlar ve Başlatma Mekanikleri

ONYX Launcher, 10'dan fazla oyun platformunu ve bağımsız oyunları otomatik olarak tanır.

---

## 📋 Platform Tespit Tablosu

| Platform | Tespit Kaynağı | Başlatma Yöntemi |
| :--- | :--- | :--- |
| **Steam** | Software\Valve\Steam + libraryfolders.vdf + ppmanifest_*.acf | steam://rungameid/{appId} |
| **Epic Games** | %ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item | com.epicgames.launcher://apps/... |
| **EA App / Origin** | %ProgramData%\Electronic Arts\EA Desktop\InstallData\*.ini | origin2://game/launch?offerIds=... |
| **Ubisoft Connect** | Software\Ubisoft\Launcher\Installs & settings.yaml | uplay://launch/{id}/0 |
| **GOG Galaxy** | Software\GOG.com\Games & index.db SQLite | goggalaxy://openGameView/{gameId} |
| **Battle.net** | %ProgramData%\Battle.net\Agent\product.db | attlenet://{productCode} |
| **Xbox / Microsoft Store** | Windows Paket Yöneticisi & Gaming Services | Windows URI veya Doğrudan Çalıştırma |
| **Minecraft** | %APPDATA%\.minecraft, CurseForge & Modrinth | minecraft:// veya Launcher |
| **Riot Games / Valorant** | %ProgramData%\Riot Games\RiotClientInstalls.json | iotclient:// |
| **Metin2 / P-Serverlar** | Yerel Dizin Taraması & Otomatik Yama .exe Tespiti | Doğrudan .exe Başlatma |
| **Yerel Bağımsız Oyunlar** | Kullanıcı Tarafından Eklenen Klasörler & Sürücüler | Doğrudan .exe Başlatma |

---

## ➕ Manuel Oyun Ekleme

1. Sağ üstteki **"+ Oyun Ekle"** butonuna tıklayın.
2. Oyun Adını yazın ve Platformu seçin.
3. Oyunun .exe dosyasını seçin veya URI protokolünü girin.
4. **Ekle** butonuna tıkladığınızda oyununuz otomatik olarak yüksek çözünürlüklü ikonuyla kütüphanenize yerleşir.