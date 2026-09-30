# PortSentinel Windows kurucusu

`installer/PortSentinel_Setup.iss`, Yunus İNAN'ın `TerabithiaDesktop_Setup.iss` dosyasındaki geliştirici bilgilerini, GitHub bağlantılarını, Türkçe modern sihirbazı, LZMA2 sıkıştırmasını, Başlat menüsü ve isteğe bağlı masaüstü kısayollarını temel alır. PortSentinel ayrı uygulama kimliği ve kendi USB/kalkan simgesi kullanır. Terabithia/RustDesk ikilileri ve lisans açıklaması bu pakete dahil değildir.

## Derleme

.NET SDK 10.0.401 ve Inno Setup 6 kurulu geliştirme makinesinde:

```powershell
.\scripts\Build-Setup.ps1
```

Betik kilitli restore, Release build, .NET testleri, Windows PowerShell 5.1 kurulum regresyonları, self-contained win-x64 publish, bağımlılık lisansları ve SHA256 manifesti üretir; sonra Inno Setup derlemesini çalıştırır. Çıktı `artifacts/installer/PortSentinel-Setup-1.0.2.exe`; kontrol özeti aynı dizinde `.sha256` dosyasıdır. Sürüm `Directory.Build.props` dosyasından alınır. Derleyici farklı konumdaysa `-InnoCompiler 'C:\...\ISCC.exe'` kullanılabilir. Betik kurulumu başlatmaz. Setup .NET 10.0.12 x64 çalışma zamanlarını içerir; kullanıcı ayrıca .NET kurmaz. Kaynak `Build.ps1` ile oluşturulan ZIP ise framework-dependent olmaya devam eder.

## Kurulum ve kaldırma

Kurucu yönetici yetkisi ister. Windows 10/11 x64, mevcut servis/dizin sahipliği ve reparse kontrolleri dosyalar kopyalanmadan yapılır. Home/Pro/Enterprise/Education edisyonu, güncelleme sürümü veya destek tarihi üzerinden engel yoktur; Windows 11 23H2 de kabul edilir. Sabit hedef `C:\Program Files\PortSentinel`; veri `C:\ProgramData\PortSentinel`. LocalService hesabıyla gecikmeli otomatik başlayan servis, ACL'ler ve kurtarma ayarları yapılandırılır. Servis kaydı başarısızsa kurucu başarılı tamamlanma ekranını göstermez; ayrıntılar Inno Setup günlüğüne yazılır.

Kurucu **USB dosya erişimi engelini etkinleştirmez**. Bu mevcut yönetim/keşif sürümüdür; Windows/GPO/MDM USB politikalarını değiştirmez. Bu durum karşılama ve kurulum bilgisi ekranlarında belirtilir.

Windows **Ayarlar → Uygulamalar → Yüklü uygulamalar → PortSentinel → Kaldır** yolunu veya Başlat menüsündeki kaldırıcıyı kullanın. Kullanıcı kaldırmayı onayladıktan sonra, dosyalar silinmeden önce servis yolu, receipt/paket işareti, dosya hash'leri ve reparse kontrol edilir. Servis durdurulur ve kaydı kaldırılır; betik yalnız doğrulanmış paket dosyalarını siler. Inno Setup kendi kaldırıcı kayıtlarını ve kısayollarını kaldırır. ProgramData veritabanı, audit, ayarlar ve başlangıç kaydı korunur. Veri klasörü hiç oluşmamış yarım kurulum, paket manifesti üzerinden kaldırılabilir. Inno kurulumunda doğrudan `Uninstall.ps1` yerine Windows kaldırıcı kullanılmalıdır.

1.0.2 setup'ı sahipliği doğrulanmış eski/yarım kurulumu yerinde onarır/günceller. Önce PortSentinel pencerelerini kapatın. Mevcut veri korunur; güncelleme öncesi korumalı yedek önerilir. Değiştirilmiş paket dosyaları, bilinmeyen çakışan dosyalar, farklı binary'ye bağlı aynı adlı servis veya reparse point varsa kontrol durur. Genel recursive delete kullanılmamalıdır. [Kurtarma belgesi](KURTARMA.md).

### Kritik kurulum kapısı

Inno'nun dosya `BeforeInstall`/`AfterInstall` callback hataları kurulum işlemini durdurmaz; 1.0.1'in yanlış başarı bildirimi bu yoldan oluştu. 1.0.2 paketi geçici dizine açar ve `PrepareToInstall` içinde hash/sahiplik doğrulama, payload kopyalama, ACL, servis kaydı ve IPC kabulünü tamamlar. Hata dönerse normal kurulum ve başarı ekranına geçilmez. `[Files]` aynı dosyaları `onlyifdoesntexist` ile tanımlar; çalışan ikililer yeniden yazılmaz. Payload kaldırma betiği üzerinden yönetilir; Inno kaldırıcı, kısayollar ve uygulama kaydını yönetir. Başarısız denemede dosyalar/receipt kurtarma için kalabilir; setup tekrar çalıştırılabilir.

## Doğrulama sınırı

Windows 11 Pro 23H2/build 22631 üzerinde gerçek Inno setup, LocalService/SCM kaydı, tırnaklı binary yolu, korumalı SQLite erişimi, üretim IPC/PID doğrulaması ve beş WPF ekranı çalıştırıldı. 1.0.1 yarım kurulum onarımı, temiz kurulum, çalışan servis onarımı, normal kaldırma, veritabanı/bilinmeyen dosya korunması, veriyle tekrar kurulum ve ProgramData/receipt eksikken kaldırma doğrulandı. 10 Windows PowerShell 5.1 regresyonu ve 42 .NET testi geçti. Boş test makinesinde `scripts/Test-InstallerLifecycle.ps1` aynı SCM kabul akışını çalıştırır; mevcut kurulum varsa teste başlamaz. Windows 10 üzerinde ayrı çalıştırma, yeniden başlatma ve USB donanım kabulü yapılmadı. EXE kod imzalı değildir.
