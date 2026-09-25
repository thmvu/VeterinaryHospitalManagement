using VeterinaryHospitalManagement.Web.Services.Clinical;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.MedicalRecords;

public sealed class MedicalRecordPageViewModel
{
    public int VisitId { get; set; }
    public string VisitNumber { get; set; } = string.Empty;
    public string PetName { get; set; } = string.Empty;
    public string VeterinarianName { get; set; } = string.Empty;
    public string VisitStatus { get; set; } = string.Empty;
    public MedicalRecordDetails? Record { get; set; }
    public bool CanEdit { get; set; }
}
