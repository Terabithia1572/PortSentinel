# Bağımlılık ve lisans notları

30 Eylül 2026'da .NET SDK 10.0.401 ve x64 runtime 10.0.12 mevcut makinede doğrulandı. NuGet restore/build/test başarılı; doğrudan sürümler Directory.Packages.props, transitif sürümler proje packages.lock.json dosyalarına kilitlendi. Kilitli restore tekrarlanabilir build betiğinde zorunludur. Microsoft .NET 10 LTS destek tarihi resmi [yaşam döngüsü](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) sayfasından incelendi.

- EF Core SQLite/Design 10.0.12 ve Microsoft Extensions 10.0.12: NuGet metadata'sı MIT.
- FluentValidation 12.1.1: NuGet metadata'sı Apache-2.0.
- Serilog Extensions Hosting 10.0.0, File 7.0.0, Compact 3.0.0, Console 6.1.1 ve transitif Serilog: Apache-2.0.
- xUnit 2.9.3: Apache-2.0; test SDK/runner geliştirme amaçlıdır.
- SQLite native bundle ve diğer transitif bağımlılıklar packages.lock.json'da kayıtlıdır; üçüncü taraf bildirimleri dağıtım paketinde tutulmalıdır.

Lisanslar indirilen paketlerin .nuspec metadata'sından okunmuştur. IPC DTO mapping'i açık C# koduyla yapılır; ticari lisans veya ek mapping kütüphanesi yoktur. System.IO.Pipes.AccessControl .NET 10 Windows referanslarında zaten vardır; eski ek NuGet paketi kullanılmaz. Üretim cihaz kontrolü MDE lisansı ayrı bir ürün gereksinimidir, bu açık kaynak bağımlılık lisansları yerine geçmez.

Build sırasında NuGet audit uyarısı görülmedi; bu, sonraki tarihlerde yeni güvenlik açığı çıkmayacağı anlamına gelmez. Runtime ve paket patch güncellemeleri kilit dosyaları yenilenip test edilerek uygulanmalıdır.
