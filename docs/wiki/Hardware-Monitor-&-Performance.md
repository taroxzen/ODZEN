> 🇹🇷 **Türkçe** | 🇬🇧 [Switch to English](Hardware-Monitor-&-Performance-EN)

# 📊 Donanım İzleme ve Performans

ONYX Launcher sol yan panelinde sisteminizin durumunu gerçek zamanlı gösteren hafif bir donanım izleyici barındırır.

---

## 📈 İzlenen Donanım Değerleri

* **İşlemci Kullanımı (CPU %):** Win32 GetSystemTimes çekirdek çağrısı ile sıfır gecikmeli ölçüm.
* **Bellek Kullanımı (RAM % & GB):** Toplam fiziksel RAM ve boş bellek oranı.
* **Ekran Kartı Kullanımı (GPU %):** DirectX / Direct3D / DXGI adaptör telemetrisi.
* **Video Belleği (VRAM GB):** Ayrılmış ekran kartı belleği kullanımı.

---

## ⚡ Sıfır Yük ve Optimizasyon

1. **Düşük Sorgu Sıklığı:** Sadece launcher odaktayken 1.5 saniyede bir güncellenir; oyun başladığında veya pencere gizlendiğinde tamamen durur.
2. **Arka Planda RAM Temizleme:** Tepsiye küçültüldüğünde SetProcessWorkingSetSize ile RAM kullanımı **<20 MB** seviyesine düşer.