using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Services.Identity;

namespace VeterinaryHospitalManagement.Tests.Unit.Identity;

public class RolePermissionAdminRulesTests
{
    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task Admin_Permissions_Cannot_Be_Edited()
    {
        using var db = CreateDbContext();
        var roleStore = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.RoleStore<IdentityRole>(db);
        var roleManager = new RoleManager<IdentityRole>(roleStore, null!, null!, null!, null!);
        await roleManager.CreateAsync(new IdentityRole(SystemRoleNames.Admin));

        var service = new RolePermissionAdminService(db, roleManager, TimeProvider.System);

        // Danh sách quyền thiếu PermissionManage và AuditView
        var permissionsWithoutAdminOnly = new[] { PermissionCodes.DashboardView, PermissionCodes.OwnerView };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateRolePermissionsAsync(SystemRoleNames.Admin, permissionsWithoutAdminOnly, "admin-1"));

        Assert.Contains("không thể chỉnh sửa", ex.Message);
    }

    [Fact]
    public async Task NonAdmin_Cannot_Be_Granted_AdminOnly_Permissions()
    {
        using var db = CreateDbContext();
        var roleStore = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.RoleStore<IdentityRole>(db);
        var roleManager = new RoleManager<IdentityRole>(roleStore, null!, null!, null!, null!);
        await roleManager.CreateAsync(new IdentityRole(SystemRoleNames.Receptionist));

        var service = new RolePermissionAdminService(db, roleManager, TimeProvider.System);

        // Gán UserManage cho Receptionist
        var permissionsWithAdminOnly = new[] { PermissionCodes.VisitView, PermissionCodes.UserManage };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateRolePermissionsAsync(SystemRoleNames.Receptionist, permissionsWithAdminOnly, "admin-1"));

        Assert.Contains("Không thể gán quyền quản trị tối cao", ex.Message);
    }

    [Fact]
    public async Task Granting_Dependent_Permission_Without_Required_Parent_Throws()
    {
        using var db = CreateDbContext();
        var roleStore = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.RoleStore<IdentityRole>(db);
        var roleManager = new RoleManager<IdentityRole>(roleStore, null!, null!, null!, null!);
        await roleManager.CreateAsync(new IdentityRole(SystemRoleNames.Manager));

        var service = new RolePermissionAdminService(db, roleManager, TimeProvider.System);

        // Report.Export cần Report.View nhưng chỉ gán Report.Export
        var permissionsMissingParent = new[] { PermissionCodes.DashboardView, PermissionCodes.ReportExport };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateRolePermissionsAsync(SystemRoleNames.Manager, permissionsMissingParent, "admin-1"));

        Assert.Contains("yêu cầu phải kèm theo quyền phụ thuộc", ex.Message);
    }

    [Fact]
    public async Task Granting_Dependent_Permission_With_Required_Parent_Succeeds()
    {
        using var db = CreateDbContext();
        var roleStore = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.RoleStore<IdentityRole>(db);
        var roleManager = new RoleManager<IdentityRole>(roleStore, null!, null!, null!, null!);
        var role = new IdentityRole(SystemRoleNames.Manager);
        await roleManager.CreateAsync(role);

        // Thêm Permission records vào DB
        db.Permissions.AddRange(
            new Web.Models.Entities.Permission { Code = PermissionCodes.ReportView, Name = "Xem báo cáo" },
            new Web.Models.Entities.Permission { Code = PermissionCodes.ReportExport, Name = "Xuất báo cáo" }
        );
        await db.SaveChangesAsync();

        var service = new RolePermissionAdminService(db, roleManager, TimeProvider.System);

        var validPermissions = new[] { PermissionCodes.ReportView, PermissionCodes.ReportExport };

        await service.UpdateRolePermissionsAsync(SystemRoleNames.Manager, validPermissions, "admin-1");

        var granted = await db.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .Include(rp => rp.Permission)
            .Select(rp => rp.Permission.Code)
            .ToListAsync();

        Assert.Contains(PermissionCodes.ReportView, granted);
        Assert.Contains(PermissionCodes.ReportExport, granted);

        // Kiểm tra AuditLog được ghi lại
        var audit = await db.AuditLogs.SingleOrDefaultAsync(a => a.Action == "Permission.UpdateRoleMatrix");
        Assert.NotNull(audit);
        Assert.Equal("admin-1", audit.UserId);
        Assert.Equal("Internal", audit.ActorType);
        Assert.Equal("IdentityRole", audit.EntityName);
    }
}
