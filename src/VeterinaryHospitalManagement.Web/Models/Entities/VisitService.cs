using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Web.Models.Entities;

public sealed class VisitService
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public int ServiceCatalogId { get; set; }
    public string ServiceNameSnapshot { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public VisitServiceStatus Status { get; set; } = VisitServiceStatus.Pending;
    public DateTimeOffset? PerformedAt { get; set; }
    public int? PerformedByVeterinarianId { get; set; }
    public string? CancellationReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Visit Visit { get; set; } = null!;
    public ServiceCatalog ServiceCatalog { get; set; } = null!;
    public VeterinarianProfile? PerformedByVeterinarian { get; set; }
}
