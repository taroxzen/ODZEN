> 🇬🇧 **English** | 🇹🇷 [Türkçe'ye Geç](Supported-Platforms)

# 🕹️ Supported Platforms & Detection Mechanics

ONYX Launcher includes out-of-the-box detection support for over 10 gaming platforms.

---

## 📋 Platform Detection Matrix

| Platform | Detection Source | Launch Protocol / Method |
| :--- | :--- | :--- |
| **Steam** | Registry Software\Valve\Steam + libraryfolders.vdf | steam://rungameid/{appId} |
| **Epic Games** | %ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item | com.epicgames.launcher://apps/... |
| **EA App / Origin** | %ProgramData%\Electronic Arts\EA Desktop\InstallData\*.ini | origin2://game/launch?offerIds=... |
| **Ubisoft Connect** | Registry Software\Ubisoft\Launcher\Installs | uplay://launch/{id}/0 |
| **GOG Galaxy** | Registry Software\GOG.com\Games & index.db | goggalaxy://openGameView/{gameId} |
| **Battle.net** | %ProgramData%\Battle.net\Agent\product.db | attlenet://{productCode} |
| **Xbox / Microsoft Store** | Windows Package Manager & Gaming Services | Windows URI or Direct App Execution |
| **Minecraft** | %APPDATA%\.minecraft & CurseForge | minecraft:// |
| **Riot Games / Valorant** | %ProgramData%\Riot Games\RiotClientInstalls.json | iotclient:// |
| **Metin2 / P-Servers** | Local directory scanning & patcher discovery | Direct Executable (.exe) |
| **Local Standalone Games** | Custom selected folders & drive roots | Direct Executable with Working Directory |