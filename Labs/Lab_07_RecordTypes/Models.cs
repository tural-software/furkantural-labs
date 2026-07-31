namespace Lab_07_RecordTypes;

/// <summary>Alışılmış DTO: mutable ve referans eşitlikli.</summary>
public class OrderDto
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>Aynı DTO, tek satır. Değer eşitliği ve <c>with</c> ücretsiz gelir.</summary>
public record OrderRecord(int Id, string CustomerName, decimal Amount);

/// <summary>
/// Record'un tek satırda verdiği eşitliği elle yazmak.
/// <para>
/// Kod doğru, sonuç aynı — ama alan eklendiğinde iki yeri de güncellemeyi hatırlamak
/// gerekir. Unutulan alan sessiz bir hatadır: nesneler eşit görünmeye devam eder.
/// </para>
/// </summary>
public sealed class OrderWithEquality : IEquatable<OrderWithEquality>
{
    public int Id { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public decimal Amount { get; init; }

    public bool Equals(OrderWithEquality? other)
        => other is not null
           && Id == other.Id
           && CustomerName == other.CustomerName
           && Amount == other.Amount;

    public override bool Equals(object? obj) => Equals(obj as OrderWithEquality);

    public override int GetHashCode() => HashCode.Combine(Id, CustomerName, Amount);
}

/// <summary>
/// Koleksiyon alanı taşıyan record.
/// <para>
/// Değer eşitliği <b>alan alan</b> karşılaştırır ve <see cref="List{T}"/> alanı için
/// karşılaştırdığı şey listenin kendisi değil referansıdır. Ayrıca <c>with</c> yüzeysel
/// kopya üretir: kopyanın listesi orijinalle aynı örnektir.
/// </para>
/// </summary>
public record Basket(int Id, List<string> Items);
