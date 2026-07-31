using System.Linq.Expressions;

namespace FurkanTural_Labs_Application.Specifications;

/// <summary>
/// Bir sorgunun "ne istediğini" taşıyan sözleşme (Lab_10).
/// <para>
/// Kritik ayrıntı <see cref="Criteria"/>'nın tipidir: <c>Expression&lt;Func&lt;T, bool&gt;&gt;</c>,
/// <c>Func&lt;T, bool&gt;</c> değil. İkincisi de derlenir ve doğru sonucu döndürür — ama
/// koşul artık SQL'e çevrilemez bir delegattır, filtreleme tabloyu belleğe çektikten sonra
/// yapılır. Specification'ın en pahalı hatası bu tek harflik tercihte saklıdır.
/// </para>
/// </summary>
/// <typeparam name="T">Sorgulanan entity.</typeparam>
public interface ISpecification<T>
{
    /// <summary>Filtre koşulu. <c>null</c> ise koşul uygulanmaz.</summary>
    Expression<Func<T, bool>>? Criteria { get; }

    /// <summary>Birlikte çekilecek navigation'lar.</summary>
    IReadOnlyList<Expression<Func<T, object>>> Includes { get; }

    /// <summary>Sıralama anahtarı. Sayfalama varsa zorunludur.</summary>
    Expression<Func<T, object>>? OrderBy { get; }

    /// <summary>Atlanacak satır sayısı.</summary>
    int? Skip { get; }

    /// <summary>Alınacak satır sayısı.</summary>
    int? Take { get; }

    /// <summary>Sorgunun değişiklik takibine girip girmeyeceği.</summary>
    bool AsNoTracking { get; }
}
