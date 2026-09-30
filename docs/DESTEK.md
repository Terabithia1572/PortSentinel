# Windows uyumluluğu — 30 Eylül 2026

Kurulum hedefi **Windows 10 ve Windows 11 x64**. Kurucu ve servis kurulum betiği Home, Pro, Enterprise, Education veya belirli bir güncelleme sürümü için ayrı engel uygulamaz. İşletim sistemi destek tarihi kurulum kabul kararına dahil edilmez. 1.0.1 ile önceki Pro 24H2/25H2 ve yaşam döngüsü kısıtları kaldırılmıştır.

Mevcut paket x64 ikililer içerir; 32 bit Windows veya ARM64 için ayrı paket üretilmemiştir. Windows 7/8 gibi eski işletim sistemleri bu .NET 10 paketinin hedefi değildir. .NET çalışma zamanı setup içine dahildir. Microsoft'un resmi [.NET OS destek listesi](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md), bu uygulamanın kurulumu kabul etmesinden ayrı bir konudur; kurucu bu liste üzerinden Windows 10/11'i engellemez.

Windows 11 Pro 23H2/build 22631 x64 üzerinde build, salt okunur tanılama, gerçek pipe/token, SQLite, WPF ve 1.0.2'nin gerçek Inno setup/LocalService/SCM kurulumu çalıştırıldı. Üretim IPC/PID doğrulaması, temiz kurulum, yerinde onarım, kaldırma/veri koruma, tekrar kurulum ve eksik ProgramData ile kaldırma doğrulandı. Windows 10 üzerinde ayrı kabul, yeniden başlatma ve tüm edisyon/güncelleme kombinasyonları test edilmiş sayılmaz.

Windows tanılaması edisyonu, güncelleme sürümünü, build'i ve CPU mimarisini bilgi amacıyla gösterir. ProductName bazı Windows 11 sistemlerinde Windows 10 adını taşıyabildiği için kurulum kabul kararı ona dayanmaz. Windows 10/11 ailesi registry major sürümü ve x64 CPU ile tanınır.

- **USBSTOR:** keşif adaptörü mevcut; fiziksel USB üzerinden test edilmedi.
- **UASP/SCSI görünümü:** USB ata düğümü üzerinden keşif tasarlandı; aygıt ve XML scope eşleşmesi donanımla doğrulanmadı.
- **Çok bölüm/LUN/portsuz volume:** fiziksel düğüm ve bölüm ilişkisi toplanır; sürücü harfi olmayan mantıksal mount-point ayrıntıları tam çıkarılmaz.
- **Hub/farklı port:** stable serial anahtarı porttan bağımsız; gerçek Windows devnode devamlılığı test edilmedi. UniqueID/seri eksikse izin oluşturulmaz.
- **MDE Device Control:** belgelenmiş üretim adayı; uygun lisans, platform, dağıtım kanalı ve donanım kabulü gerekir. Bu uygulama etkinleştirme yapmaz.

[Defender önkoşulları](https://learn.microsoft.com/en-us/defender-endpoint/device-control-overview).

Windows kurulum kapsamının genişlemesi fiziksel USB engelleme eklemez. Bu sürüm yönetim/keşif sürümüdür ve mevcut Windows/GPO/MDM politikalarını değiştirmez.
