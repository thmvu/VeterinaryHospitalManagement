using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Schedules;

public sealed class CreateShiftViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn bác sĩ.")]
    [Display(Name = "Bác sĩ thú y")]
    public int VeterinarianId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thời gian bắt đầu.")]
    [Display(Name = "Thời gian bắt đầu")]
    public DateTime StartAtLocal { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thời gian kết thúc.")]
    [Display(Name = "Thời gian kết thúc")]
    public DateTime EndAtLocal { get; set; }

    public IReadOnlyList<SelectListItem> Veterinarians { get; set; } = [];
}
