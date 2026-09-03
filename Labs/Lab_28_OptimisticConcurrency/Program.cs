using System.Reflection;
using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Persistence.Contexts;
using FurkanTural_Labs_Persistence.Registration;
using FurkanTural_Labs_Persistence.Sandbox;
using Lab_28_OptimisticConcurrency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;

// On yazar aynı satırı okuyor, her biri sayacı bir artırıp kaydediyor. Okumalar hep
// yazmalardan önce bitiyor; yani her yazar 0 görüyor. Web'de iki isteğin aynı kaydı
// düzenlemesi tam olarak bu sıralamadır: her istek kendi context'iyle okur, sonra yazar.
//
// Beklentiler ölçümden değil UPDATE'in WHERE kısmından geliyor:
//   · WHERE Id = @p                                → her yazar 0+1 yazar, sayaç 1
//   · WHERE ... AND Surum = @okunan, vazgeç        → ilk yazar geçer, gerisi düşer, 1
//   · aynı koşul, "istemci kazanır"                → elindeki 1'i yazar, 1
//   · aynı koşul, yeniden oku ve artır             → her çakışma tazelenir, 10
//   · WHERE ... AND Deger = @okunan, yeniden dene  → aynı yakalama, sütun eklemeden, 10
//   · WHERE ... AND Ad = @okunan, yeniden dene     → Ad değişmiyor, koşul hep tutar, 1
//   · SET Deger = Deger + 1                        → okuma yok, sunucuda artar, 10

const int YazarSayisi = 10;

var assembly = Assembly.GetExecutingAssembly();
await using var provider = await LabsHost.StartAsync(assembly);

var connectionString = LabsHost.BuildConfiguration(assembly)
    .GetConnectionString(PersistenceServiceRegistration.ConnectionName)!;

var secenekler = new DbContextOptionsBuilder<LabsDbContext>()
    .UseSqlServer(connectionString)
    .Options;

var report = new LabReport(
    title: "Lab_28 — On yazar aynı sayacı birer artırınca sayaç kaç oluyor",
    claim: $"{YazarSayisi} yazar aynı satırı okuyor, her biri sayacı bir artırıp kaydediyor. " +
           "Ölçülen şey işin sonunda veritabanındaki sayaç değeri.",
    metric: "Sayaç");

// ── 1. Token yok: EF satırı yalnız kimliğinden tanıyor ───────────────────────
await report.MeasureAsync(
    "1) Token yok, son yazan kazanır",
    Expectation.Exactly(1),
    () => KosAsync(o => new SerbestContext(o), DogrudanKaydetAsync),
    note: "UPDATE ... WHERE Id = @p. Her yazar 0 okudu, 1 yazdı; hiçbiri hata almadı. " +
          "Kayıp ne log'a düşüyor ne istisnaya: sayaç sessizce 1'de kalıyor.");

// ── 2–4. rowversion: aynı yakalama, üç farklı tepki ─────────────────────────
await report.MeasureAsync(
    "2) rowversion, çakışmada vazgeç",
    Expectation.Exactly(1),
    () => KosAsync(o => new SurumContext(o), CakismadaVazgecAsync),
    note: "WHERE Id = @p AND Surum = @okunan. İlk yazar geçiyor, satırın sürümü ilerliyor, " +
          "kalan yazarların koşulu tutmuyor: sıfır satır, DbUpdateConcurrencyException. " +
          "Kayıp aynı, ama artık görünür.");

await report.MeasureAsync(
    "3) rowversion, çakışmada istemci kazanır",
    Expectation.Exactly(1),
    () => KosAsync(o => new SurumContext(o), IstemciKazanirAsync),
    note: "Belgelerdeki 'client wins' kalıbı: veritabanı değerlerini orijinal olarak al, " +
          "elindekini yaz. Elindeki 0+1, yani 1. Çakışma çözüldü, artış yine kayıp.");

await report.MeasureAsync(
    "4) rowversion, çakışmada yeniden oku ve artır",
    Expectation.Exactly(YazarSayisi),
    () => KosAsync(o => new SurumContext(o), YenidenOkuyupArtirAsync),
    note: "Reload satırı tazeliyor, artış taze değerin üstüne biniyor, kayıt tekrar deneniyor. " +
          "Çözümün adı 'çakışmayı çöz' değil 'işi yeniden yap'.");

// ── 5–6. Token hangi sütunda: yakalanan şey o sütunun değişimi ──────────────
await report.MeasureAsync(
    "5) Token Deger sütunu, yeniden oku ve artır",
    Expectation.Exactly(YazarSayisi),
    () => KosAsync(o => new DegerTokenContext(o), YenidenOkuyupArtirAsync),
    note: "Yeni sütun yok. WHERE Id = @p AND Deger = @okunan aynı çakışmayı yakalıyor " +
          "çünkü değişen sütun tam olarak o.");

await report.MeasureAsync(
    "6) Token Ad sütunu, yeniden oku ve artır",
    Expectation.Exactly(1),
    () => KosAsync(o => new AdTokenContext(o), YenidenOkuyupArtirAsync),
    note: "Aynı yeniden deneme kodu, aynı istisna yakalayıcı; hiç tetiklenmiyor. Ad'ı kimse " +
          "değiştirmediği için WHERE ... AND Ad = @okunan her seferinde tutuyor. Token var, koruma yok.");

// ── 7. Okuma-değiştir-yaz döngüsünü hiç kurmamak ─────────────────────────────
await report.MeasureAsync(
    "7) ExecuteUpdate, artış sunucuda",
    Expectation.Exactly(YazarSayisi),
    () => KosAsync(o => new SerbestContext(o), SunucudaArtirAsync),
    note: "SET Deger = Deger + 1. Eski değer istemciye hiç gelmediği için bayatlayacak bir " +
          "şey de yok; token da yeniden deneme de gerekmiyor. Sayaç için doğru araç bu.");

return report.Print();

// Bir senaryoyu kendi transaction'ında çalıştırır ve sonunda geri alır. Tablo, on yazarın
// context'leri ve ölçüm hep bu transaction'ın içinde; tohum verisine dokunulmuyor.
// Yazarlar aynı bağlantıyı paylaşıyor: okuma-değiştir-yaz kaybı bağlantı sayısından değil,
// okumanın yazmadan önce bayatlamasından doğar. Sıralama aynı olduğu sürece sonuç aynı.
async Task<int> KosAsync(
    Func<DbContextOptions<LabsDbContext>, SayacContext> fabrika,
    Func<SayacContext, Sayac, Task> kaydet)
{
    await using var kap = new LabsDbContext(secenekler);

    return await DataSandbox.RollbackAsync(kap, async ctx =>
    {
        await ctx.Database.ExecuteSqlRawAsync(SayacContext.OlusturSql);

        var paylasilan = new DbContextOptionsBuilder<LabsDbContext>()
            .UseSqlServer(ctx.Database.GetDbConnection())
            .Options;
        var islem = ctx.Database.CurrentTransaction!.GetDbTransaction();

        var yazarlar = new List<(SayacContext Baglam, Sayac Sayac)>(YazarSayisi);
        try
        {
            // Önce herkes okuyor. Hiçbir yazma başlamadan on kopya da 0'ı görmüş oluyor.
            for (var i = 0; i < YazarSayisi; i++)
            {
                var baglam = fabrika(paylasilan);
                await baglam.Database.UseTransactionAsync(islem);

                var sayac = await baglam.Set<Sayac>().SingleAsync(s => s.Id == SayacContext.SayacId);
                yazarlar.Add((baglam, sayac));
            }

            // Sonra herkes sırayla yazıyor. Elindeki kopya artık bayat.
            foreach (var (baglam, sayac) in yazarlar)
                await kaydet(baglam, sayac);
        }
        finally
        {
            foreach (var (baglam, _) in yazarlar)
                await baglam.DisposeAsync();
        }

        return await ctx.Database.SqlQueryRaw<int>(SayacContext.OkuSql).SingleAsync();
    });
}

static async Task DogrudanKaydetAsync(SayacContext baglam, Sayac sayac)
{
    sayac.Deger += 1;
    await baglam.SaveChangesAsync();
}

static async Task CakismadaVazgecAsync(SayacContext baglam, Sayac sayac)
{
    sayac.Deger += 1;

    try
    {
        await baglam.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        // Kullanıcıya "kayıt değişti" denir ve iş bırakılır. Kayıp artık en azından belli.
    }
}

static async Task IstemciKazanirAsync(SayacContext baglam, Sayac sayac)
{
    sayac.Deger += 1;

    try
    {
        await baglam.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException ex)
    {
        var giris = ex.Entries.Single();
        var veritabani = await giris.GetDatabaseValuesAsync()
            ?? throw new InvalidOperationException("Satır silinmiş; bu senaryo silmeyi ölçmüyor.");

        // Orijinal değerler veritabanının güncel hâline çekiliyor; elindeki değer duruyor.
        // Bir sonraki UPDATE'in WHERE'i artık tutar ve elindeki değer yazılır.
        giris.OriginalValues.SetValues(veritabani);
        await baglam.SaveChangesAsync();
    }
}

static async Task YenidenOkuyupArtirAsync(SayacContext baglam, Sayac sayac)
{
    sayac.Deger += 1;

    while (true)
    {
        try
        {
            await baglam.SaveChangesAsync();
            return;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Reload elindeki kopyayı veritabanıyla eşitliyor; artış bu taze değere uygulanıyor.
            await ex.Entries.Single().ReloadAsync();
            sayac.Deger += 1;
        }
    }
}

static Task SunucudaArtirAsync(SayacContext baglam, Sayac sayac)
    => baglam.Set<Sayac>()
        .Where(s => s.Id == sayac.Id)
        .ExecuteUpdateAsync(s => s.SetProperty(x => x.Deger, x => x.Deger + 1));
