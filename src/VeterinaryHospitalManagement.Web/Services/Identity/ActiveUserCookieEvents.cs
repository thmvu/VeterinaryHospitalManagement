using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using VeterinaryHospitalManagement.Web.Authorization;

namespace VeterinaryHospitalManagement.Web.Services.Identity;

public sealed class ActiveUserCookieEvents(
    IUserPermissionStore permissionStore,
    ISecurityStampValidator securityStampValidator)
    : CookieAuthenticationEvents
{
    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (context.Request.Path.StartsWithSegments("/BackOffice", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        return base.RedirectToAccessDenied(context);
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        await securityStampValidator.ValidateAsync(context);
        if (context.Principal is null)
        {
            return;
        }

        var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var state = string.IsNullOrWhiteSpace(userId)
            ? null
            : await permissionStore.FindByUserIdAsync(
                userId,
                context.HttpContext.RequestAborted);

        if (state is not null
            && state.IsActive
            && state.RoleName is not null
            && SystemRoleNames.All.Contains(state.RoleName))
        {
            return;
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
    }
}
