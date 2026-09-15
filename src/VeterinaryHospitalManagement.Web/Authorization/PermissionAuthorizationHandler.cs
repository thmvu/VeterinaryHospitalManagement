using Microsoft.AspNetCore.Authorization;
using VeterinaryHospitalManagement.Web.Services.Identity;

namespace VeterinaryHospitalManagement.Web.Authorization;

public sealed class PermissionAuthorizationHandler(IPermissionService permissionService)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (await permissionService.HasPermissionAsync(context.User, requirement.PermissionCode))
        {
            context.Succeed(requirement);
        }
    }
}
