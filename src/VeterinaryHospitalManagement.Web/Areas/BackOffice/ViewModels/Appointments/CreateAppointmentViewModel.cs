using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Appointments;

public sealed class CreateAppointmentViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn thú cưng.")]
    [Display(Name = "Thú cưng")]
    public int PetId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn bác sĩ phụ trách.")]
    [Display(Name = "Bác sĩ phụ trách")]
    public int VeterinarianId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thời gian bắt đầu.")]
    [Display(Name = "Thời gian bắt đầu")]
    public DateTime StartAtLocal { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thời gian kết thúc.")]
    [Display(Name = "Thời gian kết thúc")]
    public DateTime EndAtLocal { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do khám.")]
    [StringLength(500, ErrorMessage = "Lý do khám không được vượt quá 500 ký tự.")]
    [Display(Name = "Lý do khám")]
    public string Reason { get; set; } = string.Empty;

    public IReadOnlyList<SelectListItem> Pets { get; set; } = [];
    public IReadOnlyList<SelectListItem> Veterinarians { get; set; } = [];
}
