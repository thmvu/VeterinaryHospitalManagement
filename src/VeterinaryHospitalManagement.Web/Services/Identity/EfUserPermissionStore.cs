using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Data;

namespace VeterinaryHospitalManagement.Web.Services.Identity;

public sealed class EfUserPermissionStore(ApplicationDbContext dbContext) : IUserPermissionStore
{
    public async Task<UserPermissionState?> FindByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new { candidate.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return null;
        }

        var roles = await (
                from userRole in dbContext.UserRoles.AsNoTracking()
                join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userRole.UserId == userId
                select new { userRole.RoleId, role.Name })
            .Take(2)
            .ToListAsync(cancellationToken);

        if (roles.Count != 1 || string.IsNullOrWhiteSpace(roles[0].Name))
        {
            return new UserPermissionState(
                user.IsActive,
                RoleName: null,
                new HashSet<string>(StringComparer.Ordinal));
        }

        string roleId = roles[0].RoleId;
        string[] permissionCodes = await dbContext.RolePermissions
            .AsNoTracking()
            .Where(rolePermission => rolePermission.RoleId == roleId)
            .Select(rolePermission => rolePermission.Permission.Code)
            .ToArrayAsync(cancellationToken);

        return new UserPermissionState(
            user.IsActive,
            roles[0].Name,
            permissionCodes.ToHashSet(StringComparer.Ordinal));
    }
}
