using Microsoft.AspNetCore.Authorization;

namespace VeterinaryHospitalManagement.Web.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class PermissionAuthorizeAttribute : AuthorizeAttribute
{
    public PermissionAuthorizeAttribute(string permissionCode)
    {
        Policy = PermissionPolicyName.For(permissionCode);
    }
}
