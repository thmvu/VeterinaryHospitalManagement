namespace VeterinaryHospitalManagement.Web.Services.Identity;

public interface IUserManagementService
{
    Task<IReadOnlyList<ManagedUserSummary>> ListAsync(CancellationToken cancellationToken = default);

    Task<ManagedUserSummary?> FindAsync(string userId, CancellationToken cancellationToken = default);

    Task<string> CreateAsync(CreateManagedUserRequest request, CancellationToken cancellationToken = default);

    Task UpdateAsync(UpdateManagedUserRequest request, CancellationToken cancellationToken = default);

    Task SetActiveAsync(UserActivationRequest request, CancellationToken cancellationToken = default);

    Task ChangeRoleAsync(ChangeUserRoleRequest request, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);
}

public sealed record ManagedUserSummary(
    string Id,
    string FullName,
    string Email,
    string RoleName,
    bool IsActive,
    string ConcurrencyStamp);

public sealed record CreateManagedUserRequest(
    string ActorUserId,
    string FullName,
    string Email,
    string Password,
    string RoleName);

public sealed record UpdateManagedUserRequest(
    string ActorUserId,
    string TargetUserId,
    string ExpectedConcurrencyStamp,
    string FullName,
    string Email);

public sealed record UserActivationRequest(
    string ActorUserId,
    string TargetUserId,
    string ExpectedConcurrencyStamp,
    bool IsActive);

public sealed record ChangeUserRoleRequest(
    string ActorUserId,
    string TargetUserId,
    string ExpectedConcurrencyStamp,
    string RoleName);

public sealed record ResetPasswordRequest(
    string ActorUserId,
    string TargetUserId,
    string ExpectedConcurrencyStamp,
    string NewPassword);

public sealed class UserManagementException(string message) : InvalidOperationException(message);
