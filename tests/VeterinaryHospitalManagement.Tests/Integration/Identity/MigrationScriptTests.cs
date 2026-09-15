using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VeterinaryHospitalManagement.Web.Data;

namespace VeterinaryHospitalManagement.Tests.Integration.Identity;

public class MigrationScriptTests
{
    [Fact]
    public void Initial_migration_generates_the_identity_rbac_and_audit_schema_offline()
    {
        using var context = CreateContext();
        var initialMigration = Assert.Single(
            context.Database.GetMigrations(),
            migration => migration.EndsWith("_InitialIdentityAndPermissions", StringComparison.Ordinal));

        var script = context.GetService<IMigrator>().GenerateScript(
            fromMigration: Migration.InitialDatabase,
            toMigration: initialMigration);

        Assert.Contains("CREATE TABLE [AspNetUsers]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [AspNetRoles]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [Permissions]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [RolePermissions]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [AuditLogs]", script, StringComparison.Ordinal);
        Assert.Contains("[Id] bigint NOT NULL IDENTITY", script, StringComparison.Ordinal);
        Assert.Contains(
            "CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleId], [PermissionId])",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONSTRAINT [FK_RolePermissions_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE NO ACTION",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE NO ACTION",
            script,
            StringComparison.Ordinal);
        Assert.Contains("CREATE UNIQUE INDEX [IX_AspNetUserRoles_UserId]", script, StringComparison.Ordinal);
        Assert.Contains("CREATE UNIQUE INDEX [IX_Permissions_Code]", script, StringComparison.Ordinal);
        Assert.Contains(
            "CREATE UNIQUE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]) WHERE [NormalizedEmail] IS NOT NULL",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE [Roles]", script, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE [Passwords]", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Initial_migration_can_generate_down_and_up_scripts_without_a_database()
    {
        using var context = CreateContext();
        var initialMigration = Assert.Single(
            context.Database.GetMigrations(),
            migration => migration.EndsWith("_InitialIdentityAndPermissions", StringComparison.Ordinal));
        var migrator = context.GetService<IMigrator>();

        var downScript = migrator.GenerateScript(
            fromMigration: initialMigration,
            toMigration: Migration.InitialDatabase);
        var upScript = migrator.GenerateScript(
            fromMigration: Migration.InitialDatabase,
            toMigration: initialMigration);

        Assert.Contains("DROP TABLE [RolePermissions]", downScript, StringComparison.Ordinal);
        Assert.Contains("DROP TABLE [AspNetUsers]", downScript, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [RolePermissions]", upScript, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE [AspNetUsers]", upScript, StringComparison.Ordinal);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=VeterinaryHospitalManagement_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options;

        return new ApplicationDbContext(options);
    }
}
