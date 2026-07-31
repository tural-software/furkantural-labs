using FurkanTural_Labs_Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FurkanTural_Labs_Persistence.Sandbox;

/// <summary>
/// Veri değiştiren laboratuvarların koşum kabı.
/// <para>
/// Tohum verisi <b>ortaktır</b>: Lab_01 tabloda tam 10.000 satır olduğunu, Lab_17 sayfa
/// 500'ün nerede başladığını iddia eder. Bir laboratuvar veriyi kalıcı olarak değiştirirse
/// diğerlerinin sayıları çalışma sırasına bağlı hale gelir — kanıt kapısı da anlamını yitirir.
/// </para>
/// <para>
/// Çözüm gerçek işi yapıp geri almaktır: değişiklikler transaction içinde <b>gerçekten</b>
/// uygulanır (aynı bağlantıdan okunduğunda görünürler, sorgu sayısı ve süre gerçektir),
/// iş bitince rollback ile silinir. Sahte veri ya da bellek içi taklit yok.
/// </para>
/// </summary>
public static class DataSandbox
{
    /// <summary>İşi transaction içinde çalıştırır ve sonunda daima geri alır.</summary>
    /// <typeparam name="T">İşin döndürdüğü ölçüm tipi.</typeparam>
    /// <param name="context">İşin üzerinde çalışacağı context.</param>
    /// <param name="work">Veriyi değiştiren ve bir ölçüm döndüren iş.</param>
    /// <param name="ct">İptal jetonu.</param>
    public static async Task<T> RollbackAsync<T>(
        LabsDbContext context, Func<LabsDbContext, Task<T>> work, CancellationToken ct = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        try
        {
            return await work(context);
        }
        finally
        {
            // Rollback her durumda: iş fırlatsa bile tohum verisi kirlenmiş kalmasın.
            await transaction.RollbackAsync(ct);
        }
    }
}
