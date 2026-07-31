using FurkanTural_Labs_Domain.Entities.Common;

namespace FurkanTural_Labs_Domain.Entities;

/// <summary>
/// Blog'un 1-N çocuğu. N+1 problemini göstermenin en temiz yolu bu ilişkidir:
/// N tane blog çekip her birinin yorumlarına dokunmak N ek sorgu doğurur.
/// </summary>
public class Comment : BaseEntity
{
    public int BlogId { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    public virtual Blog Blog { get; set; } = null!;
}
