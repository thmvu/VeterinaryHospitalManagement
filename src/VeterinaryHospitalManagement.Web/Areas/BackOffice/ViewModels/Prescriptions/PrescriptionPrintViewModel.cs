using VeterinaryHospitalManagement.Web.Services.Clinical;
using VeterinaryHospitalManagement.Web.Services.Visits;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Prescriptions;

public sealed class PrescriptionPrintViewModel
{
    public required VisitDetailDto Visit { get; init; }
    public required PrescriptionDetails Prescription { get; init; }
}
