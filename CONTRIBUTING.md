# Geliştirme ve katkı

Hata veya iyileştirme önerisi için [Issues](https://github.com/Terabithia1572/PortSentinel/issues) bölümünü kullanın. Kod değişikliği göndermeden önce proje telif bilgilerini [NOTICE.md](NOTICE.md) dosyasından okuyun.

## Yerel ortam

1. Windows 10/11 x64 ve `global.json` içindeki .NET SDK 10.0.401 ile depoyu klonlayın.
2. `dotnet restore PortSentinel.sln --locked-mode` ve `dotnet build PortSentinel.sln -c Release --no-restore` çalıştırın.
3. `dotnet test PortSentinel.sln -c Release --no-build --no-restore` ile testleri doğrulayın.
4. Servis ve WPF arayüzünü iki terminalde `scripts/Run-Development.ps1` ile açın. Geliştirme modunu üretim Windows servis kaydıyla karıştırmayın.

Kaynak değişikliklerinde Domain'in EF/WPF/Windows bağımlılığı almamasını, arayüzün yalnız IPC üzerinden çalışmasını ve yönetici kimliğinin gerçek Windows token'ından belirlenmesini koruyun. Paket sürümleri `Directory.Packages.props`, çözülen bağımlılıklar `packages.lock.json` dosyalarındadır; sürüm güncellemesinde ikisini birlikte inceleyin.

## Doğrulama

İşlem yetkilendirmesi, transaction tutarlılığı veya IPC davranışı değişirse ilgili unit/integration testlerini çalıştırın. Windows/GPO/MDM politika değişikliği ekleyen bir backend; lisans/önkoşul, sahiplik kaydı, readback, çatışma ve geri alma davranışlarıyla birlikte ayrı tasarım ve donanım kabulü gerektirir. Salt okunur keşfin başarıyla çalışması, fiziksel engellemenin doğrulanması anlamına gelmez.

PR açıklamasında kullanıcıya etkisini, hangi kontrollerin çalıştırıldığını ve test edilmemiş kapsamı yazın. Gerçek cihaz kimlikleri, kullanıcı SID'leri, DB, günlükler, sertifikalar veya erişim token'ları göndermeyin. Donanım testini CI'ye kendiliğinden dahil etmeyin.

## Kurucu

Inno Setup 6 ile `scripts/Build-Setup.ps1` çalıştırın. Betik uygulamayı kurmaz; `artifacts/installer` altında EXE ve SHA256 üretir. Windows servisi kurulumu/kaldırması ayrı test makinesinde kabul edilmelidir. `artifacts`, `bin`, `obj` ve yerel SQLite dosyaları Git'e dahil edilmez.
