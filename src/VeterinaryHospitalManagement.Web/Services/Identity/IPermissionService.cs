using System.Security.Claims;

namespace VeterinaryHospitalManagement.Web.Services.Identity;

public interface IPermissionService
{
    Task<bool> HasPermissionAsync(
        ClaimsPrincipal user,
        string permissionCode,
        CancellationToken cancellationToken = default);
}
