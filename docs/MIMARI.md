# Mimari ve işlem tutarlılığı

## Bağımlılık yönü

`Domain ← Application ← Infrastructure ← Service/Desktop`. Contracts yalnız taşıma protokolü/DTO içerir; Application açık mapping yapar. Domain'de EF/WPF/Windows/dış paket bağımlılığı yoktur. Desktop composition root yalnız IPC istemcisini ve viewmodel'i kurar; Infrastructure referansı Windows pipe sunucu kimliği adaptörü içindir. Desktop EF DbContext veya ayrıcalıklı cihaz API'sini çağırmaz.

- **Domain:** fiziksel kimlik, kapsam, erişim değerlendirmesi ve yedi temel kayıt modeli.
- **Application:** doğrulama (FluentValidation), yetkilendirme, kullanım senaryoları, correlation ID, hata dönüşümü, politika önizleme derleyicisi. Tek koordinatör kilidi tarama/izin işlemlerini sıralar.
- **Infrastructure:** EF migration/SQLite, gerçek Windows keşfi, salt okunur Defender/OS/politika tanılaması, açık pipe ACL'leri, SCM sunucu PID doğrulaması, korumalı dizin kontrolü.
- **Service:** DI, WindowsService lifetime, başlangıç uzlaştırması, periyodik tarama, migration, dönen JSON logları. Veritabanı sahiplik kilidi ikinci servis sürecini reddeder.
- **Desktop:** WPF, MVVM, async komutlar, periyodik IPC yenileme. Bağlantı hatasında yönetim kapatılır ve son bilinen liste güncel kabul edilmez.

Windows adaptörleri yalnız iki gömülü ve sabit PowerShell betiğini çağırır. İstemcinin betik, executable, registry anahtarı veya dosya yolu gönderebileceği bir operation yoktur. CIM/PnP taraması kontrol değil, keşiftir. Diskler USB VID/PID ata düğümüne kadar izlenir; çok LUN aynı fiziksel düğümde birleştirilir. Ebeveyn özelliği okunamazsa cihaz keşfedilemeyebilir; bu bir güvenlik garantisi değildir. Get-Disk sistem/boot bilgisi okunamazsa izin uygunluğu reddedilir.

## İstenen ve etkin politika

İzin değişikliği, cihaz kaydı/rule/audit ve `Pending` revizyonu aynı SQLite transaction'ında yazar. `ExpectedRevision` eskiyse conflict döner. Backend sonucu ayrı aşamada `Unverified` veya `Failed` kaydeder. Servis kapanırsa `Pending` başlangıçta `Interrupted` olur. İstenen kayıtlar yeniden başlatmada korunur. Bu sürüm `EffectiveRevision=null`, `VerifiedUtc=null` ve koruma=false döndürür. Mevcut Windows politikasını okumak ya da XML üretmek başarı sayılmaz.

IPC CommandDto'daki `DesiredSaved=true`, yalnız DB transaction'ının tamamlandığını ifade eder. `EnforcementVerified=false` ve açıklama ayrıca gösterilir. DB kaydından sonra backend/bağlantı hatası olursa istemci listeyi yenileyerek sonucu öğrenir; komutlar otomatik tekrar edilmez. Böylece aynı grant/revoke yanlışlıkla tekrarlanmaz.

Keşif hata verdiğinde önceki gözlemler saklanır fakat InventoryError ile işaretlenir; grant için mutlaka yeni başarılı tarama gerekir. Fiziksel çıkarma taramadan hemen sonra gerçekleşebilir; tanıtıcının o anda hâlâ bağlı olduğu kesin garanti edilmez. Bu aşama yalnız istenen izin kaydıdır.

## IPC ve kimlik

Pipe byte framing: 4 byte little-endian uzunluk, JSON, sürüm=1; istek en fazla 64 KiB, yanıt en fazla 1 MiB, JSON derinliği 32. Bilinmeyen alan/enum, truncated frame, geçersiz boyut reddedilir. Bağlantı/işlem timeout ve CancellationToken vardır. Tek bağlantıda tek operation; pipe tekrar kullanılır. Sunucu yanıtı istemci okuyup kapatana kadar kısa süre bekler, böylece Disconnect unread byte yarışını önler.

ACL: LocalSystem/servis sahibi yönetir; local authenticated users ve Administrators bağlanabilir; Network SID reddedilir. Sunucu `RunAsClient` ile Windows token'ını okur; `WindowsPrincipal.IsInRole(Administrator)` yükseltilmemiş/kısıtlı token'ı yönetici saymaz. Snapshot görüntüleme dışındaki tüm işlemler admin ister. Üretim istemcisi pipe PID'sini kayıtlı PortSentinel SCM PID'si ve Running durumuyla karşılaştırır. Bu üretim eşleştirmesi kurulu serviste ayrıca kabul testi bekler; test pipe'ı geliştirme seçeneğiyle bunu atlar.

## Depolama ve log

Tek DB sahibi servis. ProgramData dizini yalnız SYSTEM, Administrators ve LocalService yazabilir; dosya/dizin reparse point veya geniş yazma ACL'si varsa servis reddeder. LocalService seçimi read-only CIM/Defender ve kendi DB/pipe ihtiyaçları içindir; LocalSystem gerekmez. Başka LocalService süreçlerinden izolasyon ek service SID hardening ile güçlendirilebilir; bu teslimatta sağlanmaz.

EF owned identity, unique identity key, unique device rule ve foreign key kullanılır. Migration kaynakları depodadır. Zamanlar DateTime.UtcNow ile yazılır, arayüz ToLocalTime kullanır. JSON dosya logları 5 MiB × en fazla 30 dosya / 30 gün; olay/audit tabloları 1–90 günlük retention ve 10.000 satırla sınırlandırılır. Revizyonlar kaybedilmez; SQLite fiziksel dosya küçültme/VACUUM otomatik yapılmaz.

## Bilinen eksikler

Erişim denetimi motoru ve tam GPO/MDM kaynak çözümleme yoktur. Politika göstergeleri çatışma incelemesi gerektirdiğini söyler; hiçbir yönetim politikasını ezmez. RSoP ile kesin çatışma analizi, lisans/onboarding attestasyonu, engine politika hash readback, boot/UASP/handle doğrulaması tamamlanmadan bu ürünü güvenlik kontrolü olarak kullanmayın. PnP event subscription yerine 3–60 saniyelik polling/uzlaştırma kullanılır; kısa bağlantı olayları kaçabilir. Donanım kimlikleri taklit edilebilir.
