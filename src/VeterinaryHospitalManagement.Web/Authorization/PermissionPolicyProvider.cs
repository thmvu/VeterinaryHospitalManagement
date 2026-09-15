using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace VeterinaryHospitalManagement.Web.Authorization;

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!PermissionPolicyName.TryParse(policyName, out string permissionCode))
        {
            return base.GetPolicyAsync(policyName);
        }

        if (!PermissionCatalog.Contains(permissionCode))
        {
            var denyPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(DenyPermissionRequirement.Instance)
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(denyPolicy);
        }

        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(permissionCode))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
