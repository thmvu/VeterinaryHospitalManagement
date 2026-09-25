using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Web.Models.Entities;

public sealed class Prescription
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public string? Instructions { get; set; }
    public ClinicalDocumentStatus Status { get; set; } = ClinicalDocumentStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Visit Visit { get; set; } = null!;
    public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
}
