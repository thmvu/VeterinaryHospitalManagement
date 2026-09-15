using System.ComponentModel.DataAnnotations;
using VeterinaryHospitalManagement.Web.Services.Identity;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Users;

public sealed class EditUserViewModel
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;

    [Required(ErrorMessage = "Họ tên là bắt buộc.")]
    [StringLength(150)]
    [Display(Name = "Họ tên")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email là bắt buộc.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string RoleName { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public static EditUserViewModel From(ManagedUserSummary user) => new()
    {
        Id = user.Id,
        ConcurrencyStamp = user.ConcurrencyStamp,
        FullName = user.FullName,
        Email = user.Email,
        RoleName = user.RoleName,
        IsActive = user.IsActive
    };
}
