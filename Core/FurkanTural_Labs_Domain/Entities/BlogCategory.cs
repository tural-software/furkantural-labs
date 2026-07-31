using FurkanTural_Labs_Domain.Entities.Common;

namespace FurkanTural_Labs_Domain.Entities;

/// <summary>Blog ↔ Category çoka-çok bağlantı tablosu (monorepo ile aynı desen).</summary>
public class BlogCategory : BaseEntity
{
    public int BlogId { get; set; }
    public int CategoryId { get; set; }

    public virtual Blog Blog { get; set; } = null!;
    public virtual Category Category { get; set; } = null!;
}
