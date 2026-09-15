using Microsoft.AspNetCore.Identity;

namespace VeterinaryHospitalManagement.Web.Models.Entities;

public class RolePermission
{
    public string RoleId { get; set; } = string.Empty;

    public int PermissionId { get; set; }

    public IdentityRole Role { get; set; } = null!;

    public Permission Permission { get; set; } = null!;
}
