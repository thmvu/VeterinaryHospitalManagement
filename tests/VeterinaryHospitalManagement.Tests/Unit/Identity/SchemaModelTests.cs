using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using VeterinaryHospitalManagement.Web.Data;

namespace VeterinaryHospitalManagement.Tests.Unit.Identity;

public class SchemaModelTests
{
    private const string EntitiesNamespace =
        "VeterinaryHospitalManagement.Web.Models.Entities";

    [Fact]
    public void IdentityUser_has_required_profile_columns()
    {
        using var context = CreateContext();
        var user = RequireEntity(context, $"{EntitiesNamespace}.ApplicationUser");

        Assert.Equal("AspNetUsers", user.GetTableName());
        AssertProperty(user, "FullName", typeof(string), nullable: false, maxLength: 150);
        AssertProperty(user, "IsActive", typeof(bool), nullable: false);
        AssertProperty(user, "CreatedAt", typeof(DateTimeOffset), nullable: false, columnType: "datetimeoffset(7)");
    }

    [Fact]
    public void IdentityUser_normalized_email_is_unique_when_present()
    {
        using var context = CreateContext();
        var user = RequireEntity(context, $"{EntitiesNamespace}.ApplicationUser");

        var emailIndex = Assert.Single(
            user.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual(["NormalizedEmail"]));

        Assert.True(emailIndex.IsUnique);
        Assert.Equal("EmailIndex", emailIndex.GetDatabaseName());
        Assert.Equal("[NormalizedEmail] IS NOT NULL", emailIndex.GetFilter());
    }

    [Fact]
    public void Permission_code_is_required_and_unique()
    {
        using var context = CreateContext();
        var permission = RequireEntity(context, $"{EntitiesNamespace}.Permission");

        Assert.Equal("Permissions", permission.GetTableName());
        AssertProperty(permission, "Code", typeof(string), nullable: false, maxLength: 100);
        AssertProperty(permission, "Name", typeof(string), nullable: false, maxLength: 150);
        Assert.Contains(
            permission.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(["Code"]));
    }

    [Fact]
    public void RolePermission_uses_composite_key_and_restricting_foreign_keys()
    {
        using var context = CreateContext();
        var rolePermission = RequireEntity(context, $"{EntitiesNamespace}.RolePermission");

        Assert.Equal("RolePermissions", rolePermission.GetTableName());
        Assert.Equal(
            ["RoleId", "PermissionId"],
            rolePermission.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal(2, rolePermission.GetForeignKeys().Count());
        Assert.All(rolePermission.GetForeignKeys(), foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
        Assert.Contains(
            rolePermission.GetForeignKeys(),
            foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(IdentityRole));
        Assert.Contains(
            rolePermission.GetForeignKeys(),
            foreignKey => foreignKey.PrincipalEntityType.Name == $"{EntitiesNamespace}.Permission");
    }

    [Fact]
    public void IdentityUserRole_allows_only_one_role_per_user()
    {
        using var context = CreateContext();
        var userRole = context.Model.FindEntityType(typeof(IdentityUserRole<string>));

        Assert.NotNull(userRole);
        Assert.Contains(
            userRole.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(["UserId"]));
    }

    [Fact]
    public void AuditLog_uses_bigint_identity_and_preserves_history_when_user_is_removed()
    {
        using var context = CreateContext();
        var auditLog = RequireEntity(context, $"{EntitiesNamespace}.AuditLog");

        Assert.Equal("AuditLogs", auditLog.GetTableName());
        var id = auditLog.FindProperty("Id");
        Assert.NotNull(id);
        Assert.Equal(typeof(long), id.ClrType);
        Assert.Equal("bigint", id.GetColumnType());
        Assert.Equal(ValueGenerated.OnAdd, id.ValueGenerated);
        AssertProperty(auditLog, "ActorType", typeof(string), nullable: false, maxLength: 20);
        AssertProperty(auditLog, "Action", typeof(string), nullable: false, maxLength: 100);
        AssertProperty(auditLog, "EntityName", typeof(string), nullable: false, maxLength: 100);
        AssertProperty(auditLog, "EntityId", typeof(string), nullable: false, maxLength: 100);
        AssertProperty(auditLog, "Description", typeof(string), nullable: false, maxLength: 2000);
        AssertProperty(auditLog, "CreatedAt", typeof(DateTimeOffset), nullable: false, columnType: "datetimeoffset(7)");
        Assert.Contains(
            auditLog.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual(["CreatedAt"]));
        Assert.Contains(
            auditLog.GetForeignKeys(),
            foreignKey => foreignKey.Properties.Select(property => property.Name).SequenceEqual(["UserId"])
                          && foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=VeterinaryHospitalManagement_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options;

        return new ApplicationDbContext(options);
    }

    private static IEntityType RequireEntity(ApplicationDbContext context, string name)
    {
        var entity = context.Model.FindEntityType(name);
        Assert.True(entity is not null, $"Expected entity '{name}' to be mapped.");
        return entity!;
    }

    private static void AssertProperty(
        IEntityType entity,
        string propertyName,
        Type clrType,
        bool nullable,
        int? maxLength = null,
        string? columnType = null)
    {
        var property = entity.FindProperty(propertyName);
        Assert.NotNull(property);
        Assert.Equal(clrType, property.ClrType);
        Assert.Equal(nullable, property.IsNullable);
        Assert.Equal(maxLength, property.GetMaxLength());

        if (columnType is not null)
        {
            Assert.Equal(columnType, property.GetColumnType());
        }
    }
}
