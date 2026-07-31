using System.Linq.Expressions;

namespace FurkanTural_Labs_Application.Specifications;

/// <summary>
/// <see cref="ISpecification{T}"/> için ortak taban. Somut specification'lar yalnızca
/// kurucularında koşullarını bildirir; sorguyu kurma işi Persistence katmanındaki
/// değerlendiriciye kalır.
/// </summary>
/// <typeparam name="T">Sorgulanan entity.</typeparam>
public abstract class BaseSpecification<T> : ISpecification<T>
{
    private readonly List<Expression<Func<T, object>>> _includes = [];

    public Expression<Func<T, bool>>? Criteria { get; private set; }
    public IReadOnlyList<Expression<Func<T, object>>> Includes => _includes;
    public Expression<Func<T, object>>? OrderBy { get; private set; }
    public int? Skip { get; private set; }
    public int? Take { get; private set; }
    public bool AsNoTracking { get; private set; } = true;

    protected void AddCriteria(Expression<Func<T, bool>> criteria) => Criteria = criteria;

    protected void AddInclude(Expression<Func<T, object>> include) => _includes.Add(include);

    protected void ApplyOrderBy(Expression<Func<T, object>> orderBy) => OrderBy = orderBy;

    protected void ApplyPaging(int skip, int take) => (Skip, Take) = (skip, take);

    protected void EnableTracking() => AsNoTracking = false;

    /// <summary>
    /// Bu specification'ın koşulunu bir başkasınınkiyle <c>AND</c>'ler ve kendisini döndürür.
    /// <para>
    /// Koşullar iki ayrı lambda'dan geldiği için gövdeleri iki <b>ayrı</b> parametre nesnesine
    /// bağlıdır. İki gövdeyi doğrudan <see cref="Expression.AndAlso(Expression, Expression)"/>
    /// ile birleştirmek derlenir ama EF çeviremez: ortaya iki parametreli, bağlanmamış bir ağaç
    /// çıkar. Bu yüzden sağ taraf tek parametreye <b>yeniden bağlanır</b>.
    /// </para>
    /// </summary>
    /// <param name="other">Koşulu eklenecek specification.</param>
    public BaseSpecification<T> And(ISpecification<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (other.Criteria is null) return this;
        if (Criteria is null)
        {
            Criteria = other.Criteria;
            return this;
        }

        var parameter = Expression.Parameter(typeof(T), "x");
        var left = new ParameterRebinder(Criteria.Parameters[0], parameter).Visit(Criteria.Body);
        var right = new ParameterRebinder(other.Criteria.Parameters[0], parameter).Visit(other.Criteria.Body);

        Criteria = Expression.Lambda<Func<T, bool>>(Expression.AndAlso(left, right), parameter);
        return this;
    }

    /// <summary>Ağaçtaki eski parametre başvurularını yenisiyle değiştirir.</summary>
    private sealed class ParameterRebinder(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == from ? to : base.VisitParameter(node);
    }
}
