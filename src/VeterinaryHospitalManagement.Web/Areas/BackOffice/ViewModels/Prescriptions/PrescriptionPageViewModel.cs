using VeterinaryHospitalManagement.Web.Services.Clinical;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Prescriptions;

public sealed class PrescriptionPageViewModel
{
    public required int VisitId { get; init; }
    public required string VisitNumber { get; init; }
    public required string PetName { get; init; }
    public required string VeterinarianName { get; init; }
    public required string VisitStatus { get; init; }
    public required bool CanEdit { get; init; }
    public PrescriptionDetails? Prescription { get; init; }
}
