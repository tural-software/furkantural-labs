using FurkanTural_Labs_Domain.Entities;
using FurkanTural_Labs_Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Lab_23_QueryFilter;

/// <summary>
/// Filtreli dünya. <see cref="LabsDbContext"/> bilerek filtresizdir — filtre oraya
/// konsaydı diğer laboratuvarların saydığı satırlar sessizce değişirdi. Bu yüzden filtre
/// yalnız bu laboratuvara ait bu türevde tanımlanır; model önbelleği context tipine göre
/// ayrıldığı için tohum verisi ve öteki laboratuvarlar bundan etkilenmez.
/// <para>
/// Filtreler <b>adlandırılmıştır</b> (EF Core 10). Adsız aşırı yükleme tek bir filtre
/// tutar ve ikincisi birinciyi ezerdi; ad verildiğinde ikisi yan yana yaşar ve sorgu
/// başına <b>ayrı ayrı</b> kapatılabilir.
/// </para>
/// </summary>
public sealed class SoftDeleteContext(DbContextOptions<LabsDbContext> options) : LabsDbContext(options)
{
    /// <summary>Kaydın silinmiş sayılmaması koşulu. Monorepo'daki BaseEntityConfiguration ile aynı ifade.</summary>
    public const string SoftDelete = "SoftDelete";

    /// <summary>Yayın anı gelmemiş yazının listede görünmemesi koşulu.</summary>
    public const string Published = "Published";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Blog iki filtre taşır; Comment yalnız birini. Kesişimin nerede olduğu
        // ölçümün yarısını oluşturuyor: aynı adı taşıyan filtreler tek anahtarla kapanır.
        modelBuilder.Entity<Blog>()
            .HasQueryFilter(SoftDelete, b => !b.IsDeleted && b.IsActive)
            .HasQueryFilter(Published, b => b.PublishedAt <= DateTime.UtcNow);

        modelBuilder.Entity<Comment>()
            .HasQueryFilter(SoftDelete, c => !c.IsDeleted && c.IsActive);
    }
}
