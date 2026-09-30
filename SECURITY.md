# Güvenlik bildirimleri

PortSentinel 1.0.1, cihaz izin yönetimi ve keşif sürümüdür. **Fiziksel USB erişimini engellemez.** Yönetim kayıtlarının oluşması koruma sağlandığı anlamına gelmez. Uygulama içindeki “Koruma doğrulanmadı” durumu geçerli ürün sınırıdır.

Yetkilendirme atlatma, Named Pipe sunucu kimliği, dosya/dizin sahipliği, veri bütünlüğü veya kurulum/kaldırma sorunları için GitHub'ın bu depodaki **Security → Report a vulnerability** özel bildirim kanalını kullanın. Hassas örnekleri herkese açık issue veya PR içine koymayın.

Bildirimde etkilenen commit/sürümü, Windows sürümünü ve mimarisini, beklenen davranışı, yeniden üretme adımlarını ve etkisini belirtin. Kişisel dosyalar, gerçek USB serileri, SID'ler, lisans/erişim token'ları veya özel DB/log dosyalarını paylaşmayın. Süreç ve yanıt süreleri için garanti verilmemiştir.

Bilinen sınırlar: Windows 10 ve tüm edisyon/güncelleme kombinasyonları ayrı çalıştırma testinden geçmedi; gerçek kurulum/kaldırma ve donanım kabulü bekler. Seri/VID/PID kimliği taklit edilebilir. Polling kısa bağlantıları kaçırabilir. XML önizlemesi politika etkinleştirmez. Ayrıntılar [teknik karar](docs/ADR-001-erisim-denetimi.md), [mimari](docs/MIMARI.md) ve [donanım kabul protokolü](docs/DONANIM-KABUL.md) içindedir.
