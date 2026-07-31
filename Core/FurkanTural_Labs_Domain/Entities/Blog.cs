using FurkanTural_Labs_Domain.Entities.Common;

namespace FurkanTural_Labs_Domain.Entities;

public class Blog : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int ViewCount { get; set; }

    /// <summary>Yayın anı. Lab_12 (DateTime vs DateTimeOffset) bu iki alanı karşılaştırır.</summary>
    public DateTime PublishedAt { get; set; }
    public DateTimeOffset PublishedAtOffset { get; set; }

    public virtual ICollection<Comment> Comments { get; set; } = [];
    public virtual ICollection<BlogCategory> BlogCategories { get; set; } = [];
}
