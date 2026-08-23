namespace Lab_27_ValueConverter;

/// <summary>
/// Bir kaydın yayın durumu. Sayısal sırası ile alfabetik sırası bilerek birbirini tutmuyor:
/// sayıya göre <c>Taslak(0) &lt; Yayinda(1) &lt; Arsiv(2)</c>, harfe göre
/// <c>Arsiv &lt; Taslak &lt; Yayinda</c>. Ölçümün tamamı bu iki sıranın farkı üzerine kurulu.
/// </summary>
public enum Durum
{
    Taslak = 0,
    Yayinda = 1,
    Arsiv = 2
}

/// <summary>
/// Bu laboratuvara özgü kayıt. <b>Aynı durum iki kez saklanıyor</b>: bir kez EF'in varsayılanıyla
/// (int sütun), bir kez <c>HasConversion&lt;string&gt;()</c> ile (nvarchar sütun).
/// <para>
/// İki sütun aynı satırda durduğu için karşılaştırma adil: veri, satır sayısı ve sorgu şekli
/// aynı, değişen tek şey değerin diskte hangi tiple yattığı.
/// </para>
/// </summary>
public sealed class Kayit
{
    public int Id { get; set; }
    public string Baslik { get; set; } = string.Empty;

    /// <summary>Converter yok: EF enum'u varsayılan olarak <c>int</c> saklar.</summary>
    public Durum DurumSayi { get; set; }

    /// <summary>Converter var: değer diske enum adıyla, <c>nvarchar</c> olarak yazılır.</summary>
    public Durum DurumMetin { get; set; }
}
