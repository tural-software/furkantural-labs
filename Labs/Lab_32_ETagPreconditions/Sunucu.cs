using FurkanTural_Labs_Host;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Lab_32_ETagPreconditions;

/// <summary>Sunucunun ön koşulu nasıl ele aldığı. Laboratuvarda değişen tek şey budur.</summary>
public enum OnKosul
{
    /// <summary>If-Match okunmaz. Yazma her zaman uygulanır.</summary>
    YokSay,

    /// <summary>If-Match varsa uygulanır; yoksa yazma kabul edilir (RFC 9110 varsayılanı).</summary>
    Uygula,

    /// <summary>If-Match yoksa istek 428 ile geri çevrilir.</summary>
    Zorunlu
}

/// <summary>
/// Tek kayıt üzerinde oku-değiştir-yaz sunan minimal uç.
/// <para>
/// Depo süreç içinde ve kilitsiz; laboratuvar istekleri sıra ile gönderiyor. Ölçülen şey
/// iş parçacığı güvenliği değil <b>HTTP sözleşmesi</b>: iki istemci aynı sürümü okuyup
/// sonra sırayla yazdığında sunucunun kaçını kabul ettiği. Gerçek kayıp update de böyle
/// doğar — yazmalar sunucuda zaten sıralıdır, kaybı yaratan şey okumanın bayatlamasıdır.
/// </para>
/// </summary>
public static class Sunucu
{
    public static Task<LabApp> BaslatAsync(KayitDeposu depo, OnKosul kosul, bool yanittaEtag = true)
        => LabsWebHost.StartAsync(app =>
        {
            app.MapGet("/kayit/{ad}", (HttpContext ctx, string ad) =>
            {
                var kayit = depo.Oku(ad);
                ctx.Response.Headers.ETag = kayit.ETag;

                return Results.Text(kayit.Deger);
            });

            app.MapPut("/kayit/{ad}", (HttpContext ctx, string ad, Guncelleme govde) =>
            {
                var kayit = depo.Oku(ad);
                var gelen = ctx.Request.GetTypedHeaders().IfMatch;

                if (gelen.Count == 0)
                {
                    if (kosul == OnKosul.Zorunlu)
                        return Results.StatusCode(StatusCodes.Status428PreconditionRequired);
                }
                else if (kosul != OnKosul.YokSay && !Esliyor(kayit, gelen))
                {
                    return Results.StatusCode(StatusCodes.Status412PreconditionFailed);
                }

                var yeni = depo.Yaz(ad, govde.Deger);

                if (yanittaEtag)
                    ctx.Response.Headers.ETag = yeni.ETag;

                return Results.NoContent();
            });
        });

    // Karşılaştırmayı elle yapmıyoruz: EntityTagHeaderValue.Compare çerçevenin kendi
    // uygulamasıdır ve güçlü karşılaştırma istendiğinde zayıf doğrulayıcıyı hiçbir koşulda
    // eşleştirmez. Yıldız ayrı ele alınır, çünkü o bir doğrulayıcı değil varlık denetimidir.
    private static bool Esliyor(Kayit kayit, IList<EntityTagHeaderValue> gelen)
    {
        if (gelen.Any(e => e.Tag == "*"))
            return true;

        var mevcut = EntityTagHeaderValue.Parse(kayit.ETag);

        return gelen.Any(e => mevcut.Compare(e, useStrongComparison: true));
    }
}

/// <summary>PUT gövdesi.</summary>
public sealed record Guncelleme(string Deger);

/// <summary>Kaydın bir sürümü. ETag sürümden türetilir ve güçlü doğrulayıcıdır.</summary>
public sealed record Kayit(string Deger, int Surum)
{
    public string ETag => $"\"v{Surum}\"";
}

/// <summary>Senaryoların paylaştığı depo. Her senaryo kendi anahtarını kullanır.</summary>
public sealed class KayitDeposu
{
    private readonly Dictionary<string, Kayit> _kayitlar = [];

    public void Kur(string ad, string deger) => _kayitlar[ad] = new Kayit(deger, 1);

    public Kayit Oku(string ad) => _kayitlar[ad];

    public Kayit Yaz(string ad, string deger)
    {
        var yeni = new Kayit(deger, _kayitlar[ad].Surum + 1);
        _kayitlar[ad] = yeni;

        return yeni;
    }
}
