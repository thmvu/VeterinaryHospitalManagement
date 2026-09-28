using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Permissions;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Services.Identity;

public sealed class RolePermissionAdminService(
    ApplicationDbContext dbContext,
    RoleManager<IdentityRole> roleManager,
    TimeProvider timeProvider) : IRolePermissionAdminService
{
    public async Task<RolePermissionMatrixViewModel> GetMatrixAsync(CancellationToken cancellationToken = default)
    {
        var roles = await roleManager.Roles
            .OrderBy(r => r.Name)
            .Select(r => r.Name!)
            .ToListAsync(cancellationToken);

        // Đảm bảo thứ tự hiển thị ưu tiên: Admin, Manager, Veterinarian, Receptionist
        var orderedRoles = new[]
        {
            SystemRoleNames.Admin,
            SystemRoleNames.Manager,
            SystemRoleNames.Veterinarian,
            SystemRoleNames.Receptionist
        }.Where(roles.Contains).ToList();

        var permissions = await dbContext.Permissions
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var rolePermissions = await dbContext.RolePermissions
            .AsNoTracking()
            .Include(rp => rp.Role)
            .Include(rp => rp.Permission)
            .ToListAsync(cancellationToken);

        var grantsByRole = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        foreach (var roleName in orderedRoles)
        {
            var roleGrants = rolePermissions
                .Where(rp => string.Equals(rp.Role.Name, roleName, StringComparison.OrdinalIgnoreCase))
                .Select(rp => rp.Permission.Code)
                .ToHashSet(StringComparer.Ordinal);

            grantsByRole[roleName] = roleGrants;
        }

        var groups = BuildPermissionGroups(permissions);

        return new RolePermissionMatrixViewModel
        {
            Roles = orderedRoles,
            Groups = groups,
            GrantsByRole = grantsByRole,
            AdminOnlyPermissions = PermissionCatalog.AdminOnly,
            Dependencies = PermissionCatalog.Dependencies
        };
    }

    public async Task UpdateRolePermissionsAsync(
        string roleName,
        IReadOnlyCollection<string> permissionCodes,
        string actorUserId,
        CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByNameAsync(roleName);
        if (role is null)
        {
            throw new InvalidOperationException($"Không tìm thấy vai trò '{roleName}'.");
        }

        if (string.Equals(roleName, SystemRoleNames.Admin, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Vai trò Quản trị viên luôn có toàn quyền và không thể chỉnh sửa.");
        }

        var selectedSet = permissionCodes.ToHashSet(StringComparer.Ordinal);

        if (selectedSet.Any(code => !PermissionCatalog.Contains(code)))
        {
            throw new InvalidOperationException("Danh sách quyền chứa mã không hợp lệ.");
        }

        // 1. Kiểm tra AdminOnly
        if (string.Equals(roleName, SystemRoleNames.Admin, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var adminCode in PermissionCatalog.AdminOnly)
            {
                if (!selectedSet.Contains(adminCode))
                {
                    throw new InvalidOperationException($"Không thể gỡ bỏ quyền quản trị bắt buộc '{adminCode}' của vai trò Quản trị viên.");
                }
            }
        }
        else
        {
            foreach (var adminCode in PermissionCatalog.AdminOnly)
            {
                if (selectedSet.Contains(adminCode))
                {
                    throw new InvalidOperationException($"Không thể gán quyền quản trị tối cao '{adminCode}' cho vai trò '{roleName}'.");
                }
            }
        }

        // 2. Kiểm tra Dependencies
        foreach (var code in selectedSet)
        {
            if (PermissionCatalog.Dependencies.TryGetValue(code, out var requiredCode) &&
                !selectedSet.Contains(requiredCode))
            {
                throw new InvalidOperationException($"Quyền '{code}' yêu cầu phải kèm theo quyền phụ thuộc '{requiredCode}'.");
            }
        }

        // 3. Cập nhật cơ sở dữ liệu trong transaction
        var allPermissions = await dbContext.Permissions
            .ToDictionaryAsync(p => p.Code, p => p, StringComparer.Ordinal, cancellationToken);

        var currentRolePermissions = await dbContext.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .Include(rp => rp.Permission)
            .ToListAsync(cancellationToken);

        var currentCodes = currentRolePermissions.Select(rp => rp.Permission.Code).ToHashSet(StringComparer.Ordinal);

        // Xóa những quyền bị bỏ
        var toRemove = currentRolePermissions
            .Where(rp => !selectedSet.Contains(rp.Permission.Code))
            .ToList();

        if (toRemove.Count > 0)
        {
            dbContext.RolePermissions.RemoveRange(toRemove);
        }

        // Thêm những quyền mới
        var toAddCodes = selectedSet.Except(currentCodes).ToList();
        foreach (var code in toAddCodes)
        {
            if (allPermissions.TryGetValue(code, out var perm))
            {
                dbContext.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = perm.Id
                });
            }
        }

        // Ghi AuditLog
        dbContext.AuditLogs.Add(new AuditLog
        {
            ActorType = "Internal",
            UserId = actorUserId,
            Action = "Permission.UpdateRoleMatrix",
            EntityName = "IdentityRole",
            EntityId = role.Id,
            Description = $"Cập nhật phân quyền cho vai trò {roleName}: {selectedSet.Count} quyền kích hoạt.",
            CreatedAt = timeProvider.GetUtcNow()
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<PermissionGroupViewModel> BuildPermissionGroups(IReadOnlyCollection<Permission> dbPermissions)
    {
        var dbMap = dbPermissions.ToDictionary(p => p.Code, p => p.Name, StringComparer.Ordinal);

        var catalogAll = PermissionCatalog.All;

        PermissionItemViewModel ToItem(PermissionDefinition def) =>
            new(
                def.Code,
                dbMap.GetValueOrDefault(def.Code, def.Name),
                PermissionCatalog.Dependencies.GetValueOrDefault(def.Code)
            );

        return
        [
            new PermissionGroupViewModel("Tiếp đón & Lịch hẹn",
                catalogAll.Where(p => p.Code.StartsWith("Appointment.") || p.Code.StartsWith("Calendar.") || p.Code.StartsWith("Visit."))
                    .Where(p => !p.Code.StartsWith("VisitService."))
                    .Select(ToItem).ToList()),

            new PermissionGroupViewModel("Khám bệnh & Y lệnh",
                catalogAll.Where(p => p.Code.StartsWith("MedicalRecord.") || p.Code.StartsWith("Prescription.") || p.Code.StartsWith("VisitService."))
                    .Select(ToItem).ToList()),

            new PermissionGroupViewModel("Thu ngân & Hóa đơn",
                catalogAll.Where(p => p.Code.StartsWith("Invoice."))
                    .Select(ToItem).ToList()),

            new PermissionGroupViewModel("Hồ sơ & Danh mục",
                catalogAll.Where(p => p.Code.StartsWith("Owner.") || p.Code.StartsWith("Pet.") || p.Code.StartsWith("Catalog.") || p.Code.StartsWith("Schedule."))
                    .Select(ToItem).ToList()),

            new PermissionGroupViewModel("Báo cáo & Giám sát",
                catalogAll.Where(p => p.Code.StartsWith("Dashboard.") || p.Code.StartsWith("Report."))
                    .Select(ToItem).ToList()),

            new PermissionGroupViewModel("Quản trị hệ thống",
                catalogAll.Where(p => p.Code.StartsWith("User.") || p.Code.StartsWith("Permission.") || p.Code.StartsWith("Audit."))
                    .Select(ToItem).ToList())
        ];
    }
}
