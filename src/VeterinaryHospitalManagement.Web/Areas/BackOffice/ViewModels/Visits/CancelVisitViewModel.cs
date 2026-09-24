using System.ComponentModel.DataAnnotations;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Visits;

public sealed class CancelVisitViewModel
{
    [Required]
    public int VisitId { get; set; }

    public string VisitNumber { get; set; } = string.Empty;
    public string PetName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập lý do hủy lượt khám.")]
    [StringLength(500, ErrorMessage = "Lý do hủy không được vượt quá 500 ký tự.")]
    [Display(Name = "Lý do hủy")]
    public string CancellationReason { get; set; } = string.Empty;

    [Required]
    public string RowVersionBase64 { get; set; } = string.Empty;
}
