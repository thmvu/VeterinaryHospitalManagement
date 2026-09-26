using System.ComponentModel.DataAnnotations;
namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Veterinarians;
public sealed class CreateVeterinarianViewModel
{
    [Required(ErrorMessage = "Họ tên bác sĩ là bắt buộc."), StringLength(150), Display(Name = "Họ tên bác sĩ")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email đăng nhập là bắt buộc."), EmailAddress(ErrorMessage = "Email không hợp lệ."), Display(Name = "Email đăng nhập")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu là bắt buộc."), DataType(DataType.Password), Display(Name = "Mật khẩu ban đầu")]
    public string Password { get; set; } = string.Empty;

    [StringLength(150), Display(Name = "Chuyên khoa")]
    public string? Specialty { get; set; }
}
