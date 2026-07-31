using System.Data.Common;
using FurkanTural_Labs_Application.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FurkanTural_Labs_Persistence.Interceptors;

/// <summary>
/// Veritabanına giden her komutu <see cref="IQueryCounter"/>'a bildirir.
/// <para>
/// Neden log değil de interceptor: log satırı saymak kırılgandır (formatlama değişir,
/// satır bölünür). Interceptor komut düzeyinde çalışır ve EF'in ürettiği <b>tam</b>
/// komut sayısını verir — laboratuvarların kanıtı buna dayanır.
/// </para>
/// <para>
/// Altı ucun (reader/nonquery/scalar × sync/async) hepsi ezilir; biri atlanırsa
/// <c>ExecuteUpdate</c> ya da <c>Count()</c> gibi çağrılar sayıma girmez.
/// </para>
/// <para>
/// Okuyucu kapanırken satır sayısı da bildirilir. Sorgu sayısının ayırt edemediği
/// hataların ölçüsü budur: bellekte filtreleyen bir zincir de tek sorgu çalıştırır,
/// ama arkasından tabloyu satır satır taşır.
/// </para>
/// </summary>
public sealed class QueryCountInterceptor(IQueryCounter counter) : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        counter.Record(command.CommandText);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        counter.Record(command.CommandText);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        counter.Record(command.CommandText);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        counter.Record(command.CommandText);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        counter.Record(command.CommandText);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        counter.Record(command.CommandText);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>Okuyucuyu satır sayan bir katmanla sarar; EF sarmalanmışını kullanır.</summary>
    public override DbDataReader ReaderExecuted(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        => new CountingDataReader(base.ReaderExecuted(command, eventData, result), counter);

    /// <inheritdoc cref="ReaderExecuted"/>
    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
        CancellationToken cancellationToken = default)
        => new CountingDataReader(
            await base.ReaderExecutedAsync(command, eventData, result, cancellationToken), counter);
}
