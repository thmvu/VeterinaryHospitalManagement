using VeterinaryHospitalManagement.Web.Services.Visits;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Visits;

public sealed class VisitDetailViewModel
{
    public required VisitDetailDto Detail { get; set; }

    public DateTime CheckedInAtLocal { get; set; }
    public DateTime? StartedAtLocal { get; set; }
    public DateTime? CompletedAtLocal { get; set; }

    public string RowVersionBase64 => Convert.ToBase64String(Detail.RowVersion);

    public bool CanAssign => Detail.Status == "Waiting";
    public bool CanCancel => Detail.Status == "Waiting";
    public bool CanStart { get; set; }
    public bool CanViewMedicalRecord { get; set; }

    public IReadOnlyList<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> VeterinarianOptions { get; set; } = [];
}
