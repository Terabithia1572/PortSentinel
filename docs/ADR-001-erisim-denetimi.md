# ADR-001 — Erişim denetimi ve doğrulama sınırı

Karar tarihi: 30 Eylül 2026. Durum: yönetim/keşif uygulaması uygulanır; üretim engelleme backend'i bu teslimatta doğrulanamaz.

## İncelenen Microsoft kaynakları

- [Device Installation Restrictions](https://learn.microsoft.com/en-us/windows/client-management/client-tools/manage-device-installation-with-group-policy): kurulum/update denetimi, instance/hardware/class katmanları, mevcut kurulumlara retroactive uygulama ve PnP ebeveyn/çocuk ilişkileri.
- [DeviceInstallation CSP](https://learn.microsoft.com/en-us/windows/client-management/mdm/policy-csp-deviceinstallation): Pro desteği ve layered evaluation seçenekleri.
- [Defender Device Control](https://learn.microsoft.com/en-us/defender-endpoint/device-control-overview): depolama erişimi denetimi; UAS desteği platform 4.18.2105 ve sonrası; dosya maskeleri için 4.18.2207 ve sonrası.
- [Politika grupları ve kuralları](https://learn.microsoft.com/en-us/defender-endpoint/device-control-policies): MatchAll, seri/VID_PID/PrimaryId, birbirinden ayrılmış Allow/Deny kuralları ve disk/dosya erişim maskeleri.
- [Dağıtım](https://learn.microsoft.com/en-us/defender-endpoint/device-control-configure): MDE Plan 1/2 veya Defender for Business; Intune, Defender portalı veya GPO.
- [SSS](https://learn.microsoft.com/en-us/defender-endpoint/device-control-faq): tek makinede GPO ve Intune'u beraber yönetmeme; bazı ürünlerin ek erişim gereksinimleri.
- [.NET yaşam döngüsü](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core): .NET 10 LTS, destek sonu 14 Kasım 2028.
- [Windows cihaz yetenekleri](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/devpkey-device-capabilities) ve [UniqueID/USB seri ilişkisi](https://devblogs.microsoft.com/windows-music-dev/the-importance-of-including-a-unique-iserialnumber-in-your-usb-midi-devices/): keşifte UniqueID biti ile fiziksel USB instance seri bölümünün birlikte değerlendirilmesi.

## Karar ve gerekçe

Yerleşik kurulum kısıtlamaları fiziksel cihaz bazında yararlıdır; dosya I/O yetkilendirme motoru olarak kabul edilmez. Genel USB sınıfı, disk sınıfı, removable-device engeli veya USBSTOR kapatılması güvenli kapsam sağlamaz. Özellikle UASP SCSI disk düğümü, composite aygıtlar, önceden kurulmuş sürücüler ve açık tanıtıcılar birlikte değerlendirildiğinde bu proje için kesin erişim öncesi garanti kurulamadı. PnP olayından sonra devre dışı bırakma da bu açığı kapatmaz.

Üretim için aday Microsoft Defender for Endpoint Device Control'dür. Lisanslı, yönetilen ve donanım kabul testlerinden geçmiş ortam gerektirir. Registry/GPO okuma, Defender Enabled değeri veya başarılı XML üretimi, lisans/etkin politika/gerçek dosya engeli kanıtı değildir. PortSentinel mevcut makinede bu şartları doğrulayamaz. Bu nedenle çalışan backend gerçek ortam değerlendirmesi yapar ve uygulama talebini `Unverified` olarak kaydeder; hiçbir Windows politikasını değiştirmez. Boş/mock başarı backend'i yoktur. Arayüz her zaman korumanın doğrulanmadığını söyler.

XML önizlemesi bir üretim dağıtımı değildir. USB için PrimaryId + BusId eşleşmesi, tam seri ve VID_PID içeren ayrı izin grupları, kapsam içi default deny kuralı, çakışmayan exclusions ve 63 maskesi hazırlanır. UASP'ın SCSI görünümü yalnızca USB ata düğümüyle keşfedilir; XML'deki BusId=USB grubunun bu cihazı kapsaması ayrıca test edilmelidir. Önizleme bu nedenle otomatik dağıtılmaz. Yanlış/eksik kapsamla default deny etkinleştirilmez.

## Ayrı senaryolar

- **İlk bağlantı:** kurulum kısıtlaması sürücü kurulumunu engelleyebilir; bu uygulamanın envanter taraması erişim öncesi güvenlik değildir. Lisanslı I/O motoru ayrıca doğrulanmalıdır.
- **Önceden kurulu:** retroactive kurulum politikası vardır; mevcut dosya tanıtıcısı ve veri kaybı etkisi kabul testi gerektirir.
- **Açılıştan önce bağlı:** servis taraması yalnızca servis açılınca çalışır; boot boyunca koruma iddiası yoktur. Üretim motoru boot I/O testinden geçmelidir.
- **USBSTOR/UASP:** yalnız disk düğümleri taranır, USB fiziksel ata araştırılır. UASP görünümü ve filtre kapsamı laboratuvar kabul kapısıdır.
- **Çok bölüm/LUN:** disk→bölüm→mantıksal volume ilişkileri toplanır, aynı USB fiziksel düğüm altında birleştirilir. Tüm volume ve ham disk erişimleri ayrı test edilir. Mount-point-only volume'ların harfleri olmayabilir.
- **Port/hub:** Windows UniqueID yeteneği ve donanım seriyle fiziksel anahtar üretilebilir. Seri bulunamazsa port bağımlı/belirsiz; izin verilemez. Hub'ın işlevine müdahale edilmez.
- **GPO/MDM:** yönetilen ortama ait göstergeler ve ilgili politika anahtarları okunur. Yerel veri bunları ezmez. RSoP/MDM kaynağını eksiksiz kanıtlama bu teslimatın dışında; negatif gösterge yönetimsiz ortam kanıtı değildir.

## Garantiler ve başarısızlık

Garanti edilen uygulama davranışı: istenen politika ile doğrulanmış etkin politika ayrıdır; kimlik belirsizliğine geniş izin yoktur; tüm yetkiler sunucu Windows token'ından doğrulanır; hata başarıya çevrilmez. Garanti edilmeyen: fiziksel erişim engeli, reboot/servis duruşunda kalıcı koruma, firmware sahteciliğine dayanıklılık, açık handle iptali. Kimlikler kriptografik değildir; aynı kimliği taklit eden cihazlar ayırt edilemeyebilir.

Lisanslı altyapı kullanılamazsa alternatif imzalı Windows depolama/minifilter sürücüsü ve uzman sürücü doğrulamasıdır. Test-signing, imzasız sürücü, Secure Boot kapatma üretim çözümü değildir. Yerel yönetici, alternatif işletim sistemi veya fiziksel müdahaleye mutlak koruma iddiası yoktur.

Bu sınırlama ürünün asıl engelleme amacının henüz tamamlanmadığı anlamına gelir; yalnız derleme veya unit test sonucu bu kararı değiştiremez.
