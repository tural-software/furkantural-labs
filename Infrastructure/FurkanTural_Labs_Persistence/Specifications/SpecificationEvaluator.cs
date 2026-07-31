using FurkanTural_Labs_Application.Specifications;
using Microsoft.EntityFrameworkCore;

namespace FurkanTural_Labs_Persistence.Specifications;

/// <summary>
/// Specification'ı <see cref="IQueryable{T}"/> üzerine uygular. Repository'nin tek
/// <c>ListAsync</c> metodunun arkasındaki bütün iş budur; her yeni koşul için yeni bir
/// repository metodu yazılmamasının sebebi de.
/// </summary>
public static class SpecificationEvaluator
{
    /// <summary>Specification'ı sorguya çevirir; hiçbir aşamada veritabanına gidilmez.</summary>
    /// <typeparam name="T">Sorgulanan entity.</typeparam>
    /// <param name="input">Başlangıç sorgusu, tipik olarak bir <c>DbSet</c>.</param>
    /// <param name="specification">Uygulanacak koşullar.</param>
    public static IQueryable<T> Apply<T>(IQueryable<T> input, ISpecification<T> specification)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(specification);

        var query = input;

        if (specification.AsNoTracking)
            query = query.AsNoTracking();

        // Criteria bir Expression olduğu için buradaki Where, Queryable.Where'dir:
        // koşul ifade ağacına eklenir ve SQL'in WHERE'ine çevrilir.
        if (specification.Criteria is not null)
            query = query.Where(specification.Criteria);

        query = specification.Includes.Aggregate(query, (current, include) => current.Include(include));

        if (specification.OrderBy is not null)
            query = query.OrderBy(specification.OrderBy);

        if (specification.Skip.HasValue) query = query.Skip(specification.Skip.Value);
        if (specification.Take.HasValue) query = query.Take(specification.Take.Value);

        return query;
    }
}
