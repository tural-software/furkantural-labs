using System.Net;
using System.Net.Http.Json;
using FurkanTural_Labs_Application.Diagnostics;
using FurkanTural_Labs_Host;
using Lab_32_ETagPreconditions;
using Microsoft.Net.Http.Headers;

// Her senaryoda iki istemci aynı kaydı AYNI sürümden okuyup değiştirmeye çalışıyor. Kayıt
// aynı, okuma anı aynı, gönderilen gövde aynı. Değişen tek şey sunucunun ön koşulu nasıl
// ele aldığı ve istemcinin elinde hangi doğrulayıcının olduğu.
//
// Ölçülen şey kabul edilen yazma sayısı. İki yazmanın ikisi de kabul edildiyse biri
// sessizce kayboldu demektir — o yüzden burada "2" iyi haber değil.
//
// Beklentiler ölçümden değil HTTP sözleşmesinden (RFC 9110) geliyor:
//   · ön koşul okunmazsa son yazan kazanır, ikisi de kabul edilir       → 2
//   · aynı doğrulayıcıyı gönderen ikinci istemci 412 alır               → 1
//   · 412 alan istemci yeniden okuyup gönderirse ikisi de uygulanır     → 2
//   · If-Match zorunluysa göndermeyen istemci 428 alır                  → 0
//   · If-Match güçlü karşılaştırma ister; zayıf doğrulayıcı eşleşmez    → 0
//   · If-Match: * varlık denetimidir, eşzamanlılık denetimi değil       → 2
//   · PUT yanıtı yeni ETag döndürmezse istemcinin eldeki etiketi bayat  → 1
//   · aynı kurgu, yanıt yeni ETag döndürürse ikinci yazma da geçer      → 2

var depo = new KayitDeposu();

await using var gevsek = await Sunucu.BaslatAsync(depo, OnKosul.YokSay);
await using var siki = await Sunucu.BaslatAsync(depo, OnKosul.Uygula);
await using var sikiEtagsiz = await Sunucu.BaslatAsync(depo, OnKosul.Uygula, yanittaEtag: false);
await using var zorunlu = await Sunucu.BaslatAsync(depo, OnKosul.Zorunlu);

var report = new LabReport(
    title: "Lab_32 — ETag ve If-Match: iki istemci aynı kaydı değiştirince kaç yazma kabul ediliyor",
    claim: "Her senaryoda iki istemci aynı kaydı aynı sürümden okuyup değiştirmeye çalışıyor. " +
           "Değişen tek şey sunucunun ön koşulu ele alışı ve istemcinin doğrulayıcısı; " +
           "ölçülen şey kabul edilen yazma sayısı.",
    metric: "Kabul edilen");

// ── 1–2. Ön koşulun olması ile olmaması ─────────────────────────────────────
await report.MeasureAsync(
    "1) Sunucu If-Match'e bakmıyor",
    Expectation.Exactly(2),
    OnKosulsuzAsync,
    note: "İki yazma da kabul edildi ve kayıtta yalnız ikincisi duruyor. Birinci istemci " +
          "200 aldı, işinin kaybolduğunu öğrenmesinin hiçbir yolu yok. Sessiz kayıp budur.");

await report.MeasureAsync(
    "2) İkisi de aynı doğrulayıcıyı gönderiyor",
    Expectation.Exactly(1),
    OnKosulluAsync,
    note: "Tek fark sunucunun If-Match'i okuması. İkinci istemci 412 aldı, birincinin " +
          "yazması korundu. Kayıp kazaya değil, artık istemcinin gördüğü bir hataya dönüştü.");

// ── 3. 412 bir son değil, akışın parçası ────────────────────────────────────
await report.MeasureAsync(
    "3) 412 alan istemci yeniden okuyup gönderiyor",
    Expectation.Exactly(2),
    YenidenDenemeAsync,
    note: "Yine iki kabul, ama bu kez kayıp yok: ikinci yazma birincinin ürettiği sürümün " +
          "üstüne bindi. 1. senaryoyla aynı sayı, taban tabana zıt sonuç — sayıya değil " +
          "neyin üstüne yazıldığına bakmak gerekiyor.");

// ── 4. Göndermeyen istemciye ne söylenir ────────────────────────────────────
await report.MeasureAsync(
    "4) Sunucu If-Match zorunlu tutuyor, istemci göndermiyor",
    Expectation.Exactly(0),
    ZorunluAsync,
    note: "İki istek de 428 aldı: 412 'gönderdiğin eşleşmedi' derken 428 'ön koşul " +
          "göndermedin' diyor. Ayrımı korumak istemciye ne yapacağını söylüyor; ikisini " +
          "412'de birleştiren API, eksik başlığı bayat başlık gibi gösterir.");

// ── 5. Doğrulayıcının gücü ──────────────────────────────────────────────────
await report.MeasureAsync(
    "5) Zayıf doğrulayıcı (W/) ile If-Match",
    Expectation.Exactly(0),
    ZayifDogrulayiciAsync,
    note: "Sürüm doğru, etiket doğru, tek fark başındaki W/ — ikisi de 412. If-Match güçlü " +
          "karşılaştırma ister ve zayıf doğrulayıcı hiçbir koşulda eşleşmez. Aynı sürümün " +
          "güçlü hâli kontrol amaçlı gönderildiğinde kabul edildi, yani red bayatlıktan değil.");

// ── 6. Yıldız bir ön koşul mu ───────────────────────────────────────────────
await report.MeasureAsync(
    "6) If-Match: * iki istemcide",
    Expectation.Exactly(2),
    YildizAsync,
    note: "Ön koşul gönderildi, sunucu onu uyguladı ve sonuç 1. senaryoyla birebir aynı: " +
          "iki kabul, bir kayıp. Yıldız 'bu kaynak var mı' diye sorar, 'benim okuduğum " +
          "sürüm mü' diye sormaz. Kayıp update'e karşı hiçbir şey yapmaz.");

// ── 7–8. ETag'i kim taze tutuyor ────────────────────────────────────────────
await report.MeasureAsync(
    "7) PUT yanıtı yeni ETag döndürmüyor",
    Expectation.Exactly(1),
    EtagsizYanitAsync,
    note: "Tek istemci, üst üste iki yazma, arada başka kimse yok — ve ikincisi 412. " +
          "İstemcinin elindeki tek etiket ilk GET'ten kalma, yazma onu bayatlattı. Sunucu " +
          "ön koşulu doğru uyguluyor; eksik olan şey yanıtın yeni sürümü bildirmemesi.");

await report.MeasureAsync(
    "8) PUT yanıtı yeni ETag döndürüyor",
    Expectation.Exactly(2),
    EtagliYanitAsync,
    note: "Aynı istemci, aynı iki yazma, tek fark yanıtın ETag taşıması: ikisi de geçti. " +
          "Yazma yanıtına yeni doğrulayıcıyı koymak istemciyi her turda bir GET'ten " +
          "kurtarıyor; koymamak onu 412 almaya mahkûm ediyor.");

return report.Print();

// ── Senaryolar ──────────────────────────────────────────────────────────────
// Her senaryo kendi kayıt anahtarını kullanır, böylece senaryolar birbirinin sürümünü
// görmez ve sıraya bağlı değildir. Kurulumun gerçekten kurulduğu her senaryoda ayrıca
// denetlenir: kurulmadıysa ölçüm başka bir şeyi ölçeceği için senaryo fırlatır.

async Task<int> OnKosulsuzAsync()
{
    const string ad = "onkosulsuz";
    depo.Kur(ad, "baslangic");

    var etagA = await EtagOkuAsync(gevsek, ad);
    var etagB = await EtagOkuAsync(gevsek, ad);
    if (etagA != etagB)
        throw new InvalidOperationException("İki istemci aynı sürümü okumadı; senaryo kurulmadı.");

    var a = await YazAsync(gevsek, ad, "A yazdi", etagA);
    var b = await YazAsync(gevsek, ad, "B yazdi", etagB);

    var son = await DegerOkuAsync(gevsek, ad);
    if (son != "B yazdi")
        throw new InvalidOperationException($"Son değer '{son}'; kayıp update kurulmadı.");

    return Kabul(a.Durum, b.Durum);
}

async Task<int> OnKosulluAsync()
{
    const string ad = "onkosullu";
    depo.Kur(ad, "baslangic");

    var etag = await EtagOkuAsync(siki, ad);

    var a = await YazAsync(siki, ad, "A yazdi", etag);
    var b = await YazAsync(siki, ad, "B yazdi", etag);

    if (b.Durum != HttpStatusCode.PreconditionFailed)
        throw new InvalidOperationException($"412 beklenirken {(int)b.Durum} geldi.");

    var son = await DegerOkuAsync(siki, ad);
    if (son != "A yazdi")
        throw new InvalidOperationException($"Son değer '{son}'; A'nın yazması korunmadı.");

    return Kabul(a.Durum, b.Durum);
}

async Task<int> YenidenDenemeAsync()
{
    const string ad = "yeniden-deneme";
    depo.Kur(ad, "baslangic");

    var etag = await EtagOkuAsync(siki, ad);

    var a = await YazAsync(siki, ad, "A yazdi", etag);
    var bBayat = await YazAsync(siki, ad, "B yazdi", etag);
    if (bBayat.Durum != HttpStatusCode.PreconditionFailed)
        throw new InvalidOperationException("Bayat doğrulayıcı reddedilmedi; senaryo kurulmadı.");

    var taze = await EtagOkuAsync(siki, ad);
    var bTaze = await YazAsync(siki, ad, "B yazdi, A uzerine", taze);

    if (depo.Oku(ad).Surum != 3)
        throw new InvalidOperationException($"İki yazma uygulanmadı; sürüm {depo.Oku(ad).Surum}.");

    return Kabul(a.Durum, bBayat.Durum, bTaze.Durum);
}

async Task<int> ZorunluAsync()
{
    const string ad = "zorunlu";
    depo.Kur(ad, "baslangic");

    var a = await YazAsync(zorunlu, ad, "A yazdi", ifMatch: null);
    var b = await YazAsync(zorunlu, ad, "B yazdi", ifMatch: null);

    if (a.Durum != HttpStatusCode.PreconditionRequired || b.Durum != HttpStatusCode.PreconditionRequired)
        throw new InvalidOperationException($"428 beklenirken {(int)a.Durum}/{(int)b.Durum} geldi.");

    return Kabul(a.Durum, b.Durum);
}

async Task<int> ZayifDogrulayiciAsync()
{
    const string ad = "zayif";
    depo.Kur(ad, "baslangic");

    var guclu = await EtagOkuAsync(siki, ad);
    var zayif = "W/" + guclu;

    var a = await YazAsync(siki, ad, "A yazdi", zayif);
    var b = await YazAsync(siki, ad, "B yazdi", zayif);

    // Kontrol: red zayıflıktan mı geldi, bayatlıktan mı? Aynı sürümün GÜÇLÜ hâli kabul
    // ediliyorsa sebep zayıflıktır. Bu yazma ölçüme girmiyor, ölçüm yukarıda bitti.
    var kontrol = await YazAsync(siki, ad, "kontrol", guclu);
    if (kontrol.Durum != HttpStatusCode.NoContent)
        throw new InvalidOperationException(
            $"Güçlü doğrulayıcı da reddedildi ({(int)kontrol.Durum}); ölçüm zayıflığı göstermiyor.");

    return Kabul(a.Durum, b.Durum);
}

async Task<int> YildizAsync()
{
    const string ad = "yildiz";
    depo.Kur(ad, "baslangic");

    var a = await YazAsync(siki, ad, "A yazdi", "*");
    var b = await YazAsync(siki, ad, "B yazdi", "*");

    var son = await DegerOkuAsync(siki, ad);
    if (son != "B yazdi")
        throw new InvalidOperationException($"Son değer '{son}'; yıldız senaryosu kurulmadı.");

    return Kabul(a.Durum, b.Durum);
}

async Task<int> EtagsizYanitAsync()
{
    const string ad = "etagsiz";
    depo.Kur(ad, "baslangic");

    var etag = await EtagOkuAsync(sikiEtagsiz, ad);

    var ilk = await YazAsync(sikiEtagsiz, ad, "ilk", etag);
    if (ilk.ETag is not null)
        throw new InvalidOperationException("Yanıt ETag taşıyor; senaryo kurulmadı.");

    var ikinci = await YazAsync(sikiEtagsiz, ad, "ikinci", etag);

    return Kabul(ilk.Durum, ikinci.Durum);
}

async Task<int> EtagliYanitAsync()
{
    const string ad = "etagli";
    depo.Kur(ad, "baslangic");

    var etag = await EtagOkuAsync(siki, ad);

    var ilk = await YazAsync(siki, ad, "ilk", etag);
    if (ilk.ETag is null || ilk.ETag == etag)
        throw new InvalidOperationException("Yanıt yeni ETag taşımıyor; senaryo kurulmadı.");

    var ikinci = await YazAsync(siki, ad, "ikinci", ilk.ETag);

    return Kabul(ilk.Durum, ikinci.Durum);
}

// ── İstemci tarafı ──────────────────────────────────────────────────────────

static int Kabul(params HttpStatusCode[] durumlar)
    => durumlar.Count(d => (int)d is >= 200 and < 300);

static async Task<string> EtagOkuAsync(LabApp app, string ad)
{
    using var yanit = await app.Client.GetAsync($"/kayit/{ad}");
    yanit.EnsureSuccessStatusCode();

    return yanit.Headers.ETag?.ToString()
        ?? throw new InvalidOperationException("GET yanıtı ETag taşımıyor; ölçüm kurulamaz.");
}

static async Task<string> DegerOkuAsync(LabApp app, string ad)
{
    using var yanit = await app.Client.GetAsync($"/kayit/{ad}");
    yanit.EnsureSuccessStatusCode();

    return await yanit.Content.ReadAsStringAsync();
}

// Doğrulayıcı TryAddWithoutValidation ile yazılıyor: zayıf etiket ve yıldız gibi değerleri
// istemci kitaplığı kendi kurallarına göre düzeltmeden, birebir tele koymak gerekiyor.
static async Task<(HttpStatusCode Durum, string? ETag)> YazAsync(
    LabApp app, string ad, string deger, string? ifMatch)
{
    using var istek = new HttpRequestMessage(HttpMethod.Put, $"/kayit/{ad}")
    {
        Content = JsonContent.Create(new Guncelleme(deger))
    };

    if (ifMatch is not null)
        istek.Headers.TryAddWithoutValidation(HeaderNames.IfMatch, ifMatch);

    using var yanit = await app.Client.SendAsync(istek);

    return (yanit.StatusCode, yanit.Headers.ETag?.ToString());
}
