namespace VeterinaryHospitalManagement.Web.Services.Identity;

public sealed record UserPermissionState(
    bool IsActive,
    string? RoleName,
    IReadOnlySet<string> PermissionCodes);

public interface IUserPermissionStore
{
    Task<UserPermissionState?> FindByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
