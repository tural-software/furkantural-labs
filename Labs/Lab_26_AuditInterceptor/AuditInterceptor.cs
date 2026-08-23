using FurkanTural_Labs_Domain.Entities.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Lab_26_AuditInterceptor;

/// <summary>
/// Kaydetme anında audit alanlarını dolduran interceptor. Monorepo'daki
/// <c>AuditSaveChangesInterceptor</c>'ın laboratuvar karşılığı: aynı yeri dinler, aynı
/// alanları yazar.
/// <para>
/// Laboratuvara özgü olması bilerek: paylaşılan <see cref="Microsoft.EntityFrameworkCore.DbContext"/>
/// kaydına eklenseydi diğer laboratuvarların yazdığı satırlar da damgalanır ve onların
/// sayıları değişebilirdi. Interceptor model düzeyinde değil seçenek düzeyinde takıldığı
/// için ayrı bir context tipine gerek yok — aynı <c>LabsDbContext</c>'in iki farklı
/// seçenek kümesiyle iki örneği kuruluyor.
/// </para>
/// </summary>
/// <param name="an">Damganın değeri. Sabit veriliyor ki ölçüm saate bağlı olmasın.</param>
public sealed class AuditInterceptor(DateTime an) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Damgala(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Damgala(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// Damga <see cref="Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker"/> üzerinden
    /// basılır. Ölçülen kayıpların tamamının
    /// tek sebebi bu satır: tracker'a uğramayan hiçbir yazma buradan geçmez.
    /// </summary>
    private void Damgala(DbContext? context)
    {
        if (context is null) return;

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = an;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = an;
                    break;
            }
        }
    }
}
