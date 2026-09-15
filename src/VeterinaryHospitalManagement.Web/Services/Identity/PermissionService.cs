using System.Security.Claims;
using VeterinaryHospitalManagement.Web.Authorization;

namespace VeterinaryHospitalManagement.Web.Services.Identity;

public sealed class PermissionService(IUserPermissionStore permissionStore) : IPermissionService
{
    public async Task<bool> HasPermissionAsync(
        ClaimsPrincipal user,
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        if (user.Identity?.IsAuthenticated != true || !PermissionCatalog.Contains(permissionCode))
        {
            return false;
        }

        string? userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        UserPermissionState? state = await permissionStore.FindByUserIdAsync(userId, cancellationToken);
        if (state is null || !state.IsActive || !SystemRoleNames.All.Contains(state.RoleName ?? string.Empty))
        {
            return false;
        }

        if (string.Equals(state.RoleName, SystemRoleNames.Admin, StringComparison.Ordinal))
        {
            return true;
        }

        if (PermissionCatalog.AdminOnly.Contains(permissionCode) ||
            !state.PermissionCodes.Contains(permissionCode))
        {
            return false;
        }

        return !PermissionCatalog.Dependencies.TryGetValue(permissionCode, out string? prerequisite) ||
               state.PermissionCodes.Contains(prerequisite);
    }
}
