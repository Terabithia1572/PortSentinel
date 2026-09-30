# Test raporu — 30 Eylül 2026

## Sonuç

Release build: **başarılı, 0 hata / 0 uyarı**. Kilitli restore ve iki x64 framework-dependent publish başarılı. **24 unit + 18 integration = 42 başarılı; 0 başarısız, 0 atlanan**. Beş WPF ekranı gerçek geliştirme servisine bağlandı, ekran görüntüleri üretildi, binding hata logu boş; süreç başarılı kapanış kodu üretti.

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

Kısıtlı token testi ayrı bir yerel standart kullanıcı hesabıyla SCM kabulünün yerine geçmez. Üretim istemcisinin SCM PID eşleştirmesi ve LocalService altında keşif/DB izinleri kurulu servis üzerinde ayrıca test edilmelidir.

## WPF / gerçek servis smoke

Servis `--development` ile başlatıldı; gerçek migration, SQLite, keşif/tanılama ve named pipe çalıştı. Masaüstü beş tab'ı açıp render etti. WPF kapanışında dispose hatası ve IPC yanıt kapanış yarışı doğrulama sırasında bulundu, düzeltildi; son çalıştırmalar başarıyla tamamlandı. WPF kapandıktan sonra ayrı servis süreci çalışmaya devam etti; tekrar açılan WPF yeniden bağlandı.

Kanıt dosyaları proje kökünde:

- `artifacts/test-results/portsentinel_net10.0_20260930203721.trx` (24 unit)
- `artifacts/test-results/portsentinel_net10.0_20260930203728.trx` (18 integration)
- `artifacts/desktop-screen-0.png` … `desktop-screen-4.png`
- `artifacts/desktop-smoke.txt`, `artifacts/desktop-bindings.log`
- `artifacts/publish/checksums.json`
- `artifacts/publish/THIRD-PARTY-NOTICES.json` (50 paket lisans metadata kaydı)

Son publish ikilileriyle servis–WPF bağlantısı ayrıca çalıştırıldı. PE header x64 olarak doğrulandı; paket dosyalarının SHA256 manifesti kontrol edildi. Smoke sırasında bağlı USB sayısı sıfırdı. Test için başlatılan servisler teslimat öncesinde durduruldu; Windows servis kaydı oluşturulmadı.

## Çalıştırılan komutlar

`scripts/Build.ps1`: `dotnet restore --locked-mode`, Release build, no-build test ve Service/Desktop publish. EF migration kaynakları üretildi; Release model/snapshot tutarlılığı kontrol edildi. Bütün PowerShell dosyaları parser ile doğrulandı; Install/Uninstall/Test-Hardware gerçek makinede çalıştırılmadı.

## Donanım / kurulum kabulü — çalıştırılmadı

Yetkisiz yeni/önceden kurulmuş/boot öncesi USB, fiziksel 10 cihaz, hub/port, gerçek çok bölüm/LUN, UASP, seri çakışması, açık handle/yazma iptali, servis duruşunda/reboot'ta koruma, keyboard/mouse/internal disk etkilenmeme, GPO/MDM priority, lisans/engine readback, yetkili kurulum/kaldırma ve değişmiş politikaya dokunmayan geri alma. [Ayrı kabul protokolü](DONANIM-KABUL.md).

## Bilinen sınırlar

Fiziksel engelleme backend'i yok; yalnız lisanslı motor adayı ve inceleme XML'i var. Polling olayları kısa bağlantıları kaçırabilir. Mount-point-only volume ayrıntıları tam toplanmaz. Donanım kimliği taklit edilebilir. Windows 10 üzerinde ayrı kabul testi yapılmadı. Kurulum betiklerinin sözdizimi ve Windows 11 üzerindeki salt okunur ön kontrol doğrulandı; gerçek SCM/ACL/recovery yetenekleri kurulmuş VM'de kabul testi bekler.
