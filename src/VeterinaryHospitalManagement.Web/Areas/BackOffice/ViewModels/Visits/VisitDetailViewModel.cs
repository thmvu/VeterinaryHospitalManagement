using VeterinaryHospitalManagement.Web.Services.Visits;
using VeterinaryHospitalManagement.Web.Services.Clinical;
using VeterinaryHospitalManagement.Web.Services.Catalogs;

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
    public bool CanComplete { get; set; }
    public bool CanViewMedicalRecord { get; set; }
    public bool CanManageServices { get; set; }
    public IReadOnlyList<ClinicalServiceLine> ServiceLines { get; set; } = [];
    public IReadOnlyList<ServiceCatalogListItem> ServiceOptions { get; set; } = [];

    public IReadOnlyList<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> VeterinarianOptions { get; set; } = [];
}
