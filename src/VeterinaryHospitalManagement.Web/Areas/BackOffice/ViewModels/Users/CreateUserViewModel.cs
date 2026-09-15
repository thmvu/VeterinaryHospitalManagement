using System.ComponentModel.DataAnnotations;
using VeterinaryHospitalManagement.Web.Authorization;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Users;

public sealed class CreateUserViewModel
{
    [Required(ErrorMessage = "Họ tên là bắt buộc.")]
    [StringLength(150)]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Role")]
    public string RoleName { get; set; } = SystemRoleNames.Receptionist;
}
