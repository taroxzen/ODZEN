> 🇹🇷 **Türkçe** | 🇬🇧 [Switch to English](Security-&-Privacy-EN)

# 🛡️ Güvenlik ve Gizlilik

Güvenlik ve kullanıcı gizliliği, ONYX Launcher'ın temel tasarım ilkelerindendir.

---

## 🔒 Uygulanan Güvenlik Önlemleri

### 1. PowerShell Script Enjeksiyonu Koruması
* [WindowsNotificationService.cs](https://github.com/taroxzen/ONYX-Launcher/blob/main/Onyx.Avalonia/Services/WindowsNotificationService.cs) içindeki Windows bildirimleri, parametreleri UTF-16 Base64 formatına çevrilerek -EncodedCommand ile aktarılır. Özel karakterler içeren oyun isimleri komut enjeksiyonu riski oluşturmaz.

### 2. Güvenli Protokol Başlatma
* Web ve oyun protokolleri açılırken cmd.exe /C start yerine doğrudan Windows'un güvenli undll32 url.dll,FileProtocolHandler bileşeni kullanılır.

### 3. Path Traversal ve Önbellek Güvenliği
* Tüm önbellek dosyaları regex ([a-zA-Z0-9_\-]) ve SHA-256 hash süzgecinden geçirilir; dizin atlama (.., /, \) engellenir.

### 4. Sıfır Telemetri Garantisi
* ONYX Launcher hiçbir kişisel veriyi, oyun listenizi veya donanım bilginizi harici sunuculara göndermez. Tüm veriler yalnızca yerel bilgisayarınızda tutulur.