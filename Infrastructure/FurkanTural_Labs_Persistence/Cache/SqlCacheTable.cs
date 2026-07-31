using FurkanTural_Labs_Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FurkanTural_Labs_Persistence.Cache;

/// <summary>
/// Dağıtık cache'in oturduğu tabloyu kurar.
/// <para>
/// Şema, <c>dotnet sql-cache create</c> aracının ürettiğiyle birebir aynıdır; sütun adları
/// ve tipleri <c>SqlServerCache</c>'in çalıştırdığı sorgularla sabitlenmiştir, serbestçe
/// değiştirilemez. Araç yerine burada oluşturulmasının tek sebebi laboratuvarın
/// <c>dotnet run</c> dışında hiçbir kurulum adımı istememesi.
/// </para>
/// <para>
/// Tablo EF modelinin parçası <b>değildir</b>: cache verisi alan adı değil altyapıdır,
/// entity olarak modellenirse migration ve tohumlama tartışmasına girer.
/// </para>
/// </summary>
public static class SqlCacheTable
{
    public const string SchemaName = "dbo";
    public const string TableName = "CacheEntries";

    private const string Ddl = $"""
        IF OBJECT_ID(N'[{SchemaName}].[{TableName}]', N'U') IS NULL
        BEGIN
            CREATE TABLE [{SchemaName}].[{TableName}](
                [Id] nvarchar(449) COLLATE SQL_Latin1_General_CP1_CS_AS NOT NULL,
                [Value] varbinary(MAX) NOT NULL,
                [ExpiresAtTime] datetimeoffset NOT NULL,
                [SlidingExpirationInSeconds] bigint NULL,
                [AbsoluteExpiration] datetimeoffset NULL,
                CONSTRAINT [pk_{TableName}] PRIMARY KEY ([Id]));

            CREATE NONCLUSTERED INDEX [Index_{TableName}_ExpiresAtTime]
                ON [{SchemaName}].[{TableName}]([ExpiresAtTime]);
        END
        """;

    /// <summary>Tablo yoksa oluşturur; varsa hiçbir şey yapmaz.</summary>
    public static Task EnsureAsync(LabsDbContext context, CancellationToken ct = default)
        => context.Database.ExecuteSqlRawAsync(Ddl, ct);
}
