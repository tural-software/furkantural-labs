using FurkanTural_Labs_Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FurkanTural_Labs_Persistence.Contexts;

/// <summary>
/// Tüm laboratuvarların paylaştığı context.
/// <para>
/// <b>Bilerek yapılmayan iki şey var</b> — ikisi de birer laboratuvarın konusu olduğu için
/// buraya konulmadı; buraya konulsaydı ilgili tuzak görünmez olurdu:
/// </para>
/// <list type="bullet">
///   <item>
///     <b>Global UTC ValueConverter yok.</b> Monorepo'daki context her <c>DateTime</c>'ı
///     <c>Kind=Utc</c> ile materyalize eder. Lab_12 (DateTime vs DateTimeOffset) tam olarak
///     bunun yokluğunda ne kaybedildiğini gösterecek, sonra çözümü uygulayacak.
///   </item>
///   <item>
///     <b>Soft delete için global query filter yok.</b> Filtre koysaydık her sorgunun SQL'i
///     ve satır sayısı sessizce değişirdi; laboratuvarların saydığı sorgular yazdığımız
///     sorgular olsun istiyoruz.
///   </item>
/// </list>
/// </summary>
public class LabsDbContext(DbContextOptions<LabsDbContext> options) : DbContext(options)
{
    public DbSet<Blog> Blogs => Set<Blog>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<BlogCategory> BlogCategories => Set<BlogCategory>();
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LabsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
