using FurkanTural_Labs_Domain.Entities.Abstract;

namespace FurkanTural_Labs_Domain.Entities.Common;

/// <summary>
/// Monorepo'daki <c>FurkanTural_Domain.Entities.Common.BaseEntity</c>'nin laboratuvar
/// karşılığı. Alan kümesi bilerek birebir aynı: laboratuvarlarda öğrenilen davranış
/// (soft delete filtresi, audit alanlarının sorguya etkisi) gerçek projeye doğrudan taşınsın.
/// </summary>
public abstract class BaseEntity : IAuditable, ISoftDeletable
{
    public int Id { get; set; }

    // IInsertable (via IAuditable)
    public DateTime CreatedAt { get; set; }
    public int? CreatedBy { get; set; }

    // IAuditable
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedBy { get; set; }

    // ISoftDeletable
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}
