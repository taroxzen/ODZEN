> 🇹🇷 **Türkçe** | 🇬🇧 [[Getting-Started-EN|Switch to English]]

# 🚀 Başlangıç Rehberi

Bu rehberde **ONYX Launcher** sistem gereksinimleri, kurulum yöntemleri ve ilk çalıştırma adımları anlatılmaktadır.

---

## 📋 Sistem Gereksinimleri

| Bileşen | Minimum Gereksinim | Önerilen |
| :--- | :--- | :--- |
| **İşletim Sistemi** | Windows 10 (64-bit, Derleme 1809+) | Windows 11 (64-bit) |
| **Mimari** | x64 (Intel / AMD 64-bit) | x64 |
| **Çalışma Zamanı** | .NET Masaüstü Çalışma Zamanı 10.0 | Pakete Entegre Edilmiştir |
| **Bellek (RAM)** | 2 GB | 4 GB ve üzeri |
| **Disk Alanı** | ~150 MB boş alan | SSD önerilir |
| **Grafik** | DirectX 11 / OpenGL 3.0+ uyumlu ekran kartı | Güncel Dahili/Harici GPU |

---

## 📦 Kurulum Seçenekleri

En güncel sürümü indirmek için [GitHub Sürümler (Releases)](https://github.com/taroxzen/ONYX-Launcher/releases/latest) sayfasını ziyaret edin.

### Seçenek 1: Kurulum Sihirbazı (ONYX_Setup_v1.1.0.exe) — Önerilen
1. **ONYX_Setup_v1.1.0.exe** dosyasını indirin.
2. Kurulum sihirbazını başlatın (Yönetici yetkisi **gerekmez**; güvenli şekilde %LOCALAPPDATA%\Programs\ONYX Launcher altına kurulur).
3. Dilinizi ve Masaüstü Kısayolu seçeneklerini belirleyin.
4. **Kur** butonuna tıklayın. Başlat menüsü ve masaüstü kısayollarınız otomatik olarak oluşturulacaktır.

### Seçenek 2: Taşınabilir Paket (ONYX-Launcher-v1.1.0-win-x64.zip)
1. **ONYX-Launcher-v1.1.0-win-x64.zip** dosyasını indirin.
2. Arşivi istediğiniz bir klasöre çıkartın (örn. C:\Games\ONYX veya D:\ONYX).
3. Herhangi bir kurulum yapmadan doğrudan **Onyx.Avalonia.exe** dosyasını çalıştırın.

---

## 🎮 İlk Başlatma ve Otomatik Kütüphane Taraması

1. ONYX Launcher açıldığında Rust tarama motorunu otomatik olarak devreye sokar.
2. Bilgisayarınızda yüklü olan Steam, Epic Games, EA App, Ubisoft, GOG, Battle.net, Xbox, Minecraft ve Metin2 oyunları saniyeler içinde tespit edilir.
3. Arka planda yüksek çözünürlüklü logolar indirilir ve optimize edilir.
4. Oynamak istediğiniz oyuna tıklayarak doğrudan başlatabilirsiniz!