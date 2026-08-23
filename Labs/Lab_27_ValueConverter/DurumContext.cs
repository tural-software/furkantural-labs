using FurkanTural_Labs_Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Lab_27_ValueConverter;

/// <summary>
/// Ölçüm kaydının dünyası. <see cref="Kayit"/> paylaşılan <see cref="LabsDbContext"/>'e
/// eklenmedi: oraya eklenseydi her laboratuvarın modeline bir tablo daha girerdi ve tohum
/// verisinin şeması bu laboratuvarın konusuna göre şekillenirdi. Model önbelleği context
/// tipine göre ayrıldığı için bu türev kimseyi etkilemez.
/// <para>
/// Tablonun kendisi de kalıcı değil: <see cref="OlusturSql"/> her senaryonun kendi
/// transaction'ında çalıştırılır ve senaryo bitince rollback ile birlikte kaybolur.
/// </para>
/// </summary>
public sealed class DurumContext(DbContextOptions<LabsDbContext> options) : LabsDbContext(options)
{
    public const string TabloAdi = "Lab27_Kayitlar";

    /// <summary>
    /// Sütun tipleri ölçümün yarısı: <c>DurumSayi</c> int, <c>DurumMetin</c> nvarchar.
    /// Karşılaştırma ve sıralama bu tiplere göre yapılacak.
    /// </summary>
    public const string OlusturSql = $"""
        CREATE TABLE {TabloAdi} (
            Id         int IDENTITY(1,1) NOT NULL PRIMARY KEY,
            Baslik     nvarchar(100) NOT NULL,
            DurumSayi  int NOT NULL,
            DurumMetin nvarchar(20) NOT NULL
        );
        """;

    /// <summary>
    /// Metin sütuna enum'da karşılığı olmayan bir değer yazar — bir üyenin yeniden
    /// adlandırılması, elle düzeltme ya da başka bir sürümden gelen veri sonrası
    /// veritabanında kalabilecek değerin karşılığı. Hedef: 10 arşiv kaydı.
    /// </summary>
    public const string MetniBozSql = $"UPDATE {TabloAdi} SET DurumMetin = N'Arsivlendi' WHERE DurumSayi = 2;";

    /// <summary>
    /// Aynı bozulmanın sayısal sütundaki karşılığı: enum'da tanımlı olmayan bir sayı.
    /// Hedef satırlar metin sütundan seçiliyor çünkü bu senaryoda bozulan sütun o değil.
    /// </summary>
    public const string SayiyiBozSql = $"UPDATE {TabloAdi} SET DurumSayi = 99 WHERE DurumMetin = N'Arsiv';";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Kayit>(b =>
        {
            b.ToTable(TabloAdi);
            b.HasKey(k => k.Id);
            b.Property(k => k.Baslik).HasMaxLength(100).IsRequired();

            // Tek satırlık fark. Converter yalnız DurumMetin'e takılı; DurumSayi EF'in
            // varsayılanıyla, yani int olarak saklanıyor.
            b.Property(k => k.DurumMetin).HasConversion<string>().HasMaxLength(20).IsRequired();
        });
    }
}
