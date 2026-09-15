using VeterinaryHospitalManagement.Web.Authorization;

namespace VeterinaryHospitalManagement.Web.Services.Identity;

public static class UserManagementRules
{
    public static bool IsSystemRole(string? roleName) =>
        !string.IsNullOrWhiteSpace(roleName) && SystemRoleNames.All.Contains(roleName);

    public static bool WouldRemoveLastActiveAdmin(
        string currentRole,
        bool currentIsActive,
        int activeAdminCount,
        string nextRole,
        bool nextIsActive) =>
        currentIsActive
        && string.Equals(currentRole, SystemRoleNames.Admin, StringComparison.Ordinal)
        && activeAdminCount <= 1
        && (!nextIsActive || !string.Equals(nextRole, SystemRoleNames.Admin, StringComparison.Ordinal));
}
