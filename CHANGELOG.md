# Değişiklik günlüğü

## 1.0.1 — 30 Eylül 2026

- Windows 10 ve Windows 11 x64 kurulum hedefi genişletildi.
- Home/Pro/Enterprise/Education edisyonu, 24H2/25H2 güncelleme sürümü ve destek tarihi üzerinden uygulanan kurulum engelleri kaldırıldı.
- Hem Inno Setup ön kontrolü hem servis kurulum betiği güncellendi; tanılama ekranındaki hedef bilgisi aynı davranışa getirildi.
- .NET 10.0.12 x64 çalışma zamanını içeren Türkçe `PortSentinel-Setup-1.0.1.exe` üretildi.
- Paket sürümü tek `Directory.Build.props` kaynağından okunur; geliştirici **Yunus İNAN** olarak korunur.
- Windows 11 Pro 23H2 üzerindeki gerçek Windows PowerShell ön kontrolü ve self-contained servis/WPF smoke testi başarılı.
- 24 unit ve 18 integration testi başarılı; WPF beş ekranında binding hatası yok.

Fiziksel USB erişim engelleme motoru bu sürümde eklenmemiştir. Windows 10 üzerinde ayrı çalıştırma ve gerçek kurulum/kaldırma kabul testi henüz yapılmadı.

## 1.0.0 — 30 Eylül 2026

- Türkçe WPF/MVVM yönetim arayüzü, ayrı Windows Worker Service ve Named Pipe IPC.
- Fiziksel USB ata düğümüne dayalı disk/bölüm/volume keşfi.
- SQLite migration, kalıcı cihaz izin kayıtları, revizyon ve denetim günlüğü.
- Gerçek Windows token yetkilendirmesi; standart kullanıcı görüntüleme, yükseltilmiş yönetici işlemleri.
- Salt okunur OS/Defender/GPO göstergeleri ve uygulama yapmayan Defender XML önizlemesi.
- Framework-dependent ZIP build betiği, servis kurulum/kaldırma betikleri ve self-contained Inno Setup kurucusu.
- İlk kurucudaki dar Windows 11 Pro 24H2/25H2 kısıtı 1.0.1'de kaldırıldı.
