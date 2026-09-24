using System.ComponentModel.DataAnnotations;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Visits;

public sealed class StartVisitViewModel
{
    [Required]
    public int VisitId { get; set; }

    [Required]
    public string RowVersionBase64 { get; set; } = string.Empty;
}
