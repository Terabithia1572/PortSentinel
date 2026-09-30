# PortSentinel Windows kurucusu

`installer/PortSentinel_Setup.iss`, Yunus İNAN'ın `TerabithiaDesktop_Setup.iss` dosyasındaki geliştirici bilgilerini, GitHub bağlantılarını, Türkçe modern sihirbazı, LZMA2 sıkıştırmasını, Başlat menüsü ve isteğe bağlı masaüstü kısayollarını temel alır. PortSentinel ayrı uygulama kimliği ve kendi USB/kalkan simgesi kullanır. Terabithia/RustDesk ikilileri ve lisans açıklaması bu pakete dahil değildir.

## Derleme

.NET SDK 10.0.401 ve Inno Setup 6 kurulu geliştirme makinesinde:

```powershell
.\scripts\Build-Setup.ps1
```

Betik kilitli restore, Release build, otomatik testler, self-contained win-x64 publish, bağımlılık lisansları ve SHA256 manifesti üretir; sonra Inno Setup derlemesini çalıştırır. Çıktı `artifacts/installer/PortSentinel-Setup-1.0.1.exe`; kontrol özeti aynı dizinde `.sha256` dosyasıdır. Sürüm `Directory.Build.props` dosyasından alınır. Derleyici farklı konumdaysa `-InnoCompiler 'C:\...\ISCC.exe'` kullanılabilir. Betik kurulumu başlatmaz. Setup .NET 10.0.12 x64 çalışma zamanlarını içerir; kullanıcı ayrıca .NET kurmaz. Kaynak `Build.ps1` ile oluşturulan ZIP ise framework-dependent olmaya devam eder.

## Kurulum ve kaldırma

Kurucu yönetici yetkisi ister. Windows 10/11 x64, mevcut servis/dizin sahipliği ve reparse kontrolleri dosyalar kopyalanmadan yapılır. Home/Pro/Enterprise/Education edisyonu, güncelleme sürümü veya destek tarihi üzerinden engel yoktur; Windows 11 23H2 de kabul edilir. Sabit hedef `C:\Program Files\PortSentinel`; veri `C:\ProgramData\PortSentinel`. LocalService hesabıyla gecikmeli otomatik başlayan servis, ACL'ler ve kurtarma ayarları yapılandırılır. Servis kaydı başarısızsa kurucu başarılı tamamlanma ekranını göstermez; ayrıntılar Inno Setup günlüğüne yazılır.

Kurucu **USB dosya erişimi engelini etkinleştirmez**. Bu mevcut yönetim/keşif sürümüdür; Windows/GPO/MDM USB politikalarını değiştirmez. Bu durum karşılama ve kurulum bilgisi ekranlarında belirtilir.

Windows **Ayarlar → Uygulamalar → Yüklü uygulamalar → PortSentinel → Kaldır** yolunu veya Başlat menüsündeki kaldırıcıyı kullanın. Kullanıcı kaldırmayı onayladıktan sonra, dosyalar silinmeden önce servis yolu, receipt, dosya hash'leri ve reparse kontrol edilir; ardından servis durdurulur ve kaydı kaldırılır. Kontrol başarısızsa kaldırma durur. Inno Setup kendi dosyalarını ve kısayollarını kaldırır. ProgramData veritabanı, audit, ayarlar ve başlangıç kaydı korunur. Inno kurulumunda doğrudan `Uninstall.ps1` yerine Windows kaldırıcı kullanılmalıdır.

Yerinde yükseltme desteklenmez: korumalı veriyi yedekleyin, önce mevcut sürümü kendi kaldırıcısıyla kaldırın, ardından yeni paketi kurun. Kurulum başarısızsa kayıt/dosya durumu ve loglar incelenmeden genel recursive delete kullanılmamalıdır. Kısmi kurulum kurtarması için [kurtarma belgesi](KURTARMA.md).

## Doğrulama sınırı

Setup derlemesi, hash manifesti, dahil edilen runtime yapılandırmaları, PowerShell sözdizimi ve mevcut makinede salt okunur OS ön kontrolü doğrulanır. Windows 11 Pro 23H2/build 22631 artık kabul edilir. Servis/WPF geliştirme smoke testi Windows 11 üzerinde çalıştırılır; Windows 10 üzerinde ayrı gerçek kurulum/çalıştırma testi yapılmadı. Tüm Windows 10/11 güncellemelerinin aynı donanım davranışını göstermesi test edilmiş sayılmaz. Gerçek SCM kurulum, yeniden başlatma ve kaldırma kabul testi bekler. EXE kod imzalı değildir.
