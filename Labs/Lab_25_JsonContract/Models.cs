using System.Text.Json.Serialization;

namespace Lab_25_JsonContract;

/// <summary>
/// Ödemenin her ödeme biçiminde bulunan üç alanı. Senaryoların yarısında referansın
/// <b>bildirilen</b> tipi budur — ölçülen kaybın kaynağı da o.
/// </summary>
public class Odeme
{
    public string Numara { get; set; } = string.Empty;
    public decimal Tutar { get; set; }
    public DateTime An { get; set; }
}

/// <summary>Kartla ödemeye özgü üç alan daha; nesnenin çalışma zamanı tipi hep budur.</summary>
public class KartOdemesi : Odeme
{
    public string KartSahibi { get; set; } = string.Empty;
    public string Son4 { get; set; } = string.Empty;
    public string Banka { get; set; } = string.Empty;
}

/// <summary>
/// Yukarıdakinin birebir aynısı, tek fark tabanın türemiş tipi <b>tanıması</b>.
/// Ayrı bir tip çifti gerekti: öznitelik <see cref="Odeme"/> üzerine konsaydı taban tiple
/// yazan senaryolar da düzelir ve tuzak tabloda hiç görünmezdi.
/// </summary>
[JsonDerivedType(typeof(TanimliKartOdemesi), "kart")]
public class TanimliOdeme
{
    public string Numara { get; set; } = string.Empty;
    public decimal Tutar { get; set; }
    public DateTime An { get; set; }
}

/// <inheritdoc cref="KartOdemesi"/>
public class TanimliKartOdemesi : TanimliOdeme
{
    public string KartSahibi { get; set; } = string.Empty;
    public string Son4 { get; set; } = string.Empty;
    public string Banka { get; set; } = string.Empty;
}

/// <summary>
/// Ölçümün paydası: altı alan ve beklenen değerleri.
/// <para>
/// Değerlerin hiçbiri tipin varsayılanı değil. Bu bilerek: alan adı eşleşmediğinde nesne
/// varsayılan değerle doldurulur ve "geri geldi" ile "hiç okunamadı" ayırt edilemez olurdu.
/// </para>
/// </summary>
public static class Beklenen
{
    /// <summary>Ölçülen alan sayısı; tablodaki her sayı bunun kaçta kaçı olduğunu söyler.</summary>
    public const int AlanSayisi = 6;

    public const string Numara = "SIP-2026-000481";
    public const decimal Tutar = 1349.90m;
    public const string KartSahibi = "F. Tural";
    public const string Son4 = "4417";
    public const string Banka = "Ziraat";

    public static readonly DateTime An = new(2026, 3, 14, 9, 30, 0, DateTimeKind.Utc);

    /// <summary>Ölçülecek nesne. Her senaryo bunun bir kopyasıyla başlar.</summary>
    public static KartOdemesi Kart() => new()
    {
        Numara = Numara,
        Tutar = Tutar,
        An = An,
        KartSahibi = KartSahibi,
        Son4 = Son4,
        Banka = Banka
    };

    /// <inheritdoc cref="Kart"/>
    public static TanimliKartOdemesi TanimliKart() => new()
    {
        Numara = Numara,
        Tutar = Tutar,
        An = An,
        KartSahibi = KartSahibi,
        Son4 = Son4,
        Banka = Banka
    };

    /// <summary>
    /// Geri okunan nesnede özgün değerini koruyan alanları sayar.
    /// <para>
    /// Türemiş alanlar yalnız nesne gerçekten türemiş tipe materyalize olduysa sayılır;
    /// taban tipe düşen bir sonuçta o alanlar <b>yoktur</b>, boş değildir.
    /// </para>
    /// </summary>
    /// <param name="geri">Serileştirilip geri okunan nesne.</param>
    public static int Eslesen(Odeme? geri)
    {
        if (geri is null) return 0;

        var sayi = Ortak(geri.Numara, geri.Tutar, geri.An);

        if (geri is KartOdemesi kart)
            sayi += Turemis(kart.KartSahibi, kart.Son4, kart.Banka);

        return sayi;
    }

    /// <inheritdoc cref="Eslesen(Odeme?)"/>
    public static int Eslesen(TanimliOdeme? geri)
    {
        if (geri is null) return 0;

        var sayi = Ortak(geri.Numara, geri.Tutar, geri.An);

        if (geri is TanimliKartOdemesi kart)
            sayi += Turemis(kart.KartSahibi, kart.Son4, kart.Banka);

        return sayi;
    }

    private static int Ortak(string numara, decimal tutar, DateTime an)
    {
        var sayi = 0;
        if (numara == Numara) sayi++;
        if (tutar == Tutar) sayi++;
        if (an == An) sayi++;
        return sayi;
    }

    private static int Turemis(string kartSahibi, string son4, string banka)
    {
        var sayi = 0;
        if (kartSahibi == KartSahibi) sayi++;
        if (son4 == Son4) sayi++;
        if (banka == Banka) sayi++;
        return sayi;
    }
}
