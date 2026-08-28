# 🕹️ Supported Platforms & Detection Mechanics

ONYX Launcher includes out-of-the-box detection support for over 10 gaming platforms and launcher ecosystems.

---

## 📋 Platform Detection Matrix

| Platform | Detection Source | Launch Protocol / Method |
| :--- | :--- | :--- |
| **Steam** | Registry Software\Valve\Steam + libraryfolders.vdf + ppmanifest_*.acf | steam://rungameid/{appId} |
| **Epic Games** | %ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item | com.epicgames.launcher://apps/{appName}?action=launch&silent=true |
| **EA App / Origin** | %ProgramData%\Electronic Arts\EA Desktop\InstallData\*.ini & Registry | origin2://game/launch?offerIds={contentId} |
| **Ubisoft Connect** | Registry Software\Ubisoft\Launcher\Installs & settings.yaml | uplay://launch/{id}/0 |
| **GOG Galaxy** | Registry Software\GOG.com\Games & index.db SQLite | goggalaxy://openGameView/{gameId} |
| **Battle.net** | %ProgramData%\Battle.net\Agent\product.db | attlenet://{productCode} or direct .exe |
| **Xbox / Microsoft Store** | Windows Package Manager & Gaming Services Registry | Windows URI or Direct App Execution |
| **Minecraft** | %APPDATA%\.minecraft, CurseForge Instances & Prism/Modrinth | minecraft:// or instance launcher |
| **Riot Games / Valorant** | %ProgramData%\Riot Games\RiotClientInstalls.json | iotclient:// or Riot Client executable |
| **Metin2 / P-Servers** | Local directory scanning & auto-patcher .exe discovery | Direct Executable (metin2client.exe, etc.) |
| **Local Standalone Games** | Custom selected folders, drive roots, Windows Uninstall registry | Direct Executable with Working Directory |

---

## 🔍 How to Add Custom Local Games

1. Click the **"+ Oyun Ekle / Add Game"** button on the top-right toolbar.
2. Enter the **Game Title**.
3. Choose the **Platform** (e.g. Local, Custom, Emulator).
4. Browse for the game's .exe executable file or enter a custom URI protocol.
5. Click **Add**. ONYX Launcher will extract the high-resolution .ico icon and immediately add it to your library.