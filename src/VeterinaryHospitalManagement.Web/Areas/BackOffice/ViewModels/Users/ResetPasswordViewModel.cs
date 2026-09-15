using System.ComponentModel.DataAnnotations;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Users;

public sealed class ResetPasswordViewModel
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu mới là bắt buộc.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; } = string.Empty;
}
