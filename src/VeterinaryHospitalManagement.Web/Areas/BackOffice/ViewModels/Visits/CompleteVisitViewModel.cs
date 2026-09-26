using System.ComponentModel.DataAnnotations;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Visits;

public sealed class CompleteVisitViewModel
{
    [Range(1, int.MaxValue)]
    public int VisitId { get; set; }

    [Required]
    public string RowVersionBase64 { get; set; } = string.Empty;
}
