using Microsoft.AspNetCore.Authorization;

namespace VeterinaryHospitalManagement.Web.Authorization;

public sealed class DenyPermissionRequirement : IAuthorizationRequirement
{
    private DenyPermissionRequirement()
    {
    }

    public static DenyPermissionRequirement Instance { get; } = new();
}
