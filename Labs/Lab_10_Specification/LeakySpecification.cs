using FurkanTural_Labs_Domain.Entities;

namespace Lab_10_Specification;

/// <summary>
/// Specification'ın en pahalı hatası: koşulu <c>Expression&lt;Func&lt;T, bool&gt;&gt;</c>
/// yerine <c>Func&lt;T, bool&gt;</c> tutmak.
/// <para>
/// Derlenir, doğru sonucu döndürür, testleri geçer. Tek farkı koşulun artık SQL'e
/// çevrilebilir bir ifade ağacı değil, çalıştırılabilir bir delegat olmasıdır — yani
/// filtreleme tabloyu belleğe çektikten sonra yapılır. Sonuç doğru olduğu için hata
/// gözden kaçar; fark yalnızca taşınan satır sayısında görünür.
/// </para>
/// </summary>
/// <typeparam name="T">Sorgulanan entity.</typeparam>
internal interface ILeakySpecification<T>
{
    Func<T, bool> Criteria { get; }
}

/// <summary><see cref="Lab_10_Specification.PublishedBeforeSpec"/>'in sızdıran ikizi.</summary>
/// <param name="publishedBefore">Üst sınır (dahil).</param>
internal sealed class LeakyPublishedBeforeSpec(DateTime publishedBefore) : ILeakySpecification<Blog>
{
    public Func<Blog, bool> Criteria => b => b.PublishedAt <= publishedBefore;
}

internal static class LeakySpecificationEvaluator
{
    /// <summary>
    /// Doğru değerlendiriciyle satır satır aynı görünür. Tek fark <c>Where</c> çağrısının
    /// hangi sınıfa gittiğidir: koşul bir <c>Func</c> olduğu için derleyici
    /// <c>Queryable.Where</c>'i seçemez ve <c>Enumerable.Where</c>'e düşer. Bu satırdan
    /// sonra filtreleme veritabanının değil, uygulamanın işidir.
    /// </summary>
    /// <typeparam name="T">Sorgulanan entity.</typeparam>
    /// <param name="input">Başlangıç sorgusu.</param>
    /// <param name="specification">Uygulanacak koşul.</param>
    public static IEnumerable<T> Apply<T>(IQueryable<T> input, ILeakySpecification<T> specification)
        where T : class
        => input.Where(specification.Criteria);
}
