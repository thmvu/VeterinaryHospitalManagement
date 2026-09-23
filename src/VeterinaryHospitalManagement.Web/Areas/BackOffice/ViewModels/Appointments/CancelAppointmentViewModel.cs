using System.ComponentModel.DataAnnotations;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Appointments;

public sealed class CancelAppointmentViewModel
{
    [Required]
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do hủy lịch hẹn.")]
    [StringLength(500, ErrorMessage = "Lý do hủy không được vượt quá 500 ký tự.")]
    [Display(Name = "Lý do hủy")]
    public string Reason { get; set; } = string.Empty;

    [Required]
    public string RowVersionBase64 { get; set; } = string.Empty;
}
