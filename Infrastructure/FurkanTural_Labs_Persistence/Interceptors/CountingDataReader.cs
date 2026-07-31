using System.Collections;
using System.Data;
using System.Data.Common;
using FurkanTural_Labs_Application.Diagnostics;

namespace FurkanTural_Labs_Persistence.Interceptors;

/// <summary>
/// Okuyucuyu saran ve <b>gerçekten dönen satırları</b> sayan geçirgen katman.
/// <para>
/// EF'in <c>DataReaderDisposing</c> ucundaki <c>ReadCount</c> kullanılmadı: o sayı okuma
/// <i>çağrısı</i> adedidir ve okuyucu sonuna kadar tüketildiğinde sonuncu çağrı "satır
/// kalmadı" cevabını aldığı için satır sayısının bir fazlası çıkar. Fark sabit değil,
/// okuyucunun tüketilip tüketilmediğine bağlı; sabit bir sayı çıkarmak da bu yüzden
/// yanlış olur. Doğru sayıyı almanın tek yolu <see cref="Read"/> sonuçlarını saymaktır.
/// </para>
/// <para>
/// Geri kalan her üye içteki okuyucuya olduğu gibi devredilir; bu sınıf davranışı
/// değiştirmez, yalnızca gözlemler.
/// </para>
/// </summary>
internal sealed class CountingDataReader(DbDataReader inner, IQueryCounter counter) : DbDataReader
{
    private int _rows;
    private bool _reported;

    // ── Sayımın yapıldığı iki uç ──────────────────────────────────────────────
    public override bool Read()
    {
        var hasRow = inner.Read();
        if (hasRow) _rows++;
        return hasRow;
    }

    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        var hasRow = await inner.ReadAsync(cancellationToken);
        if (hasRow) _rows++;
        return hasRow;
    }

    /// <summary>Sayıyı bir kez bildirir. Dispose iki yoldan da gelebilir; çift sayım olmasın.</summary>
    private void Report()
    {
        if (_reported) return;
        _reported = true;
        counter.RecordRows(_rows);
    }

    protected override void Dispose(bool disposing)
    {
        Report();
        if (disposing) inner.Dispose();
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        Report();
        await inner.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    public override void Close()
    {
        Report();
        inner.Close();
    }

    public override async Task CloseAsync()
    {
        Report();
        await inner.CloseAsync();
    }

    // ── Devredilen üyeler ─────────────────────────────────────────────────────
    public override object this[int ordinal] => inner[ordinal];
    public override object this[string name] => inner[name];
    public override int Depth => inner.Depth;
    public override int FieldCount => inner.FieldCount;
    public override bool HasRows => inner.HasRows;
    public override bool IsClosed => inner.IsClosed;
    public override int RecordsAffected => inner.RecordsAffected;
    public override int VisibleFieldCount => inner.VisibleFieldCount;

    public override bool GetBoolean(int ordinal) => inner.GetBoolean(ordinal);
    public override byte GetByte(int ordinal) => inner.GetByte(ordinal);
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
        => inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
    public override char GetChar(int ordinal) => inner.GetChar(ordinal);
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
        => inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);
    public override string GetDataTypeName(int ordinal) => inner.GetDataTypeName(ordinal);
    public override DateTime GetDateTime(int ordinal) => inner.GetDateTime(ordinal);
    public override decimal GetDecimal(int ordinal) => inner.GetDecimal(ordinal);
    public override double GetDouble(int ordinal) => inner.GetDouble(ordinal);
    public override Type GetFieldType(int ordinal) => inner.GetFieldType(ordinal);
    public override T GetFieldValue<T>(int ordinal) => inner.GetFieldValue<T>(ordinal);
    public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken)
        => inner.GetFieldValueAsync<T>(ordinal, cancellationToken);
    public override float GetFloat(int ordinal) => inner.GetFloat(ordinal);
    public override Guid GetGuid(int ordinal) => inner.GetGuid(ordinal);
    public override short GetInt16(int ordinal) => inner.GetInt16(ordinal);
    public override int GetInt32(int ordinal) => inner.GetInt32(ordinal);
    public override long GetInt64(int ordinal) => inner.GetInt64(ordinal);
    public override string GetName(int ordinal) => inner.GetName(ordinal);
    public override int GetOrdinal(string name) => inner.GetOrdinal(name);
    public override DataTable? GetSchemaTable() => inner.GetSchemaTable();
    public override Stream GetStream(int ordinal) => inner.GetStream(ordinal);
    public override string GetString(int ordinal) => inner.GetString(ordinal);
    public override TextReader GetTextReader(int ordinal) => inner.GetTextReader(ordinal);
    public override object GetValue(int ordinal) => inner.GetValue(ordinal);
    public override int GetValues(object[] values) => inner.GetValues(values);
    public override bool IsDBNull(int ordinal) => inner.IsDBNull(ordinal);
    public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken)
        => inner.IsDBNullAsync(ordinal, cancellationToken);
    public override bool NextResult() => inner.NextResult();
    public override Task<bool> NextResultAsync(CancellationToken cancellationToken)
        => inner.NextResultAsync(cancellationToken);
    public override IEnumerator GetEnumerator() => inner.GetEnumerator();
}
