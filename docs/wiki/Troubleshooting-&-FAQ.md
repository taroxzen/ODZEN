# ❓ Troubleshooting & Frequently Asked Questions (FAQ)

Find quick answers to common questions and troubleshooting solutions.

---

## 🔧 Frequently Asked Questions

### Q: Why didn't one of my games appear automatically in the library?
**A:** Check if the game was installed in a non-standard directory. You can easily add it manually by clicking the **"+ Oyun Ekle / Add Game"** button on the top right toolbar and selecting the .exe file.

### Q: Does ONYX Launcher affect in-game FPS?
**A:** No. When you launch a game and minimize ONYX Launcher to the tray, it consumes **0% CPU** and trims its RAM usage to **<20 MB**, leaving all system resources available for your game.

### Q: Is internet connection required?
**A:** No. ONYX Launcher works completely offline. An internet connection is only used to fetch high-resolution game logos and online artwork.

### Q: How do I change the language?
**A:** Click on **Settings (Ayarlar)** in the left navigation sidebar and select your preferred language (English, Turkish, German, French, Spanish, Russian, Dutch, or Bulgarian).

---

## 🛠️ Common Troubleshooting Steps

### Scanner Executable Missing
If you see an alert saying the scanner binary was not found:
* Ensure onyx-game-scanner.exe is placed in the same folder as Onyx.Avalonia.exe.
* If using the Portable ZIP, make sure you extracted all files rather than running directly from inside the archive.

### Windows Defender / SmartScreen Notice
Since ONYX Launcher is a new open-source community application without an expensive EV code signing certificate:
* Click **"More Info" (Daha fazla bilgi)** -> **"Run Anyway" (Yine de çalıştır)** on the Windows SmartScreen dialog.