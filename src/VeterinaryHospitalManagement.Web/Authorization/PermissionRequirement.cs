using Microsoft.AspNetCore.Authorization;

namespace VeterinaryHospitalManagement.Web.Authorization;

public sealed record PermissionRequirement(string PermissionCode) : IAuthorizationRequirement;
