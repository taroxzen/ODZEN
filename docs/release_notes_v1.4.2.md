# ODZEN v1.4.2 Sürüm Notları (Release Notes)

**Yayım Tarihi:** 3 Eylül 2026  
**Geliştirici:** Taroxzen (https://github.com/taroxzen)  
**Paket Sürümü:** 1.4.2 / 1.4.2.0

---

## 🚀 Öne Çıkan Yenilikler ve Düzeltmeler (v1.4.2)

### 1. 🛡️ Epic Games Doğrudan Protokol & Kısayol Başlatma Sistemi
- **Sorun:** Epic Games oyunları doğrudan binary (.exe) üzerinden çalıştırılmaya çalışıldığında launcher kimlik doğrulaması, EOS (Epic Online Services) veya Anti-Cheat gereksinimleri nedeniyle başlatılamıyordu.
- **Çözüm:** Epic Games oyunları artık resmi Epic URL protokol kısayolu (`com.epicgames.launcher://apps/{AppName}?action=launch&silent=true`) üzerinden doğrudan ve tam uyumlu olarak başlatılıyor. Bu sayede bulut senkronizasyonu, anti-cheat ve kullanıcı oturumu sorunsuz devreye girer.

### 2. 📁 Aynı Klasörde Çoklu Oyun Bulma Sorunu Giderildi (Klasör Bazlı Akıllı Tekilleştirme)
- **Sorun:** Epic Games'te bir oyun indirildiğinde (örneğin GOALS), Epic Manifest dizininde hem ana oyun hem de alt paket ("GOALS Base Game" / DLC manifesti) bulunuyordu ve kütüphanede aynı dizin için iki ayrı oyun kartı listeleniyordu.
- **Çözüm:**
  - `odzen-core` Epic tarayıcısına `MainGameAppName` denetimi eklendi; başka bir ana oyuna ait alt paketler ve eklentiler otomatik filtrelendi.
  - İsim sonundaki `" Base Game"` ve `" Content"` belirteçleri temizlendi.
  - Hem `EpicScanner`, hem `RiotScanner`, hem de genel `engine.rs` seviyesinde platform + kurulum dizini bazlı tekilleştirme (`dedupe_games`) uygulandı. Artık hiçbir platformda aynı klasör için birden fazla oyun kartı listelenmez.

### 3. 🌐 Çevrimiçi Logo Arama Filtresi & "GOALS" Hatasının Giderilmesi
- **Sorun:** Wikimedia Commons üzerinde `"GOALS logo"` araması yapıldığında, BM Sürdürülebilir Kalkınma Hedefleri (Sustainable Development Goals) simgesi ilk sırada çıkıyor ve oyun logosu yerine bu simge indirilip atanıyordu.
- **Çözüm:**
  - Tek kelimelik ve kısa oyun isimleri için arama motoru otomatik olarak `"{oyun_adı} video game logo"` sorgusu yapar (böylece `"GOALS Logo.png"` gerçek oyun logosu ilk sıraya gelir).
  - Negatif anahtar kelime filtresi eklendi: Siyasi, kurumsal ve oyun dışı konular (`sustainable`, `development goals`, `agenda 2030`, `organization`, `council`, `university`, `coat of arms`, `flag of`) arama sonuçlarından elenir.
  - "GOALS" için doğrudan 4K şeffaf PNG vektör entegrasyonu tanımlandı.

### 4. 🎛️ Gelişmiş "Çevrimiçi Oyun Medya ve Logo Yapılandırması" Paneli (6 Yeni Özellik!)
Ayarlar penceresindeki medya yapılandırma kartı baştan aşağı yenilendi ve tam kontrol sağlayan profesyonel bir kontrol paneline dönüştürüldü:
1. **📂 Logo Klasörünü Aç:** Tek tıkla diskteki logo klasörünü (`%LOCALAPPDATA%\ODZEN\artwork\logos`) Windows Gezgini'nde açar.
2. **🗑️ Logo Önbelleğini Temizle & Canlı Boyut Sayacı:** Diskteki önbellek boyutunu (örn. `14.2 MB`) anlık hesaplayarak gösterir ve tek tıkla tüm önbelleği sıfırlama imkanı sunar.
3. **⚡ Yalnızca Eksikleri İndir:** Kütüphanede yalnızca henüz logosu bulunmayan oyunları internetten indirerek kota ve zamandan tasarruf sağlar.
4. **🔑 SteamGridDB API Anahtarı Alanı:** Kullanıcının kendi SteamGridDB API anahtarını girebileceği özel alan eklendi.
5. **☑️ Arama Kaynakları Seçimi (Toggles):** Steam Store, Wikimedia Commons ve SteamGridDB kaynakları ayrı ayrı açılıp kapatılabilir.
6. **📊 Canlı İndirme İlerleme Çubuğu ve Sayaç:** Logolar indirilirken yüzde göstergesi ve o an inen oyunun adını gösteren canlı progress bar eklendi.

### 5. 🎨 Kapsamlı Logo Tespit Çözümü & Etkileşimli Aday Seçici (Modal)
- **Sorun:** "THE FINALS" siyah yazısı koyu temada görünmüyor, "Dispatch" için alakasız tren kutu afişi geliyordu. "S2" için Milano banliyö treni kırmızı X simgesi eşleşiyordu.
- **Çözüm:**
  - **Sıkı Kelime Sınırı (`\b{kelime}\b`):** Tam kelime kontrolü ile alakasız benzer isimli oyunlar elendi.
  - **Kısa Başlık Koruması ($\le 3$ karakter):** Kısa aramalarda açıkça video oyunu olduğu teyit edilmeyen görseller (Milano tren simgesi) reddedilir.
  - **Alakasız Kategori Filtresi:** Railway, train, metro, season 2 vb. kategoriler Wikimedia sorgularından elendi.
  - **Şeffaflık & En-Boy Denetimi:** Kare fotoğraf ve kutu afişleri reddedilir; sistem ikonuna dönülür.
  - **Koyu Tema Kontrast Adaptörü:** Siyah logolar otomatik algılanıp platine/beyaza çevrilerek netleştirildi.
  - **🔍 Çevrimiçi Ara & Seç (Modal):** Oyun Detay'da Steam, SteamGridDB ve Wikimedia adaylarını küçük resimler halinde gösteren ve doğrudan URL yapıştırmaya izin veren modern seçici paneli eklendi.

### 6. 🧠 İki Karşılaştırma Sistemi (Dual-Verification) & EXE Üretici Firma Taraması
- **Sorun:** Yerel oyun dizinlerindeki DirectX setup (`dxwebsetup.exe`), uninstaller veya crash handler dosyaları bazen oyun zannedilip seçiliyordu.
- **Çözüm:**
  - **1. Kademe (Oyun Varlıkları & Motor Denetimi):** Tarama derinliği 5'e çıkarıldı. Unreal Engine (`*-Win64-Shipping.exe`, `.pak`, `.ucas`), Unity (`*_Data`, `UnityPlayer.dll`) ve Source (`.vpk`) motor dosyaları önceliklendirildi. `_Redist`, `Redist`, `DirectX`, `Prerequisites` yan klasörleri elendi.
  - **2. Kademe (EXE İç Metadata & Üretici Firma):** `FileVersionInfo` PE başlıkları okunarak `CompanyName` (Üretici Firma: örn. `CJGameLab`), `ProductName` ve `FileDescription` analiz edildi. Setup ve uninstaller araçları kesin olarak elendi.
  - **Doğrulama:** `Dispatch` için DirectX setup yerine 73.5 MB'lık gerçek `Dispatch-Win64-Shipping.exe` ikili dosyası, `S2` için ise `TheRaw.exe` ve yapımcısı `CJGameLab` başarıyla eşleştirildi.

---

## 📦 Hazırlanan Paketler ve Dosya Yolları

| Paket Türü | Dosya Adı / Konumu | Boyut | Açıklama |
| :--- | :--- | :--- | :--- |
| **Windows Kurulum Sihirbazı** | `ODZEN_INSTALLER_OUTPUT\ODZEN_Setup_v1.4.2.exe` | 15.8 MB | Modern Inno Setup 6 Türkçe/İngilizce/Çok Dilli Kurucu |
| **Taşınabilir ZIP (x64)** | `ODZEN-v1.4.2-x64.zip` | 25.7 MB | Kurulum gerektirmeyen bağımsız taşınabilir sürüm |
| **Microsoft Store MSIX** | `Taroxzen.ODZENGameLibrary_1.4.2.0_x64.msix` | 26.3 MB | Microsoft Store / Windows App Installer dijital imzalı paket |
