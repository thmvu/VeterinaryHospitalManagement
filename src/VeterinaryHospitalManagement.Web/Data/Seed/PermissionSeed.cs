using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Data.Seed;

public sealed class PermissionSeed(ApplicationDbContext dbContext, TimeProvider timeProvider)
{
    private const string BaselineAction = "Identity.PermissionBaselineSeeded";
    private const string BaselineEntity = "PermissionCatalog";
    private const string BaselineVersion = "v1";

    public async Task EnsurePermissionsAsync(
        IReadOnlyDictionary<string, IdentityRole> roles,
        CancellationToken cancellationToken = default)
    {
        var existingPermissions = await dbContext.Permissions
            .ToDictionaryAsync(permission => permission.Code, StringComparer.Ordinal, cancellationToken);
        var baselineMarkerExists = await dbContext.AuditLogs.AnyAsync(
            audit => audit.ActorType == "System"
                && audit.Action == BaselineAction
                && audit.EntityName == BaselineEntity
                && audit.EntityId == BaselineVersion,
            cancellationToken);
        var roleNamesById = roles.ToDictionary(
            pair => pair.Value.Id,
            pair => pair.Key,
            StringComparer.Ordinal);
        var existingGrantRows = await dbContext.RolePermissions
            .Where(rolePermission => roleNamesById.Keys.Contains(rolePermission.RoleId))
            .Select(rolePermission => new
            {
                rolePermission.RoleId,
                rolePermission.Permission.Code
            })
            .ToListAsync(cancellationToken);
        var existingGrants = SystemRoleNames.All.ToDictionary(
            roleName => roleName,
            _ => (IReadOnlySet<string>)new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);

        foreach (var row in existingGrantRows)
        {
            ((HashSet<string>)existingGrants[roleNamesById[row.RoleId]]).Add(row.Code);
        }

        var plan = PermissionSeedPlanner.Build(
            baselineMarkerExists,
            existingPermissions.Keys.ToHashSet(StringComparer.Ordinal),
            existingGrants);

        foreach (var definition in PermissionCatalog.All)
        {
            if (existingPermissions.ContainsKey(definition.Code))
            {
                continue;
            }

            var permission = new Permission
            {
                Code = definition.Code,
                Name = definition.Name
            };
            dbContext.Permissions.Add(permission);
            existingPermissions.Add(definition.Code, permission);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var (roleName, role) in roles)
        {
            foreach (var permissionCode in plan.MissingGrants[roleName])
            {
                dbContext.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = existingPermissions[permissionCode].Id
                });
            }
        }

        if (plan.AddBaselineMarker)
        {
            dbContext.AuditLogs.Add(new AuditLog
            {
                ActorType = "System",
                Action = BaselineAction,
                EntityName = BaselineEntity,
                EntityId = BaselineVersion,
                Description = "Initial role-permission baseline was seeded.",
                CreatedAt = timeProvider.GetUtcNow()
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
