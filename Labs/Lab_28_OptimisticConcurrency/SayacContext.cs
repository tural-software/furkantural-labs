using FurkanTural_Labs_Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Lab_28_OptimisticConcurrency;

/// <summary>
/// Ölçüm kaydının dünyası. <see cref="Sayac"/> paylaşılan <see cref="LabsDbContext"/>'e
/// eklenmedi: oraya eklenseydi her laboratuvarın modeline bir tablo daha girerdi. Model
/// önbelleği context tipine göre ayrıldığı için bu türevler kimseyi etkilemez.
/// <para>
/// Aynı tablo dört ayrı modelle okunuyor; değişen tek şey EF'in <c>UPDATE</c>'in
/// <c>WHERE</c> kısmına ne yazdığı. Tablonun kendisi kalıcı değil: <see cref="OlusturSql"/>
/// her senaryonun kendi transaction'ında çalışır ve senaryo bitince rollback ile kaybolur.
/// </para>
/// </summary>
public abstract class SayacContext(DbContextOptions<LabsDbContext> options) : LabsDbContext(options)
{
    public const string TabloAdi = "Lab28_Sayaclar";

    /// <summary>Boş tabloya atılan ilk satırın kimliği; her senaryo tabloyu sıfırdan kurar.</summary>
    public const int SayacId = 1;

    /// <summary>
    /// <c>Surum</c> sütunu her senaryoda tabloda; SQL Server onu her <c>UPDATE</c>'te
    /// kendisi ilerletir. Sütunun varlığı hiçbir şeyi korumaz — EF'in ona bakması gerekir.
    /// </summary>
    public const string OlusturSql = $"""
        CREATE TABLE {TabloAdi} (
            Id    int IDENTITY(1,1) NOT NULL PRIMARY KEY,
            Ad    nvarchar(50) NOT NULL,
            Deger int NOT NULL,
            Surum rowversion NOT NULL
        );
        INSERT INTO {TabloAdi} (Ad, Deger) VALUES (N'goruntulenme', 0);
        """;

    /// <summary>Sayacın veritabanındaki son değeri; ölçüm buradan okunur.</summary>
    public static readonly string OkuSql = $"SELECT Deger AS [Value] FROM {TabloAdi} WHERE Id = {SayacId}";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Sayac>(b =>
        {
            b.ToTable(TabloAdi);
            b.HasKey(s => s.Id);
            b.Property(s => s.Ad).HasMaxLength(50).IsRequired();
            Yapilandir(b);
        });
    }

    /// <summary>Türevler arasındaki tek fark: hangi sütun çakışmayı yakalıyor.</summary>
    protected abstract void Yapilandir(EntityTypeBuilder<Sayac> b);
}

/// <summary>Token yok. <c>UPDATE ... WHERE Id = @p</c>: satır yerindeyse yazar.</summary>
public sealed class SerbestContext(DbContextOptions<LabsDbContext> options) : SayacContext(options)
{
    protected override void Yapilandir(EntityTypeBuilder<Sayac> b)
        => b.Ignore(s => s.Surum);
}

/// <summary>
/// <c>rowversion</c> token. <c>UPDATE ... WHERE Id = @p AND Surum = @okunan</c>: satır
/// okunduğundan beri herhangi bir sütunu değiştiyse sıfır satır etkilenir ve EF
/// <c>DbUpdateConcurrencyException</c> fırlatır.
/// </summary>
public sealed class SurumContext(DbContextOptions<LabsDbContext> options) : SayacContext(options)
{
    protected override void Yapilandir(EntityTypeBuilder<Sayac> b)
        => b.Property(s => s.Surum).IsRowVersion();
}

/// <summary>
/// Token olarak sayacın kendisi. Yeni sütun yok; <c>WHERE Id = @p AND Deger = @okunan</c>.
/// Yalnız bu sütundaki değişikliği yakalar, ama yakalanması gereken de tam olarak o.
/// </summary>
public sealed class DegerTokenContext(DbContextOptions<LabsDbContext> options) : SayacContext(options)
{
    protected override void Yapilandir(EntityTypeBuilder<Sayac> b)
    {
        b.Ignore(s => s.Surum);
        b.Property(s => s.Deger).IsConcurrencyToken();
    }
}

/// <summary>
/// Token olarak kimsenin değiştirmediği sütun. <c>WHERE Id = @p AND Ad = @okunan</c>
/// her zaman tutar; token var ama koruduğu şey değişmiyor.
/// </summary>
public sealed class AdTokenContext(DbContextOptions<LabsDbContext> options) : SayacContext(options)
{
    protected override void Yapilandir(EntityTypeBuilder<Sayac> b)
    {
        b.Ignore(s => s.Surum);
        b.Property(s => s.Ad).IsConcurrencyToken();
    }
}
