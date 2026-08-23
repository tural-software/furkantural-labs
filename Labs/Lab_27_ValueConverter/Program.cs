using System.Reflection;
using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Persistence.Contexts;
using FurkanTural_Labs_Persistence.Registration;
using FurkanTural_Labs_Persistence.Sandbox;
using Lab_27_ValueConverter;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// Aynı 30 kaydın durumu iki sütunda birden duruyor: DurumSayi converter'sız (int),
// DurumMetin HasConversion<string>() ile (nvarchar). Her senaryo aynı soruyu iki sütundan
// birine soruyor; ölçülen şey sorgunun döndürdüğü satır sayısı.
//
// Beklentiler ölçümden değil SQL'in nerede karar verdiğinden geliyor: karşılaştırma ve
// sıralama sunucuda sütun tipine göre yapılır, C#'taki enum sırasına göre değil.
//   · eşitlik iki tipte de aynı kümeyi bulur                      → 10
//   · aralık int'te sayısal sıraya bakar (Yayinda 1, Arsiv 2)      → 20
//   · aralık nvarchar'da alfabetik sıraya bakar (Arsiv < Yayinda)  → 10
//   · sıralamanın başı int'te Taslak(0), metinde Arsiv             → 10 ve 0
//   · küme üyeliği sıraya hiç bakmaz, iki tipte de doğru           → 20

const int GrupBoyu = 10;

var assembly = Assembly.GetExecutingAssembly();
await using var provider = await LabsHost.StartAsync(assembly);

var connectionString = LabsHost.BuildConfiguration(assembly)
    .GetConnectionString(PersistenceServiceRegistration.ConnectionName)!;

var secenekler = new DbContextOptionsBuilder<LabsDbContext>()
    .UseSqlServer(connectionString)
    .Options;

// "Yayınlanmış ve sonrası": iş kuralı olarak Yayinda ile Arsiv, yani 20 kayıt.
// Aşağıdaki senaryolar hep bu kümeyi arıyor, her seferinde başka bir yazımla.
Durum[] yayindanSonrakiler = [Durum.Yayinda, Durum.Arsiv];

var report = new LabReport(
    title: "Lab_27 — Enum'u metin saklayınca aynı sorgu kaç satır döndürüyor",
    claim: $"Aynı {GrupBoyu * 3} kaydın durumu iki sütunda birden duruyor: biri int, diğeri " +
           "converter ile nvarchar. Ölçülen şey sorgunun döndürdüğü satır sayısı.",
    metric: "Satır");

// ── 1–2. Eşitlik: converter'ın güvenli tarafı ────────────────────────────────
await report.MeasureAsync(
    "1) int sütun, eşitlik (== Yayinda)",
    Expectation.Exactly(GrupBoyu),
    () => OlcAsync(q => q.Where(k => k.DurumSayi == Durum.Yayinda)),
    note: "Converter yok, sütun int, sorgu WHERE DurumSayi = 1'e dönüşüyor. Ölçünün sıfır noktası.");

await report.MeasureAsync(
    "2) metin sütun, eşitlik (== Yayinda)",
    Expectation.Exactly(GrupBoyu),
    () => OlcAsync(q => q.Where(k => k.DurumMetin == Durum.Yayinda)),
    note: "Converter değeri N'Yayinda'ya çevirip karşılaştırıyor. Eşitlik iki tipte de aynı " +
          "kümeyi bulur; converter'ın sorunsuz göründüğü yer burası.");

// ── 3–4. Aralık: karşılaştırmayı C# değil sütun tipi belirliyor ──────────────
await report.MeasureAsync(
    "3) int sütun, aralık (>= Yayinda)",
    Expectation.Exactly(GrupBoyu * 2),
    () => OlcAsync(q => q.Where(k => k.DurumSayi >= Durum.Yayinda)),
    note: "Sayısal sıra: Yayinda 1, Arsiv 2. Beklenen iş kuralı sonucu — 20 kayıt.");

await report.MeasureAsync(
    "4) metin sütun, aralık (>= Yayinda)",
    Expectation.Exactly(GrupBoyu),
    () => OlcAsync(q => q.Where(k => k.DurumMetin >= Durum.Yayinda)),
    note: "Aynı C# ifadesi, yarısı kadar sonuç. nvarchar sütunda karşılaştırma alfabetik: " +
          "'Arsiv' < 'Yayinda' olduğu için arşiv kayıtları kümenin dışında kalıyor. Hata yok, uyarı yok.");

// ── 5–6. Sıralama: aynı kayma ORDER BY'da da var ─────────────────────────────
await report.MeasureAsync(
    "5) int sütun, sıralamanın ilk 10'unda Taslak",
    Expectation.Exactly(GrupBoyu),
    () => OlcAsync(q => q.OrderBy(k => k.DurumSayi).Take(GrupBoyu).Where(k => k.DurumSayi == Durum.Taslak)),
    note: "Sayısal sıranın başı Taslak(0); ilk 10 kaydın tamamı taslak.");

await report.MeasureAsync(
    "6) metin sütun, sıralamanın ilk 10'unda Taslak",
    Expectation.Exactly(0),
    () => OlcAsync(q => q.OrderBy(k => k.DurumMetin).Take(GrupBoyu).Where(k => k.DurumSayi == Durum.Taslak)),
    note: "Alfabetik sıranın başı 'Arsiv'; ilk 10 kayıtta tek bir taslak yok. Sayma yine int " +
          "sütundan yapılıyor, yani ölçülen şey sıralamanın kendisi.");

// ── 7–8. Küme üyeliği: sıraya hiç bakmayan yazım ─────────────────────────────
await report.MeasureAsync(
    "7) metin sütun, Contains ile küme",
    Expectation.Exactly(GrupBoyu * 2),
    () => OlcAsync(q => q.Where(k => yayindanSonrakiler.Contains(k.DurumMetin))),
    note: "4. senaryonun aradığı küme, bu kez IN listesi olarak. Üyelik sıraya " +
          "bakmadığı için converter'ın alfabetik sırası devreye girmiyor.");

await report.MeasureAsync(
    "8) int sütun, Contains ile küme",
    Expectation.Exactly(GrupBoyu * 2),
    () => OlcAsync(q => q.Where(k => yayindanSonrakiler.Contains(k.DurumSayi))),
    note: "Aynı yazım int sütunda da aynı sayıyı veriyor: saklama tipi değişse bile sonuç " +
          "değişmiyor. Aralık sorgusunun aksine bu ifade saklamadan bağımsız.");

// ── 9–10. Saklanan değerin kendisi tanınmazsa ────────────────────────────────
// Veritabanında enum'da karşılığı olmayan bir değer kalabilir: bir üye yeniden adlandırılır,
// veri başka bir sürümden gelir, biri elle düzeltir. İki saklama biçimi bu duruma aynı
// tepkiyi vermiyor. Ölçülen şey okumanın kaç satır getirebildiği; 30 satırın yalnız 10'u bozuk.
await report.MeasureAsync(
    "9) metin sütun, tanınmayan değer",
    Expectation.Exactly(0),
    () => OkunanSatirAsync(DurumContext.MetniBozSql),
    note: "Sütunda N'Arsivlendi' yazıyor, enum'da böyle bir üye yok. EF çeviremediği değeri " +
          "varsayılana düşürmüyor, okuma anında InvalidOperationException fırlatıyor. Bozuk 10 " +
          "satır yüzünden sağlam 20 satır da elde edilemiyor: sorgunun tamamı düşüyor.");

await report.MeasureAsync(
    "10) int sütun, tanınmayan değer",
    Expectation.Exactly(GrupBoyu * 3),
    () => OkunanSatirAsync(DurumContext.SayiyiBozSql),
    note: "Aynı bozulma sayısal sütunda hiç fark edilmiyor: enum'un temel tipi int olduğu ve " +
          "C# tanımsız değerleri taşımaya izin verdiği için 99 sorunsuz okunuyor. 30 satırın " +
          "hepsi geliyor — ama 10'u artık hiçbir üyeye karşılık gelmeyen bir değer taşıyor.");

return report.Print();

// Bir sorguyu kendi transaction'ında çalıştırır ve sonunda geri alır. Ölçüm tablosu da
// fikstür de bu transaction'ın içinde doğar ve orada kalır; tohum verisine dokunulmaz.
async Task<int> OlcAsync(Func<IQueryable<Kayit>, IQueryable<Kayit>> sorgu)
{
    await using var db = new DurumContext(secenekler);

    return await DataSandbox.RollbackAsync(db, async ctx =>
    {
        await KurAsync(ctx);
        return await sorgu(ctx.Set<Kayit>().AsNoTracking()).CountAsync();
    });
}

// Fikstürü kurar, verilen SQL ile bozar ve tabloyu baştan sona okumayı dener. Ölçüm elde
// edilen satır sayısı: materyalizasyon düşerse hiçbir satır elde edilemez, yani 0.
async Task<int> OkunanSatirAsync(string bozSql)
{
    await using var db = new DurumContext(secenekler);

    return await DataSandbox.RollbackAsync(db, async ctx =>
    {
        await KurAsync(ctx);
        await ctx.Database.ExecuteSqlRawAsync(bozSql);

        try
        {
            return (await ctx.Set<Kayit>().AsNoTracking().ToListAsync()).Count;
        }
        catch (InvalidOperationException)
        {
            // Hata sunucudan değil çeviriden geliyor: SQL çalıştı, satırlar okundu, EF
            // değeri enum'a dönüştüremedi. Kısmi sonuç diye bir şey yok.
            return 0;
        }
    });
}

// Her durumdan 10 kayıt, sırayla serpiştirilmiş: Id sırası durum sırasıyla aynı olmasın ki
// ORDER BY ölçümü Id'nin tesadüfünden değil sütunun kendisinden gelsin.
async Task KurAsync(LabsDbContext ctx)
{
    await ctx.Database.ExecuteSqlRawAsync(DurumContext.OlusturSql);

    var kayitlar = new List<Kayit>(GrupBoyu * 3);
    for (var i = 1; i <= GrupBoyu; i++)
    {
        foreach (var durum in Enum.GetValues<Durum>())
        {
            kayitlar.Add(new Kayit
            {
                Baslik = $"Lab_27 kayıt {durum} #{i:D2}",
                DurumSayi = durum,
                DurumMetin = durum
            });
        }
    }

    ctx.Set<Kayit>().AddRange(kayitlar);
    await ctx.SaveChangesAsync();
    ctx.ChangeTracker.Clear();
}
