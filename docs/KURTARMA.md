# Kurtarma, güncelleme ve kaldırma

## Bu sürümde politika sahipliği

PortSentinel Windows/GPO/MDM erişim politikalarına yazmaz. `policy-baseline.json` salt okunur başlangıç kaydıdır; `WindowsPolicyChanges=[]`. Kaldırma USBSTOR'u açmaz, DeviceInstall/Defender ayarlarını silmez, başka yönetim aracının politikasını geri yüklemez. XML önizlemesini elle başka araca dağıtırsanız o dağıtımın sahipliği ve geri alması PortSentinel'e ait olmaz; ilgili GPO/Intune üzerinden yönetin.

## Servis veya DB hatası

1. Yönetici PowerShell ile `Get-Service PortSentinel` ve korumalı `C:\ProgramData\PortSentinel\logs` altındaki JSON loglarını kontrol edin. Correlation ID, başarısız komutu bulur.
2. Servis DB/migration erişiminde başarısızsa süreç başarısız çıkış kodu üretir. SCM recovery yeniden başlatabilir. Arayüzün servis bağlantısı yokken gösterdiği liste güncel değildir.
3. Yedekleme/geri yükleme öncesi `Stop-Service PortSentinel` çalıştırın. SQLite DB ve varsa WAL/SHM dosyalarını birlikte, yalnız yönetici erişimli yedek dizinine kopyalayın; canlı DB'yi tek dosya olarak kopyalamayın.
4. ACL'leri kaldırmayın veya Everyone yazma izni vermeyin. Yedekleri ProgramData ACL'leri korunarak geri yükleyin. Sahiplik kilidi servis açıkken ikinci servisin başlamasını reddeder.
5. `Start-Service PortSentinel`; istenen kayıtları ve Pending→Interrupted uzlaştırmasını kontrol edin. Kaydedilmiş izinleri Windows'ta uygulandı sanmayın.

Servis/DB hatasında cihazları otomatik serbest bırakan kod yoktur. Bu sürüm başlangıçta da koruma sağlamadığı için “fail closed engel devam ediyor” iddiası da yoktur. Lisanslı dış motorun davranışı ayrı doğrulanır.

## Kaldırma

Setup EXE ile kurulduysa masaüstünü kapatın ve Windows Uygulamalar listesinden veya Başlat menüsünden PortSentinel kaldırıcıyı çalıştırın. Inno Setup kaldırıcı dosyalardan önce servis sahipliğini kontrol eder ve servisi kaldırır; ProgramData korunur. Bu kurulumda doğrudan PowerShell kaldırıcı dosyaları silmez. [Setup ayrıntıları](SETUP.md).

PowerShell/ZIP ile kurulduysa masaüstünü kapatın; kaynak paketten yükseltilmiş PowerShell:

```powershell
.\scripts\Uninstall.ps1
```

Kaldırıcı servis binary yolu, korumalı receipt ve kurulum kökü kapsamını doğrular; yalnız receipt'teki hash'i değişmemiş dosyaları siler. Sonradan değişen dosya varsa hiçbir dosya silmeden reddeder. Bilinmeyen dosyalar ve ProgramData/DB/audit/başlangıç yedeği korunur. Dizindeki reparse point'ler reddedilir. Windows politikaları değişmez. Kaldırma canlı sistemde henüz çalıştırılmadı; VM kabul testi gereklidir.

## Güncelleme

Bu teslimatta yerinde upgrade kurucusu yoktur. Servisi durdurup korumalı DB yedeği alın, eski paket kaldırıcıyla dosyaları kaldırın, yeni paketi build edip kurun. Korunan ProgramData receipt veri sahipliğini gösterir. Yeni migration'ların geri uyumluluğu ayrıca incelenmelidir; eski binary ile yeni DB şemasına dönmeyin.

Kurulum başarısızsa betik yalnız o çalıştırmada oluşturduğu servis kaydını kaldırır; kopyalanmış dosyalar/receipt kalabilir. Kaldırıcıyla temizleyip tekrar kurun. Hata sonrası klasörleri genel recursive delete ile temizlemeyin.

## Üretim motoru için geri alma şartı

Gelecek motor, değişiklik öncesi anahtar/GPO/XML snapshot'ı ve sahiplik kaydı tutmalı; geri alma yalnız current value/hash hâlâ uygulamanın yazdığı değerle eşleşiyorsa yapılmalıdır. Sonradan domain/MDM tarafından değişen değer conflict sayılmalı ve korunmalıdır. Bu protokol şu anda uygulanmıyor; bu nedenle bu sürüm bir politika değişikliği içeren receipt'i geri almayı reddeder.
