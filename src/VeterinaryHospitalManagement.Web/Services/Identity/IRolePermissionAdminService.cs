using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Permissions;

namespace VeterinaryHospitalManagement.Web.Services.Identity;

public interface IRolePermissionAdminService
{
    Task<RolePermissionMatrixViewModel> GetMatrixAsync(CancellationToken cancellationToken = default);

    Task UpdateRolePermissionsAsync(
        string roleName,
        IReadOnlyCollection<string> permissionCodes,
        string actorUserId,
        CancellationToken cancellationToken = default);
}
