using FurkanTural_Labs_Application.Specifications;
using FurkanTural_Labs_Domain.Entities;

namespace Lab_10_Specification;

/// <summary>Yorum almış yazılar. Tohumda ilk 1.000 blog yorumludur.</summary>
internal sealed class HasCommentsSpec : BaseSpecification<Blog>
{
    public HasCommentsSpec() => AddCriteria(b => b.Comments.Any());
}

/// <summary>Belirtilen andan önce yayımlanmış yazılar.</summary>
internal sealed class PublishedBeforeSpec : BaseSpecification<Blog>
{
    /// <param name="publishedBefore">Üst sınır (dahil).</param>
    public PublishedBeforeSpec(DateTime publishedBefore)
        => AddCriteria(b => b.PublishedAt <= publishedBefore);
}

/// <summary>Yorum almış yazılar, tarihe göre sıralı ve sayfalanmış.</summary>
internal sealed class CommentedBlogsPageSpec : BaseSpecification<Blog>
{
    /// <param name="skip">Atlanacak satır.</param>
    /// <param name="take">Alınacak satır.</param>
    public CommentedBlogsPageSpec(int skip, int take)
    {
        AddCriteria(b => b.Comments.Any());
        ApplyOrderBy(b => b.PublishedAt);
        ApplyPaging(skip, take);
    }
}
