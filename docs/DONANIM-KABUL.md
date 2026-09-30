# Ayrı donanım kabul protokolü

**Henüz çalıştırılmadı.** Otomatik unit/SQLite testleri fiziksel engelleme kanıtı değildir. Bu sürüm engelleme yapmadığından engellenmesi beklenen USB fixture okuması başarılı olacak ve kabul testi başarısız sayılacaktır.

İmzalı/lisanslı üretim motoru ve desteklenen ayrı laboratuvar makinesi hazırlanmalıdır. Secure Boot açık kalır; test-signing kullanılmaz. Kendi geliştirme makinenizde politikaları etkinleştirmeyin. Kayıp riski olmayan test ortamı, ayrı USB fixture'lar ve yetkili kurtarma erişimi kullanın.

## Test koşulları

- Lisans ve onboarding yönetim kaydıyla doğrulansın; yalnız Get-MpComputerStatus yeterli değil.
- Tam etkin XML/revizyon/hash ve engine accepted durumu kaydedilsin. USB dışındaki cihaz ailelerine default deny uygulanmasın.
- Önceden hazırlanmış fixture dosyaları, boyut ve SHA256 envanteri izinli durumda doğrulansın. FileNotFound erişim reddi sayılmasın.
- Her deny denemesi standart kullanıcı token'ında gerçek CreateFile/Read, yeni process, raw disk ve tüm volume yollarında ölçülsün. Önceden açık handle'lar ve süren yazmalar ayrıca değerlendirilsin.

## Zorunlu senaryolar

1. Aynı anda 10 fiziksel USB: 5 açık izin/5 ret; her cihazın bütün volume'ları ve aynı model farklı seri.
2. Hiç bağlanmamış cihaz, daha önce kurulmuş cihaz ve boot öncesinde takılı cihaz: ilk erişimden itibaren ret.
3. USBSTOR, UASP harici SSD, iki bölüm ve çok LUN; sürücü harfi olmayan volume ve ham disk erişimi.
4. Direkt port, farklı port, hub; tak/çıkar yarışları ve servis tarama boşlukları.
5. Seri yokluğu, aynı seri/VID/PID çakışması, farklı takılma sırası ve firmware spoof sınırları.
6. WPF kapalı, servis durdurulmuş, servis yeniden başlamış ve reboot: motorun kalıcılığı ayrı ölçülsün.
7. İzin iptali sırasında okuma handle'ı, devam eden yazma, mapped file ve güvenli çıkarma; veri kaybı etkisi kaydedilsin.
8. Domain GPO/MDM çatışması, engine refresh/update ve yarıda kalan apply; eski/yeni etkin revizyon kanıtı.
9. Standart kullanıcının IPC yönetim komutu, DB/pipe spoof, servisin DB erişim hatası.
10. Klavye/fare/hub/ağ adaptörü, dahili/sistem diski ve USB dışı diskler çalışmaya devam etsin.
11. Yetkili kurulum/kaldırma ve yalnız uygulama-owned politikaların hash kontrollü geri alınması.

Her senaryo için OS/engine sürümü, politika revizyonu, transport, fiziksel kimlik, token SID, zaman, beklenen/gerçek sonuç ve hata kodu kaydedilir. Bir senaryonun başarısı diğerini kanıtlamaz.

## Salt okunur fixture aracı

`scripts/Test-Hardware.ps1` yalnız açıkça seçildiğinde, gerçek envanterde fiziksel USB/volume ilişkisini doğrulayıp manifestteki mevcut dosyayı okur. Politika uygulamaz, fixture yazmaz, cihaz disable etmez. `-AcknowledgeHardwareTest` zorunludur. Manifest biçimi:

```json
[
  {
    "Name": "Laboratuvar USB — ret denemesi",
    "PhysicalInstanceId": "USB\\VID_1234&PID_ABCD\\LABSERIAL001",
    "FixtureFile": "E:\\PortSentinelFixture\\probe.bin",
    "ExpectedReadable": false
  }
]
```

```powershell
.\scripts\Test-Hardware.ps1 -Manifest .\lab-manifest.json -AcknowledgeHardwareTest
```

Bu tek dosya okuma yardımcı aracıdır; tüm boot/UASP/raw/handle/yazma garantisini tek başına kanıtlamaz. Test sonucu `artifacts/hardware-*` JSON dosyasına yazılır. Test manifestindeki cihaz ID/path örneklerini gerçek fixture bilgileriyle değiştirin.
