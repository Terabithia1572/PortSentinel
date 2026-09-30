# Değişiklik günlüğü

## 1.0.2 — 30 Eylül 2026

- 1.0.1 kurulum hatası gerçek setup ile yeniden üretildi: Windows PowerShell 5.1 JSON manifestini iç içe dizi okuyordu; servis kayıt betiği `ChildPath` hatasıyla duruyordu. Manifest okuma iki PowerShell sürümünde uyumlu hale getirildi.
- Inno Setup `AfterInstall` istisnalarını yuttuğundan, başarısız servis kurulumu yanlışlıkla başarılı gösteriliyordu. Kritik kurulum `PrepareToInstall` aşamasına taşındı; SCM/LocalService ve gerçek Named Pipe snapshot yanıtı doğrulanmadan kurulum devam etmez.
- Sahipliği doğrulanmış yarım/eski kurulum yerinde onarılır. Eksik ProgramData veya henüz oluşturulmamış servis, tekrar kurulumu engellemez.
- Kaldırıcı eksik veri/receipt durumunda paket işareti ve hash manifestinden sahiplik doğrular; yalnız değişmemiş paket dosyalarını kaldırır. Bilinmeyen dosyalar ve mevcut ProgramData korunur.
- Servis ilk uzun CIM taramasından önce IPC dinlemeye başlar. Arayüz kurulu olmayan/duran servisi Türkçe ve yapılacak işlemi belirten mesajla gösterir.
- Inno Restart Manager'ın kurulumdan hemen sonra yeni servisi durdurması önlendi; açık PortSentinel pencereleri için kapatma isteği gösterilir. SCM binary yolu Windows PowerShell tırnak işlemesinden bağımsız, tırnaklı olarak kaydedilir.
- LocalService'in okuyamadığı yönetim registry anahtarları tanılamada ayrı raporlanır; diğer göstergeler kaybolmaz.
- Kaldırıcı boş uygulama dizinini de temizler; eski kaldırıcıdan kalmış tamamen boş dizin tekrar kurulumu engellemez.
- Windows PowerShell 5.1 üzerinde 10 kurulum/sahiplik regresyonu ve gerçek SCM kurulum–onarım–kaldırma kabul akışı CI'a eklendi. Windows 10/11 x64 hedefi ve geliştirici Yunus İNAN korunur.

## 1.0.1 — 30 Eylül 2026

- Windows 10 ve Windows 11 x64 kurulum hedefi genişletildi.
- Home/Pro/Enterprise/Education edisyonu, 24H2/25H2 güncelleme sürümü ve destek tarihi üzerinden uygulanan kurulum engelleri kaldırıldı.
- Hem Inno Setup ön kontrolü hem servis kurulum betiği güncellendi; tanılama ekranındaki hedef bilgisi aynı davranışa getirildi.
- .NET 10.0.12 x64 çalışma zamanını içeren Türkçe `PortSentinel-Setup-1.0.1.exe` üretildi.
- Paket sürümü tek `Directory.Build.props` kaynağından okunur; geliştirici **Yunus İNAN** olarak korunur.
- Windows 11 Pro 23H2 üzerindeki gerçek Windows PowerShell ön kontrolü ve self-contained servis/WPF smoke testi başarılı.
- 24 unit ve 18 integration testi başarılı; WPF beş ekranında binding hatası yok.

Fiziksel USB erişim engelleme motoru bu sürümde eklenmemiştir. Windows 10 üzerinde ayrı çalıştırma ve gerçek kurulum/kaldırma kabul testi henüz yapılmadı.

## 1.0.0 — 30 Eylül 2026

- Türkçe WPF/MVVM yönetim arayüzü, ayrı Windows Worker Service ve Named Pipe IPC.
- Fiziksel USB ata düğümüne dayalı disk/bölüm/volume keşfi.
- SQLite migration, kalıcı cihaz izin kayıtları, revizyon ve denetim günlüğü.
- Gerçek Windows token yetkilendirmesi; standart kullanıcı görüntüleme, yükseltilmiş yönetici işlemleri.
- Salt okunur OS/Defender/GPO göstergeleri ve uygulama yapmayan Defender XML önizlemesi.
- Framework-dependent ZIP build betiği, servis kurulum/kaldırma betikleri ve self-contained Inno Setup kurucusu.
- İlk kurucudaki dar Windows 11 Pro 24H2/25H2 kısıtı 1.0.1'de kaldırıldı.
