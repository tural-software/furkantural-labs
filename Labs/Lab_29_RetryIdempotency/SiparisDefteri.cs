using Microsoft.AspNetCore.Http;

namespace Lab_29_RetryIdempotency;

/// <summary>Sunucunun o senaryoda nasıl bozulacağı.</summary>
public enum Ariza
{
    /// <summary>Siparişi kaydeder, sonra 503 döner: iş yapıldı, cevap hata.</summary>
    KaydedipHataDon,

    /// <summary>Siparişi kaydeder, cevabı istemcinin sabrından geç yazar.</summary>
    KaydedipGecCevapVer
}

/// <summary>
/// Sahte sipariş servisi. Ölçülen tek şey <see cref="SiparisSayisi"/>: sunucunun
/// kaydettiği sipariş adedi.
/// <para>
/// Arıza her zaman kayıttan <b>sonra</b> gelir. Bu bir kurgu değil, en sık görülen
/// sıralamadır: veritabanı commit etti, sonra bir bildirim servisi düştü, bir ağ geçidi
/// zaman aşımına uğradı ya da bağlantı cevap yazılırken koptu. İstemcinin gördüğü
/// hata, işin yapılmadığını söylemez.
/// </para>
/// </summary>
public sealed class SiparisDefteri
{
    public const string AnahtarBasligi = "Idempotency-Key";

    /// <summary>Geç cevabın süresi; istemcinin deneme zaman aşımının üç katı.</summary>
    public static readonly TimeSpan GecikmeSuresi = TimeSpan.FromSeconds(3);

    private readonly Lock _kilit = new();
    private readonly HashSet<string> _gorulenAnahtarlar = [];
    private int _siparisSayisi;
    private int _denemeSayisi;
    private Ariza _ariza;
    private int _arizaliDenemeSayisi;

    /// <summary>Sunucunun kaydettiği sipariş adedi.</summary>
    public int SiparisSayisi
    {
        get { lock (_kilit) return _siparisSayisi; }
    }

    /// <summary>Senaryo başında defteri boşaltır ve ilk kaç denemenin bozulacağını ayarlar.</summary>
    public void Sifirla(Ariza ariza, int arizaliDenemeSayisi)
    {
        lock (_kilit)
        {
            _gorulenAnahtarlar.Clear();
            _siparisSayisi = 0;
            _denemeSayisi = 0;
            _ariza = ariza;
            _arizaliDenemeSayisi = arizaliDenemeSayisi;
        }
    }

    public async Task<IResult> KarsilaAsync(HttpContext context)
    {
        int deneme;
        Ariza ariza;
        bool arizali;

        lock (_kilit)
        {
            deneme = ++_denemeSayisi;
            ariza = _ariza;
            arizali = deneme <= _arizaliDenemeSayisi;

            // Anahtar yoksa her istek yeni bir sipariştir. Anahtar varsa ve daha önce
            // görüldüyse sipariş zaten kayıtlıdır; aynı işi ikinci kez yapmak yerine
            // önceki sonucu dönmek yeterli.
            var anahtar = context.Request.Headers[AnahtarBasligi].ToString();
            if (anahtar.Length == 0 || _gorulenAnahtarlar.Add(anahtar))
                _siparisSayisi++;
        }

        if (!arizali)
            return Results.Created($"/siparisler/{deneme}", null);

        if (ariza == Ariza.KaydedipHataDon)
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

        // İstemci vazgeçse de sunucu işini bitirir; iptal jetonu bilerek verilmedi.
        await Task.Delay(GecikmeSuresi, CancellationToken.None);
        return Results.Created($"/siparisler/{deneme}", null);
    }
}
