using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Services.Identity;

public sealed class UserManagementService(
    ApplicationDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : IUserManagementService
{
    public async Task<IReadOnlyList<ManagedUserSummary>> ListAsync(CancellationToken cancellationToken = default) =>
        await (from user in dbContext.Users.AsNoTracking()
               join userRole in dbContext.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
               join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
               orderby user.Email
               select new ManagedUserSummary(
                   user.Id,
                   user.FullName,
                   user.Email!,
                   role.Name!,
                   user.IsActive,
                   user.ConcurrencyStamp ?? string.Empty))
            .ToListAsync(cancellationToken);

    public async Task<ManagedUserSummary?> FindAsync(string userId, CancellationToken cancellationToken = default) =>
        await (from user in dbContext.Users.AsNoTracking()
               join userRole in dbContext.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
               join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
               where user.Id == userId
               select new ManagedUserSummary(
                   user.Id,
                   user.FullName,
                   user.Email!,
                   role.Name!,
                   user.IsActive,
                   user.ConcurrencyStamp ?? string.Empty))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<string> CreateAsync(CreateManagedUserRequest request, CancellationToken cancellationToken = default) =>
        InSerializableTransactionAsync(async () =>
        {
            ValidateCreate(request);
            var role = await FindSystemRoleAsync(request.RoleName, cancellationToken);
            var email = request.Email.Trim();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = request.FullName.Trim(),
                IsActive = true,
                CreatedAt = timeProvider.GetUtcNow()
            };

            EnsureSucceeded(await userManager.CreateAsync(user, request.Password), "Không thể tạo tài khoản.");
            dbContext.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = role.Id });
            await dbContext.SaveChangesAsync(cancellationToken);
            AddAudit(request.ActorUserId, "Identity.UserCreated", user.Id, $"Created user with role {role.Name}.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return user.Id;
        }, cancellationToken);

    public Task UpdateAsync(UpdateManagedUserRequest request, CancellationToken cancellationToken = default) =>
        InSerializableTransactionAsync(async () =>
        {
            var user = await LoadUserAsync(request.TargetUserId, request.ExpectedConcurrencyStamp, cancellationToken);
            var email = RequireValue(request.Email, "Email là bắt buộc.").Trim();
            user.FullName = RequireValue(request.FullName, "Họ tên là bắt buộc.").Trim();
            user.Email = email;
            user.UserName = email;
            EnsureSucceeded(await userManager.UpdateAsync(user), "Không thể cập nhật tài khoản do dữ liệu đã thay đổi.");
            AddAudit(request.ActorUserId, "Identity.UserUpdated", user.Id, "Updated user profile.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        }, cancellationToken);

    public Task SetActiveAsync(UserActivationRequest request, CancellationToken cancellationToken = default) =>
        InSerializableTransactionAsync(async () =>
        {
            var user = await LoadUserAsync(request.TargetUserId, request.ExpectedConcurrencyStamp, cancellationToken);
            var currentRole = await LoadExactlyOneRoleAsync(user.Id, cancellationToken);
            await EnsureNotRemovingLastAdminAsync(currentRole.Name!, user.IsActive, currentRole.Name!, request.IsActive, cancellationToken);
            user.IsActive = request.IsActive;
            EnsureSucceeded(await userManager.UpdateAsync(user), "Không thể thay đổi trạng thái tài khoản do dữ liệu đã thay đổi.");
            EnsureSucceeded(await userManager.UpdateSecurityStampAsync(user), "Không thể thu hồi phiên đăng nhập cũ.");
            AddAudit(request.ActorUserId, request.IsActive ? "Identity.UserActivated" : "Identity.UserDeactivated", user.Id,
                request.IsActive ? "Activated user." : "Deactivated user and revoked sessions.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        }, cancellationToken);

    public async Task ChangeRoleAsync(ChangeUserRoleRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            await InSerializableTransactionAsync(async () =>
            {
                var user = await LoadUserAsync(request.TargetUserId, request.ExpectedConcurrencyStamp, cancellationToken);
                var currentRole = await LoadExactlyOneRoleAsync(user.Id, cancellationToken);
                var nextRole = await FindSystemRoleAsync(request.RoleName, cancellationToken);
                if (string.Equals(currentRole.Id, nextRole.Id, StringComparison.Ordinal))
                {
                    return 0;
                }

                await EnsureNotRemovingLastAdminAsync(currentRole.Name!, user.IsActive, nextRole.Name!, user.IsActive, cancellationToken);
                dbContext.UserRoles.Remove(new IdentityUserRole<string> { UserId = user.Id, RoleId = currentRole.Id });
                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = nextRole.Id });
                await dbContext.SaveChangesAsync(cancellationToken);
                EnsureSucceeded(await userManager.UpdateSecurityStampAsync(user), "Không thể thu hồi phiên đăng nhập cũ.");
                AddAudit(request.ActorUserId, "Identity.UserRoleChanged", user.Id,
                    $"Changed role from {currentRole.Name} to {nextRole.Name} and revoked sessions.");
                await dbContext.SaveChangesAsync(cancellationToken);
                return 0;
            }, cancellationToken);
        }
        catch (Exception exception) when (IsConcurrentRoleMutation(exception))
        {
            throw new UserManagementException("Tài khoản đã được thay đổi đồng thời. Hãy tải lại trang và thử lại.");
        }
    }

    public Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default) =>
        InSerializableTransactionAsync(async () =>
        {
            var user = await LoadUserAsync(request.TargetUserId, request.ExpectedConcurrencyStamp, cancellationToken);
            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                throw new UserManagementException("Mật khẩu mới là bắt buộc.");
            }

            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            EnsureSucceeded(await userManager.ResetPasswordAsync(user, token, request.NewPassword), "Không thể đặt lại mật khẩu.");
            EnsureSucceeded(await userManager.UpdateSecurityStampAsync(user), "Không thể thu hồi phiên đăng nhập cũ.");
            AddAudit(request.ActorUserId, "Identity.UserPasswordReset", user.Id, "Reset password and revoked sessions.");
            await dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        }, cancellationToken);

    private IQueryable<ManagedUserSummary> QueryUsers() =>
        from user in dbContext.Users
        join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
        join role in dbContext.Roles on userRole.RoleId equals role.Id
        select new ManagedUserSummary(user.Id, user.FullName, user.Email!, role.Name!, user.IsActive, user.ConcurrencyStamp ?? string.Empty);

    private async Task<ApplicationUser> LoadUserAsync(string userId, string expectedConcurrencyStamp, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken)
            ?? throw new UserManagementException("Không tìm thấy tài khoản.");
        if (!string.Equals(user.ConcurrencyStamp, expectedConcurrencyStamp, StringComparison.Ordinal))
        {
            throw new UserManagementException("Tài khoản đã được thay đổi. Hãy tải lại trang và thử lại.");
        }

        return user;
    }

    private async Task<IdentityRole> LoadExactlyOneRoleAsync(string userId, CancellationToken cancellationToken)
    {
        var roles = await (from userRole in dbContext.UserRoles
                           join role in dbContext.Roles on userRole.RoleId equals role.Id
                           where userRole.UserId == userId
                           select role).Take(2).ToListAsync(cancellationToken);
        if (roles.Count != 1 || !UserManagementRules.IsSystemRole(roles[0].Name))
        {
            throw new UserManagementException("Tài khoản không có đúng một role hệ thống.");
        }

        return roles[0];
    }

    private async Task<IdentityRole> FindSystemRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        if (!UserManagementRules.IsSystemRole(roleName))
        {
            throw new UserManagementException("Role không hợp lệ.");
        }

        return await dbContext.Roles.SingleOrDefaultAsync(role => role.Name == roleName, cancellationToken)
            ?? throw new UserManagementException("Role hệ thống chưa được khởi tạo.");
    }

    private async Task EnsureNotRemovingLastAdminAsync(
        string currentRole,
        bool currentIsActive,
        string nextRole,
        bool nextIsActive,
        CancellationToken cancellationToken)
    {
        if (currentIsActive
            && string.Equals(currentRole, SystemRoleNames.Admin, StringComparison.Ordinal)
            && (!nextIsActive || !string.Equals(nextRole, SystemRoleNames.Admin, StringComparison.Ordinal)))
        {
            await AcquireAdminInvariantLockAsync(cancellationToken);
        }

        var activeAdminCount = await (from user in dbContext.Users
                                      join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
                                      join role in dbContext.Roles on userRole.RoleId equals role.Id
                                      where user.IsActive && role.Name == SystemRoleNames.Admin
                                      select user.Id).CountAsync(cancellationToken);
        if (UserManagementRules.WouldRemoveLastActiveAdmin(
                currentRole, currentIsActive, activeAdminCount, nextRole, nextIsActive))
        {
            throw new UserManagementException("Không thể khóa hoặc hạ quyền Admin đang hoạt động cuối cùng.");
        }
    }

    private async Task AcquireAdminInvariantLockAsync(CancellationToken cancellationToken)
    {
        var transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction()
            ?? throw new InvalidOperationException("A transaction is required to protect the Admin invariant.");
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sp_getapplock
                @Resource = N'VeterinaryHospitalManagement.ActiveAdmin',
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = 10000;
            SELECT @result;
            """;
        var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (result < 0)
        {
            throw new UserManagementException("Không thể khóa thay đổi Admin. Hãy thử lại.");
        }
    }

    private void AddAudit(string actorUserId, string action, string targetUserId, string description) =>
        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorType = "Internal",
            UserId = actorUserId,
            Action = action,
            EntityName = "ApplicationUser",
            EntityId = targetUserId,
            Description = description,
            CreatedAt = timeProvider.GetUtcNow()
        });

    private async Task<T> InSerializableTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var result = await operation();
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private static void ValidateCreate(CreateManagedUserRequest request)
    {
        RequireValue(request.FullName, "Họ tên là bắt buộc.");
        RequireValue(request.Email, "Email là bắt buộc.");
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            throw new UserManagementException("Mật khẩu là bắt buộc.");
        }
    }

    private static string RequireValue(string? value, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new UserManagementException(message) : value;

    private static void EnsureSucceeded(IdentityResult result, string message)
    {
        if (!result.Succeeded)
        {
            throw new UserManagementException(UserManagementRules.ToSafeIdentityErrorMessage(result.Errors, message));
        }
    }

    private static bool IsConcurrentRoleMutation(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException)
            {
                return true;
            }

            if (current is SqlException { Number: 1205 or 1222 or 2601 or 2627 })
            {
                return true;
            }
        }

        return false;
    }
}
