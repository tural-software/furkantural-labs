using System.Security.Cryptography;
using FurkanTural_Labs_Application.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;

// Her senaryoda aynı 5 yük korunuyor ve ardından "süreç yeniden başlıyor": aynı
// yapılandırmayla yeni bir sağlayıcı kuruluyor. Yük hiç değişmiyor, korunma anı hiç
// değişmiyor. Değişen tek şey anahtar halkasının nerede durduğu ve kime ait olduğu.
//
// Ölçülen şey çözülebilen yük sayısı. Oturum çerezi de sahtecilik jetonu da bu yoldan
// geçtiği için sayı doğrudan şunu söylüyor: geri dönüşümden sonra kaç kullanıcı içeride.
//
// Beklentiler ölçümden değil Data Protection'ın sözleşmesinden geliyor:
//   · kalıcı yer bulunamazsa anahtarlar süreçle birlikte gider               → 0
//   · klasöre yazılan halka yeni süreçte okunur                              → 5
//   · uygulama adı amaç zincirinin kökü; değişirse koruma alanı da değişir    → 0
//   · amaç dizgesi de o zincirde; aynı halka yetmez                           → 0
//   · anahtar dönüşü eski anahtarı silmez, yalnız yenisini öne alır           → 5
//   · iptal silme değildir ama yeni süreçte okumayı da bitirir               → 0
//   · klasör yayında silinirse halka yeniden üretilir                         → 0
//   · bilinmeyen anahtar kimliği önbellekteki halkayı tazeletir               → 5
//   · bilinen ama iptal edilmiş anahtar tazeletmez; çalışan işçi görmez       → 5
//   · at-rest şifreleme okuma tarafında yapılandırma istemez                  → 5

const int YukSayisi = 5;
const string Amac = "FurkanTural.Admin.Oturum.v1";
const string UygulamaAdi = "FurkanTural.Admin";

var kok = Directory.CreateDirectory(
    Path.Combine(Path.GetTempPath(), "ft-lab31-" + Guid.NewGuid().ToString("N"))).FullName;

var report = new LabReport(
    title: "Lab_31 — Data Protection: aynı 5 yük, anahtar halkası değişince kaçı çözülüyor",
    claim: $"Her senaryoda {YukSayisi} yük korunuyor ve süreç yeniden başlıyor. Değişen tek şey " +
           "anahtar halkasının nerede durduğu ve kime ait olduğu; ölçülen şey çözülen yük sayısı.",
    metric: "Çözülen");

// ── 1–2. Anahtarların nerede durduğu ────────────────────────────────────────
await report.MeasureAsync(
    "1) Bellekteki halka, süreç yeniden başladı",
    Expectation.Exactly(0),
    BellekteAsync,
    note: "Kalıcı bir yer bulunamadığında Data Protection anahtarları süreç dışına hiç " +
          "yazmaz. Kod değişmedi, çerez süresi dolmadı, veri tabanı yerinde — ama halka " +
          "gittiği için beş yükün beşi de okunamıyor. App pool geri dönüşümü bunu yapar.");

await report.MeasureAsync(
    "2) Kalıcı halka, süreç yeniden başladı",
    Expectation.Exactly(YukSayisi),
    KaliciAsync,
    note: "Tek fark: halka diske yazıldı. Aynı yeniden başlatma, aynı yükler, beşi de " +
          "çözülüyor. Çözüm bir çerez ayarı değil, anahtarın adresi.");

// ── 3–4. Halkayı paylaşmak yetmiyor: amaç zinciri de örtüşmeli ──────────────
await report.MeasureAsync(
    "3) Kalıcı halka, uygulama adı değişti",
    Expectation.Exactly(0),
    UygulamaAdiDegistiAsync,
    note: "Dosyalar aynı klasörde, anahtar yerinde, yine de hiçbir yük açılmıyor: " +
          "uygulama adı amaç zincirinin kökü. Bu yüzden ad sabit bir dizge olmalı; içerik " +
          "kökü yolundan türetilirse site başka bir klasöre taşındığı gün halka ölür.");

await report.MeasureAsync(
    "4) Kalıcı halka, amaç dizgesi değişti",
    Expectation.Exactly(0),
    AmacDegistiAsync,
    note: "Aynı halka, aynı uygulama adı, tek fark CreateProtector'a verilen dizge. " +
          "Sonuç 3 ile aynı: koruma alanı anahtarla değil amaç zinciriyle tanımlanıyor. " +
          "Çerez şemasını yeniden adlandırmak da aynı sonucu verir.");

// ── 5–6. Dönüş ile iptal aynı şey değil ─────────────────────────────────────
await report.MeasureAsync(
    "5) Yeni anahtar üretildi, eskisi duruyor",
    Expectation.Exactly(YukSayisi),
    AnahtarDonduAsync,
    note: "Halkaya yeni ve aktif bir anahtar eklendi; yeni yükler onunla korunacak. Eski " +
          "beş yük hâlâ çözülüyor, çünkü eski anahtar duruyor. Süresi dolan anahtar " +
          "okumaya devam eder; dolması tek başına kimseyi çıkışa atmaz.");

await report.MeasureAsync(
    "6) Anahtar iptal edildi, süreç yeniden başladı",
    Expectation.Exactly(0),
    AnahtarIptalAsync,
    note: "Anahtar dosyası silinmedi, yalnızca iptal işaretlendi. Yine de beş yükün hiçbiri " +
          "açılmıyor: iptal bilinçli olarak okumayı da bitirir. Oturumları toptan " +
          "düşürmenin yolu budur ve yanlışlıkla yapılırsa geri dönüşü yoktur.");

// ── 7–8. Yayın ve geri dönüşümün iki ayrı tuzağı ────────────────────────────
await report.MeasureAsync(
    "7) Anahtar klasörü yayında silindi",
    Expectation.Exactly(0),
    KlasorSilindiAsync,
    note: "Klasör doğru yapılandırılmıştı ama yayın onu sildi. Yeni süreç klasörü yeniden " +
          "kurup sıfırdan anahtar üretiyor; yapılandırma kusursuz görünürken sonuç 1. " +
          "senaryoyla aynı. Anahtar klasörü yayın paketinin dışında kalmalı.");

await report.MeasureAsync(
    "8) Eski işçi, yeni işçinin anahtarıyla korunan yükte",
    Expectation.Exactly(YukSayisi),
    OnbellekteKalanHalkaAsync,
    note: "Halka her istekte diskten okunmuyor, önbelleğe alınıyor — ama bilinmeyen bir " +
          "anahtar kimliği onu tazeletiyor. Eski işçi, kendisi başladıktan sonra üretilmiş " +
          "bir anahtarla korunan beş yükü de açıyor. Örtüşen geri dönüşümde oturum kaybının " +
          "sebebi önbellek değil; koşul yalnızca iki işçinin aynı depoyu okuması.");

await report.MeasureAsync(
    "9) Anahtar iptal edildi, eski işçi çalışmaya devam ediyor",
    Expectation.Exactly(YukSayisi),
    IptalCalisanIsciAsync,
    note: "6 ile aynı iptal, tek fark okuyanın halkasını iptalden önce yüklemiş olması. " +
          "Beşi de hâlâ açılıyor: bilinmeyen anahtar halkayı tazeletir, bilinen ama iptal " +
          "edilmiş anahtar tazeletmez. Yani iptal çalışan süreçlerde anında geçerli değil; " +
          "oturumu gerçekten kapatmak isteyen uygulama bunu kendi durumunda tutmalı.");

// ── 10. At-rest şifreleme: bedeli okuma tarafında değil ─────────────────────
await report.MeasureAsync(
    "10) Halka DPAPI ile şifreli, okuyanda ayar yok",
    Expectation.Exactly(YukSayisi),
    DpapiAsync,
    note: "Halka diske şifreli yazıldı, okuyan taraf için hiçbir şey yapılandırılmadı ve " +
          "beşi de çözüldü: çözücü XML'in içinden seçiliyor. Bedeli aynı makinede görünmez; " +
          "makine kapsamlı şifrelenmiş halka başka bir makineye taşınırsa okunamaz.");

var cikis = report.Print();

try { Directory.Delete(kok, recursive: true); } catch (IOException) { }

return cikis;

// Ölçüm düzeneği: her senaryo kendi klasörünü alır, böylece senaryolar birbirinin halkasını
// görmez. klasor null ise kalıcılık hiç kurulmaz ve halka süreçte kalır.
ServiceProvider Halka(string? klasor, string ad = UygulamaAdi, bool dpapi = false)
{
    var services = new ServiceCollection();
    var builder = services.AddDataProtection().SetApplicationName(ad);

    if (klasor is null)
        builder.UseEphemeralDataProtectionProvider();
    else
        builder.PersistKeysToFileSystem(new DirectoryInfo(klasor));

    if (dpapi && OperatingSystem.IsWindows())
        builder.ProtectKeysWithDpapi(protectToLocalMachine: true);

    return services.BuildServiceProvider();
}

string Klasor(string ad) => Directory.CreateDirectory(Path.Combine(kok, ad)).FullName;

static string Yuk(int sira) => $"oturum-{sira}";

string[] Koru(IServiceProvider saglayici, string amac = Amac)
{
    var koruyucu = saglayici.GetRequiredService<IDataProtectionProvider>().CreateProtector(amac);
    return [.. Enumerable.Range(1, YukSayisi).Select(i => koruyucu.Protect(Yuk(i)))];
}

// Çözülen sayılırken yalnız istisnanın yokluğu değil, metnin birebir geri gelmesi aranıyor:
// yanlış anahtarla "başarılı" bir çözme olmadığını da bu karşılaştırma garanti ediyor.
int Coz(IServiceProvider saglayici, string[] yukler, string amac = Amac)
{
    var koruyucu = saglayici.GetRequiredService<IDataProtectionProvider>().CreateProtector(amac);
    var cozulen = 0;

    for (var i = 0; i < yukler.Length; i++)
    {
        try
        {
            if (koruyucu.Unprotect(yukler[i]) == Yuk(i + 1))
                cozulen++;
        }
        catch (CryptographicException)
        {
        }
    }

    return cozulen;
}

Task<int> BellekteAsync()
{
    using var once = Halka(klasor: null);
    var yukler = Koru(once);

    using var sonra = Halka(klasor: null);
    return Task.FromResult(Coz(sonra, yukler));
}

Task<int> KaliciAsync()
{
    var klasor = Klasor("kalici");

    using var once = Halka(klasor);
    var yukler = Koru(once);

    using var sonra = Halka(klasor);
    return Task.FromResult(Coz(sonra, yukler));
}

Task<int> UygulamaAdiDegistiAsync()
{
    var klasor = Klasor("ad-degisti");

    using var once = Halka(klasor, ad: UygulamaAdi);
    var yukler = Koru(once);

    using var sonra = Halka(klasor, ad: UygulamaAdi + ".Panel");
    return Task.FromResult(Coz(sonra, yukler));
}

Task<int> AmacDegistiAsync()
{
    var klasor = Klasor("amac-degisti");

    using var once = Halka(klasor);
    var yukler = Koru(once);

    using var sonra = Halka(klasor);
    return Task.FromResult(Coz(sonra, yukler, amac: "FurkanTural.Admin.Oturum.v2"));
}

// Yeni anahtarın etkinleşme tarihi mevcut anahtardan sonra olduğu için varsayılan anahtar
// odur; eski anahtar halkada kalır ve eski yükler onunla çözülmeye devam eder.
Task<int> AnahtarDonduAsync()
{
    var klasor = Klasor("anahtar-dondu");

    using var once = Halka(klasor);
    var yukler = Koru(once);

    using var donus = Halka(klasor);
    var simdi = DateTimeOffset.UtcNow;
    donus.GetRequiredService<IKeyManager>().CreateNewKey(simdi, simdi.AddDays(90));

    using var sonra = Halka(klasor);
    return Task.FromResult(Coz(sonra, yukler));
}

Task<int> AnahtarIptalAsync()
{
    var klasor = Klasor("anahtar-iptal");

    using var once = Halka(klasor);
    var yukler = Koru(once);

    using var iptal = Halka(klasor);
    iptal.GetRequiredService<IKeyManager>()
        .RevokeAllKeys(DateTimeOffset.UtcNow.AddMinutes(1), "laboratuvar senaryosu");

    using var sonra = Halka(klasor);
    return Task.FromResult(Coz(sonra, yukler));
}

Task<int> KlasorSilindiAsync()
{
    var klasor = Klasor("klasor-silindi");

    using var once = Halka(klasor);
    var yukler = Koru(once);

    Directory.Delete(klasor, recursive: true);

    using var sonra = Halka(Klasor("klasor-silindi"));
    return Task.FromResult(Coz(sonra, yukler));
}

// Eski işçi önce kendi halkasını yüklüyor (ilk koruma bunu yaptırır). Yeni anahtar ondan
// SONRA üretildiği için eski işçinin önbelleğinde yok; yeni işçinin ürettiği yükler de o
// anahtarla korunuyor. Yeni anahtarın gerçekten kullanıldığı ayrıca doğrulandı: yükün
// başlığındaki anahtar kimliği iki işçide farklı çıkıyor (ayrıntı README'de).
Task<int> OnbellekteKalanHalkaAsync()
{
    var klasor = Klasor("onbellek");

    using var eski = Halka(klasor);
    _ = Koru(eski);

    using var yonetim = Halka(klasor);
    var simdi = DateTimeOffset.UtcNow;
    yonetim.GetRequiredService<IKeyManager>().CreateNewKey(simdi, simdi.AddDays(90));

    using var yeni = Halka(klasor);
    var yukler = Koru(yeni);

    return Task.FromResult(Coz(eski, yukler));
}

// 6'nın aynısı, tek fark okuyan taraf: halkasını iptalden ÖNCE yüklemiş ve hâlâ ayakta olan
// işçi. Yükler de o işçinin kendi ürettikleri, yani anahtar kimliği önbellekte zaten var.
Task<int> IptalCalisanIsciAsync()
{
    var klasor = Klasor("iptal-calisan-isci");

    using var eski = Halka(klasor);
    var yukler = Koru(eski);

    using var iptal = Halka(klasor);
    iptal.GetRequiredService<IKeyManager>()
        .RevokeAllKeys(DateTimeOffset.UtcNow.AddMinutes(1), "laboratuvar senaryosu");

    return Task.FromResult(Coz(eski, yukler));
}

Task<int> DpapiAsync()
{
    var klasor = Klasor("dpapi");

    using var once = Halka(klasor, dpapi: true);
    var yukler = Koru(once);

    using var sonra = Halka(klasor);
    return Task.FromResult(Coz(sonra, yukler));
}
