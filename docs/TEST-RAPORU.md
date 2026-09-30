# Test raporu — 30 Eylül 2026

## Sonuç

Release build: **başarılı, 0 hata / 0 uyarı**. **24 unit + 18 integration = 42 başarılı; 0 başarısız, 0 atlanan**; ek **10 Windows PowerShell 5.1 kurulum regresyonu** geçti. 1.0.2 Inno setup ile gerçek LocalService/SCM kurulumu, üretim IPC/PID doğrulaması ve beş WPF ekranı çalıştırıldı; binding hata logu boş ve WPF çıkışı 0.

Bu sonuçlar fiziksel USB dosya erişimi engelini kanıtlamaz. Bu teslimatta Windows politika yazma motoru yoktur. Ürünün asıl erişim denetimi kabulü **tamamlanmadı**.

## Ortam

Windows 11 Pro 23H2 x64, build 22631; .NET SDK 10.0.401 / runtime 10.0.12. 1.0.1 ile edisyon/güncelleme/destek tarihi kısıtları kaldırıldı; bu ortam Windows 10/11 x64 kurulum hedefidir. Defender platform 4.18.26080.4; DeviceControlState=Disabled. Salt okunur taramada bağlı USB depolama sayısı **0**. Defender Policy Manager ve Enrollments anahtarları mevcut; bunlar yönetim/çatışma incelemesi göstergesi olarak raporlandı. MDE lisansı/onboarding doğrulanmadı.

## Simülasyon / saf iş mantığı — 24 unit

10 cihazda bağımsız ret/izin; aynı model farklı seri; eksik/unsafe seri ve UniqueID; yinelenen fiziksel kimlik; port/hub anahtar sürekliliği; sistem diski ve kapsam dışı cihazın korunması; çok bölüm/UASP temsilinin tek fiziksel kararı; doğrulanmamış backend'de etkin ret iddiası olmaması; anahtar normalizasyonu. IPC framing boyut/truncation, enum/unknown admin alanı, JSON roundtrip ve FluentValidation sınırları. XML'de MatchAll/scope, exclusion ve disk+dosya maskesi 63; XML escaping; belirsiz izin önizlemesinin reddi.

**Bu cihaz senaryoları model girdileriyle simüle edildi.** Gerçek port, USBSTOR/UASP, ilk kurulum ve boot erişim testleri sayılmaz.

## SQLite / uygulama entegrasyonu — 13 test

Gerçek geçici SQLite dosyasında migration, unique index/foreign key, restart kalıcılığı, duplicate kayıt ve eski revizyonun atomik reddi; izin iptali, auth, retention, Pending→Interrupted. Pipeline: standart caller'ın yönetim komutunun audit ile reddi; 10 eşzamanlı grant'ta 1 başarı/9 revision conflict; kaybolan cihaz; scan hatasında kayıt reddi/stale işareti; backend hata durumunda istenen kayıt korunup Failed olması; DB yokken correlation ID'li hata.

Bu gruptaki cihaz envanteri/backend yalnız test projesindeki kontrollü double'lardır. Üretim kodu mock backend kullanmaz. SQLite ve transaction davranışı gerçektir.

## Gerçek Windows entegrasyonu — 5 test

- **WindowsIpc (3):** gerçek Named Pipe + ACL, sunucuda gerçek Windows token rolü; Administrators SID'si deny-only yapılmış ve ayrıcalıkları kısıtlanmış token'ın Settings komutunda Unauthorized alması; sahte admin alanının reddi ve sonraki bağlantının çalışması.
- **WindowsReadOnly (2):** gerçek CIM/PnP envanter betiğinin çalışması (bağlı USB bulunmadı); gerçek OS/Defender tanılamasının korumayı doğrulanmamış göstermesi.

Kısıtlı token testi ayrı bir yerel standart kullanıcı hesabının yerine geçmez. 1.0.2'de üretim SCM PID eşleştirmesi, LocalService altında keşif/tanılama ve SQLite/ACL erişimi gerçek kurulu serviste doğrulandı; ayrı standart kullanıcı oturumu henüz denenmedi.

## WPF / gerçek servis smoke

Servis `--development` ile başlatıldı; gerçek migration, SQLite, keşif/tanılama ve named pipe çalıştı. Masaüstü beş tab'ı açıp render etti. WPF kapanışında dispose hatası ve IPC yanıt kapanış yarışı doğrulama sırasında bulundu, düzeltildi; son çalıştırmalar başarıyla tamamlandı. WPF kapandıktan sonra ayrı servis süreci çalışmaya devam etti; tekrar açılan WPF yeniden bağlandı.

Kanıt dosyaları proje kökünde:

- `artifacts/test-results/portsentinel_net10.0_20260930203721.trx` (24 unit)
- `artifacts/test-results/portsentinel_net10.0_20260930203728.trx` (18 integration)
- `artifacts/desktop-screen-0.png` … `desktop-screen-4.png`
- `artifacts/desktop-smoke.txt`, `artifacts/desktop-bindings.log`
- `artifacts/publish/checksums.json`
- `artifacts/publish/THIRD-PARTY-NOTICES.json` (50 paket lisans metadata kaydı)

1.0.2 kurulu self-contained ikililerle WPF `--smoke-test` geliştirme bayrağı olmadan çalıştırıldı. SCM Running/LocalService, tırnaklı binary yolu ve pipe sunucusu PID eşleşti. Beş ekran render edildi, binding logu 0 byte; arayüz `Servis bağlı · IPC doğrulandı` gösterdi. LocalService'in erişemediği Defender Policy Manager anahtarı ayrı tanılama satırında raporlandı. Bağlı USB sayısı sıfırdı.

## Gerçek kurulum kabulü — 1.0.2

1.0.1 EXE'siyle kullanıcı hatası aynen yeniden üretildi: PowerShell 5.1 manifest dizisini yanlış okuyarak `ChildPath` hatası verdi; `AfterInstall` hatası yutulduğu için setup çıkışı 0 oldu, servis/veri dizini oluşmadı. Yeni setup kritik işlemleri `PrepareToInstall` içinde yapar; başarısız denemeler çıkış 7 ile durdu ve başarı ekranına geçmedi.

- 1.0.1'den kalan, servis/ProgramData bulunmayan paket dosyalarının yerinde onarımı.
- LocalService hesabıyla Running servis, tırnaklı SCM yolu ve gerçek snapshot/PID kabulü.
- Temiz kurulum ve çalışan servisin yerinde yeniden kurulması.
- Normal kaldırmada servis/payload kaldırılması, SQLite'ın byte-for-byte korunması ve bilinmeyen dosyanın korunması.
- Korunan veriyle tekrar kurulum.
- ProgramData/receipt bulunmayan durumda kaldırma; boş uygulama dizini de temizlenir.

`scripts/Test-SetupScripts.ps1`: Windows PowerShell 5.1 JSON dizi okuma, eksik/boş veri dizini, eski kaldırıcıdan kalan boş program dizini, eksik payload onarımı, path traversal, değiştirilmiş dosya ve yabancı servis/dizin kontrolleri — **10 başarılı**.

`scripts/Test-InstallerLifecycle.ps1`: yalnız başlangıçta servis/uygulama/veri dizini olmayan yönetici test makinesinde çalışır. Gerçek kurulum, çalışan servis onarımı, kaldırma, veriyle tekrar kurulum ve eksik veri dizini kaldırmasını kontrol eder; test verisini çalışma alanındaki artifact dizinine taşır. Sürüm tag/manuel installer CI akışına eklendi. Yerel kanıtlar `artifacts/installer/acceptance-*.log/json` ve `artifacts/lifecycle/*/verification.json` altında; makineye özel loglar kaynak Git geçmişine eklenmez.

## Çalıştırılan komutlar

`scripts/Build.ps1` ve `scripts/Build-Setup.ps1`: kilitli restore, Release build, test ve Service/Desktop publish. EF migration/model tutarlılığı doğrulandı. Setup/Install/Uninstall gerçek makinede çalıştırıldı; Test-Hardware çalıştırılmadı.

## Donanım / yeniden başlatma kabulü — çalıştırılmadı

Yetkisiz yeni/önceden kurulmuş/boot öncesi USB, fiziksel 10 cihaz, hub/port, gerçek çok bölüm/LUN, UASP, seri çakışması, açık handle/yazma iptali, servis duruşunda/reboot'ta koruma, keyboard/mouse/internal disk etkilenmeme, GPO/MDM priority ve lisans/engine readback. [Ayrı kabul protokolü](DONANIM-KABUL.md).

## Bilinen sınırlar

Fiziksel engelleme backend'i yok; yalnız lisanslı motor adayı ve inceleme XML'i var. Polling olayları kısa bağlantıları kaçırabilir. Mount-point-only volume ayrıntıları tam toplanmaz. Donanım kimliği taklit edilebilir. Windows 10 üzerinde ayrı kabul ve reboot/SCM failure-recovery senaryoları yapılmadı. Windows 11 üzerinde gerçek kurulum/ACL/LocalService/IPC/kaldırma akışı kabul edildi.
