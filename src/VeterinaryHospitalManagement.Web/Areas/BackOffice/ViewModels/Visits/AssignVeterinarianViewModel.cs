using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Visits;

public sealed class AssignVeterinarianViewModel
{
    [Required]
    public int VisitId { get; set; }

    public string VisitNumber { get; set; } = string.Empty;
    public string PetName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn bác sĩ mới.")]
    [Display(Name = "Bác sĩ mới")]
    public int NewVeterinarianId { get; set; }

    [Required]
    public string RowVersionBase64 { get; set; } = string.Empty;

    public IReadOnlyList<SelectListItem> VeterinarianOptions { get; set; } = [];
}
