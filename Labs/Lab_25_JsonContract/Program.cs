using System.Text.Json;
using FurkanTural_Labs_Application.Diagnostics;
using Lab_25_JsonContract;

// Her senaryoda aynı nesne var: altı alanı da dolu bir kart ödemesi. Nesne, alanları ve
// değerleri sekiz senaryoda da değişmiyor. Değişen tek şey, serileştiricinin nesneyi
// hangi tip üzerinden gördüğü ve hangi seçeneklerle çalıştığı.
//
// Beklentiler ölçümden değil System.Text.Json'ın sözleşmesinden geliyor:
//   · Sözleşmeyi çalışma zamanı tipi değil BİLDİRİLEN tip belirler → taban tiple 3
//   · object bildirilen tipse çalışma zamanı tipi kullanılır → 6
//   · Tip açıkça verilirse (GetType) ya da türemiş tip tanıtılırsa → 6
//   · Varsayılan seçeneklerde okuma büyük/küçük harfe DUYARLI → camelCase JSON ile 0
//   · Duyarsızlık açılınca aynı JSON tam okunur → 6

var varsayilan = new JsonSerializerOptions();
var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);

var report = new LabReport(
    title: "Lab_25 — Serileştirme sözleşmesini nesne değil bildirilen tip yazar",
    claim: $"Aynı {Beklenen.AlanSayisi} alanlı ödeme nesnesi sekiz ayrı yoldan serileştirilip geri okunuyor. " +
           "Ölçülen şey, kaç alanın özgün değeriyle geri geldiği.",
    metric: "Geri gelen");

// ── 1. Türemiş tip uçtan uca: kayıp yok ──────────────────────────────────────
// Ölçünün temeli. Bildirilen tip ile çalışma zamanı tipi aynıysa sorun da yok.
await report.MeasureAsync(
    "1) Türemiş tip, uçtan uca",
    Expectation.Exactly(Beklenen.AlanSayisi),
    () =>
    {
        var json = JsonSerializer.Serialize(Beklenen.Kart(), varsayilan);
        var geri = JsonSerializer.Deserialize<KartOdemesi>(json, varsayilan);

        return Task.FromResult(Beklenen.Eslesen(geri));
    },
    note: "Değişken de nesne de KartOdemesi. Diğer yedi senaryonun ölçüldüğü taban bu satır.");

// ── 2. Taban tipe atanmış referans: üç alan JSON'a hiç yazılmıyor ────────────
// Servis imzası Odeme döndürüyorsa olan tam olarak budur. Nesne eksiksiz, JSON değil.
await report.MeasureAsync(
    "2) Taban tipe atanmış referans",
    Expectation.Exactly(3),
    () =>
    {
        Odeme odeme = Beklenen.Kart();
        var json = JsonSerializer.Serialize(odeme, varsayilan);
        var geri = JsonSerializer.Deserialize<KartOdemesi>(json, varsayilan);

        return Task.FromResult(Beklenen.Eslesen(geri));
    },
    note: "Türemiş üç alan JSON'a hiç yazılmadı; okuma tarafı türemiş tipi istese de yok olanı bulamaz. " +
          "Kaybı yapan atama satırı, serileştirme çağrısı değil.");

// ── 3. Koleksiyon: eleman tipi de bildirilen tiptir ──────────────────────────
// Liste dönen uçlarda aynı kural her elemana ayrı ayrı işler.
await report.MeasureAsync(
    "3) List<Odeme> içinde",
    Expectation.Exactly(3),
    () =>
    {
        var liste = new List<Odeme> { Beklenen.Kart() };
        var json = JsonSerializer.Serialize(liste, varsayilan);
        var geri = JsonSerializer.Deserialize<List<KartOdemesi>>(json, varsayilan);

        return Task.FromResult(Beklenen.Eslesen(geri?.SingleOrDefault()));
    },
    note: "List<Odeme> bildirildiği için her eleman taban sözleşmesiyle yazıldı. " +
          "Tek nesnede görülen kayıp, listede eleman sayısı kadar tekrarlanır.");

// ── 4. object: kuralın tek istisnası ─────────────────────────────────────────
// Sezgiye aykırı: en az bilgi taşıyan bildirim en çok alanı koruyor. object
// bildirildiğinde System.Text.Json çalışma zamanı tipine bakar.
await report.MeasureAsync(
    "4) object olarak",
    Expectation.Exactly(Beklenen.AlanSayisi),
    () =>
    {
        object kutu = Beklenen.Kart();
        var json = JsonSerializer.Serialize(kutu, varsayilan);
        var geri = JsonSerializer.Deserialize<KartOdemesi>(json, varsayilan);

        return Task.FromResult(Beklenen.Eslesen(geri));
    },
    note: "Taban tip 3 veriyordu, object 6 veriyor. Bildirim ne kadar belirsizse " +
          "sözleşme o kadar geniş — kuralın en şaşırtıcı sonucu bu.");

// ── 5. Tipi açıkça vermek: özniteliksiz çözüm ────────────────────────────────
await report.MeasureAsync(
    "5) GetType() verilerek",
    Expectation.Exactly(Beklenen.AlanSayisi),
    () =>
    {
        Odeme odeme = Beklenen.Kart();
        var json = JsonSerializer.Serialize(odeme, odeme.GetType(), varsayilan);
        var geri = JsonSerializer.Deserialize<KartOdemesi>(json, varsayilan);

        return Task.FromResult(Beklenen.Eslesen(geri));
    },
    note: "2. senaryonun aynısı, tek fark tipin çağrıya elle verilmesi. Modele dokunmadan " +
          "kaybı durduran en kısa yol.");

// ── 6. [JsonDerivedType]: okuma tarafını da düzelten tek seçenek ─────────────
// 4 ve 5 yazmayı düzeltiyor ama JSON'da tipin ne olduğu yazmıyor; taban tipe okumak
// hâlâ türemiş nesne vermez. Öznitelik ayrımcıyı JSON'a koyar.
await report.MeasureAsync(
    "6) [JsonDerivedType], tabana okuma",
    Expectation.Exactly(Beklenen.AlanSayisi),
    () =>
    {
        TanimliOdeme odeme = Beklenen.TanimliKart();
        var json = JsonSerializer.Serialize(odeme, varsayilan);
        var geri = JsonSerializer.Deserialize<TanimliOdeme>(json, varsayilan);

        return Task.FromResult(Beklenen.Eslesen(geri));
    },
    note: "Taban tiple yazıldı, taban tiple okundu, altı alan da geldi: dönen nesne " +
          "TanimliKartOdemesi. Ayrımcı JSON'a yazıldığı için tip bilgisi kayıtta duruyor.");

// ── 7. Ad eşleşmesi: iki uç, iki farklı varsayılan ───────────────────────────
// Yazan taraf web varsayılanlarını kullanıyor (camelCase), okuyan taraf seçeneksiz
// JsonSerializer çağırıyor. Varsayılan okuma büyük/küçük harfe duyarlıdır.
await report.MeasureAsync(
    "7) camelCase yaz, seçeneksiz oku",
    Expectation.Exactly(0),
    () =>
    {
        var json = JsonSerializer.Serialize(Beklenen.Kart(), web);
        var geri = JsonSerializer.Deserialize<KartOdemesi>(json, varsayilan);

        return Task.FromResult(Beklenen.Eslesen(geri));
    },
    note: "Tip doğru, alanlar JSON'da tam, yine de hiçbiri okunamadı: nesne varsayılan " +
          "değerlerle doldu. Ne istisna var ne uyarı.");

// ── 8. Aynı JSON, duyarsız okuma ─────────────────────────────────────────────
await report.MeasureAsync(
    "8) camelCase yaz, duyarsız oku",
    Expectation.Exactly(Beklenen.AlanSayisi),
    () =>
    {
        var json = JsonSerializer.Serialize(Beklenen.Kart(), web);
        var geri = JsonSerializer.Deserialize<KartOdemesi>(json, web);

        return Task.FromResult(Beklenen.Eslesen(geri));
    },
    note: "7. senaryonun birebir aynı JSON'u. Değişen tek şey okuma seçenekleri; " +
          "kaybı yapan JSON değil, iki ucun farklı varsayılanlarla çalışmasıydı.");

return report.Print();
