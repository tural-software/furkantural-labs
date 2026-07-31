namespace FurkanTural_Labs_Domain.Entities.Abstract;

/// <summary>Fiziksel olarak silinmeyen, bayrakla pasifleştirilen entity'ler.</summary>
public interface ISoftDeletable
{
    bool IsActive { get; set; }
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
}
