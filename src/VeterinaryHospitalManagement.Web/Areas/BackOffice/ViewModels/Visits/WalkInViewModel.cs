using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Visits;

public sealed class WalkInViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn thú cưng.")]
    [Display(Name = "Thú cưng")]
    public int? PetId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn bác sĩ phụ trách.")]
    [Display(Name = "Bác sĩ phụ trách")]
    public int? VeterinarianId { get; set; }

    public IReadOnlyList<SelectListItem> PetOptions { get; set; } = [];
    public IReadOnlyList<SelectListItem> VeterinarianOptions { get; set; } = [];
}
