namespace FurkanTural_Labs_Domain.Entities.Abstract;

/// <summary>Kaydın oluşturulma bilgisini taşıyan entity'ler.</summary>
public interface IInsertable
{
    DateTime CreatedAt { get; set; }
    int? CreatedBy { get; set; }
}
