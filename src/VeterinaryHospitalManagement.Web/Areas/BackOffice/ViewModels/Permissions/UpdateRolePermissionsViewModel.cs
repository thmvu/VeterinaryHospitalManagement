using System.ComponentModel.DataAnnotations;

namespace VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Permissions;

public sealed class UpdateRolePermissionsViewModel
{
    [Required(ErrorMessage = "Vai trò là bắt buộc.")]
    public string RoleName { get; set; } = string.Empty;

    public List<string> SelectedPermissions { get; set; } = [];
}
