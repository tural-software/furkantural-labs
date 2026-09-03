namespace Lab_28_OptimisticConcurrency;

/// <summary>
/// Bu laboratuvara özgü kayıt: bir görüntülenme sayacı. On yazarın hepsi aynı satırı
/// okuyor, <see cref="Deger"/>'i bir artırıp kaydediyor. Ölçülen şey işin sonunda
/// veritabanında duran değer.
/// <para>
/// <see cref="Surum"/> sütunu tabloda hep var (<c>rowversion</c>), ama yalnız
/// <see cref="SurumContext"/> onu modele alıyor. Öteki context'ler sütunu görmezden
/// geliyor; böylece "token yok" senaryosu sütunun yokluğunu değil, EF'in ondan
/// habersiz olmasını ölçüyor — üretimde de tuzak tam olarak bu şekilde kurulur.
/// </para>
/// </summary>
public sealed class Sayac
{
    public int Id { get; set; }
    public string Ad { get; set; } = string.Empty;
    public int Deger { get; set; }
    public byte[] Surum { get; set; } = [];
}
