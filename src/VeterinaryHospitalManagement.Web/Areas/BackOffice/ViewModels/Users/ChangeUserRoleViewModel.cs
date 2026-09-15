using System.ComponentModel.DataAnnotations;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Users;

public sealed class ChangeUserRoleViewModel
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Role mới")]
    public string RoleName { get; set; } = string.Empty;
}
