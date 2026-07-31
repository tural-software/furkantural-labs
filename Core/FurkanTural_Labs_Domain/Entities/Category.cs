using FurkanTural_Labs_Domain.Entities.Common;

namespace FurkanTural_Labs_Domain.Entities;

public class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;

    // Navigation'lar `virtual`: Lab_04 lazy loading proxy'lerini açıp N+1'i
    // "hiç sorgu yazmadan" nasıl tetiklediğini gösterebilsin diye.
    public virtual ICollection<BlogCategory> BlogCategories { get; set; } = [];
}
