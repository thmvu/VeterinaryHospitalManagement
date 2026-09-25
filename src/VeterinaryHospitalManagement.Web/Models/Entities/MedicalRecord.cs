using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Web.Models.Entities;

public sealed class MedicalRecord
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public string ChiefComplaint { get; set; } = string.Empty;
    public string? Symptoms { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? TemperatureC { get; set; }
    public string? Diagnosis { get; set; }
    public string? TreatmentNotes { get; set; }
    public DateOnly? FollowUpDate { get; set; }
    public ClinicalDocumentStatus Status { get; set; } = ClinicalDocumentStatus.Draft;
    public int? FinalizedByVeterinarianId { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Visit Visit { get; set; } = null!;
    public VeterinarianProfile? FinalizedByVeterinarian { get; set; }
}
