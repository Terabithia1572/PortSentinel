# Named Pipe protokolü

Protokolün kaynak tanımı `src/PortSentinel.Contracts/Protocol.cs`; istemci/sunucu uygulamaları Infrastructure/Ipc altındadır. Bu belge 1.0.1 ve protokol sürümü 1 içindir.

## Bağlantı ve kimlik

Üretim pipe adı `PortSentinel.v1`, geliştirme adı `PortSentinel.Development.v1`. Her bağlantı bir istek/yanıt işlemi içindir. Byte-mode Named Pipe üzerinde ilk dört byte signed little-endian payload uzunluğudur; devamında UTF-8 JSON bulunur. İstek uzunluğu 1–65.536 byte, yanıt en fazla 1.048.576 byte; JSON derinlik sınırı 32.

Sunucu Windows istemci token'ını impersonation ile okur. `WindowsPrincipal.IsInRole(Administrator)` yükseltilmiş rolü belirler; deny-only yönetici SID'si yönetim yetkisi vermez. İstemci JSON'unda rol/SID veya script yolu gönderme işlemi yoktur. Pipe ACL local authenticated users ve Administrators bağlantısına izin verir; Network SID reddedilir. Üretim istemcisi sunucu PID'sini kayıtlı, Running durumdaki PortSentinel Windows servisiyle karşılaştırır. Geliştirme modunda yalnız bu SCM eşleştirmesi atlanır.

## İstekler

Her istekte `version` ve `operation` zorunludur. Operation string enum'dur; sayısal enum ve bilinmeyen alanlar reddedilir.

- `Snapshot`: mevcut görüntüyü döndürür; standart kullanıcıya açıktır.
- `Grant`: `deviceId`, 1–80 karakter `name` ve `expectedRevision`; yönetici gerekir.
- `Revoke`: `deviceId`, `expectedRevision`; yönetici gerekir.
- `Rename`: `deviceId`, `name`, `expectedRevision`; yönetici gerekir.
- `Settings`: `scanIntervalSeconds` 3–60, `retentionDays` 1–90; yönetici gerekir.
- `Reconcile`: yeni tarama/uzlaştırma; yönetici gerekir.
- `PreviewPolicy`: Defender XML önizlemesi; yönetici gerekir, Windows'a uygulanmaz.

Örnek snapshot payload:

```json
{"version":1,"operation":"Snapshot"}
```

Bu JSON tek başına raw pipe'a yazılmaz: önüne UTF-8 byte uzunluğunun dört byte prefix'i gelir. Uygulama kodunda `PipeProtocol.WriteAsync` / `ReadAsync` kullanılır. Bilinmeyen veya bozuk frame sınırlarından sonra bağlantı kapanır; yeni bağlantı kabul edilir.

## Yanıt ve işlem sonucu

Yanıt `success`, `correlationId`, isteğe göre `snapshot`, `command`, `preview` veya `error` içerir. Hata `code`, `message`, `correlationId` ile taşınır. Correlation ID log/audit ile ilişkilendirme içindir; yetki sağlamaz.

`command.desiredSaved=true`, yalnız istenen kayıt transaction'ının tamamlandığını söyler. `enforcementVerified=false`, fiziksel erişimin doğrulanmadığını belirtir. Snapshot'ta `desiredRevision` ve nullable `effectiveRevision` ayrıdır. Bu sürümde backend korumayı doğrulamaz ve etkin revizyon boş kalır.

`expectedRevision` eskiyse conflict oluşur; istemci güncel snapshot'ı okuyup kullanıcı kararını yenilemelidir. Yönetim komutları bağlantı hatası sonrasında otomatik tekrar edilmez: DB kaydı tamamlanmış, yanıt kaybolmuş olabilir. Sunucu istemcinin yanıtı okuyup bağlantıyı kapatmasını kısa süre bekler; timeout/cancellation işlem ömrünü sınırlar.

## Doğrulama sınırı

Gerçek Windows pipe/token, kısıtlı token'ın yönetim reddi ve sahte `admin` alanı otomatik test edilir. Üretim SCM PID eşleştirmesi ve LocalService altında keşif/DB yetkileri kurulu servis kabulünde ayrıca doğrulanmalıdır. Geliştirme smoke testinin başarılı olması üretim kurulum kabulünü tamamlamaz.
