namespace FurkanTural_Labs_Domain.Entities.Abstract;

/// <summary>Oluşturma bilgisine ek olarak güncelleme bilgisini de taşıyan entity'ler.</summary>
public interface IAuditable : IInsertable
{
    DateTime? UpdatedAt { get; set; }
    int? UpdatedBy { get; set; }
}
