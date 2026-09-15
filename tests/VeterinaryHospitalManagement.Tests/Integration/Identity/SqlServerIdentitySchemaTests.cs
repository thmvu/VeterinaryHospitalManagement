using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Tests.Integration.Identity;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class SqlServerIdentitySchemaTests
{
    [SqlServerSchemaFact]
    public async Task Migration_runs_up_down_up_on_the_exact_test_database()
    {
        await EnsureTestDatabaseExistsAsync();
        await using var context = CreateContext();
        var migrator = context.GetService<IMigrator>();
        var initialMigration = FindInitialMigration(context);

        await migrator.MigrateAsync(initialMigration);
        Assert.Contains(initialMigration, await context.Database.GetAppliedMigrationsAsync());

        await migrator.MigrateAsync(Migration.InitialDatabase);
        Assert.Empty(await context.Database.GetAppliedMigrationsAsync());

        await migrator.MigrateAsync(initialMigration);
        Assert.Contains(initialMigration, await context.Database.GetAppliedMigrationsAsync());
    }

    [SqlServerSchemaFact]
    public async Task Normalized_email_allows_multiple_nulls_but_rejects_duplicates()
    {
        await ResetToInitialMigrationAsync();

        await using (var setup = CreateContext())
        {
            setup.Users.AddRange(
                CreateUser("null-email-1", null),
                CreateUser("null-email-2", null),
                CreateUser("email-1", "DUPLICATE@EXAMPLE.COM"));
            await setup.SaveChangesAsync();
        }

        await using var duplicateContext = CreateContext();
        duplicateContext.Users.Add(CreateUser("email-2", "DUPLICATE@EXAMPLE.COM"));

        await AssertSqlErrorAsync(() => duplicateContext.SaveChangesAsync(), 2601, 2627);
    }

    [SqlServerSchemaFact]
    public async Task Permission_code_unique_index_rejects_duplicates()
    {
        await ResetToInitialMigrationAsync();

        await using (var setup = CreateContext())
        {
            setup.Permissions.Add(new Permission { Code = "Schema.Duplicate", Name = "Schema test" });
            await setup.SaveChangesAsync();
        }

        await using var duplicateContext = CreateContext();
        duplicateContext.Permissions.Add(
            new Permission { Code = "Schema.Duplicate", Name = "Schema test duplicate" });

        await AssertSqlErrorAsync(() => duplicateContext.SaveChangesAsync(), 2601, 2627);
    }

    [SqlServerSchemaFact]
    public async Task AspNetUserRoles_unique_user_index_rejects_a_second_role()
    {
        await ResetToInitialMigrationAsync();

        await using (var setup = CreateContext())
        {
            setup.Users.Add(CreateUser("one-role-user", "ONE-ROLE@EXAMPLE.COM"));
            setup.Roles.AddRange(
                CreateRole("role-a", "RoleA"),
                CreateRole("role-b", "RoleB"));
            setup.UserRoles.Add(new IdentityUserRole<string>
            {
                UserId = "one-role-user",
                RoleId = "role-a"
            });
            await setup.SaveChangesAsync();
        }

        await using var secondRoleContext = CreateContext();
        secondRoleContext.UserRoles.Add(new IdentityUserRole<string>
        {
            UserId = "one-role-user",
            RoleId = "role-b"
        });

        await AssertSqlErrorAsync(() => secondRoleContext.SaveChangesAsync(), 2601, 2627);
    }

    [SqlServerSchemaFact]
    public async Task RolePermission_foreign_keys_reject_missing_role_and_permission()
    {
        await ResetToInitialMigrationAsync();

        int permissionId;
        await using (var setup = CreateContext())
        {
            setup.Roles.Add(CreateRole("real-role", "RealRole"));
            var permission = new Permission { Code = "Schema.Fk", Name = "Schema FK test" };
            setup.Permissions.Add(permission);
            await setup.SaveChangesAsync();
            permissionId = permission.Id;
        }

        await using (var missingRoleContext = CreateContext())
        {
            missingRoleContext.RolePermissions.Add(new RolePermission
            {
                RoleId = "missing-role",
                PermissionId = permissionId
            });
            await AssertSqlErrorAsync(() => missingRoleContext.SaveChangesAsync(), 547);
        }

        await using var missingPermissionContext = CreateContext();
        missingPermissionContext.RolePermissions.Add(new RolePermission
        {
            RoleId = "real-role",
            PermissionId = int.MaxValue
        });
        await AssertSqlErrorAsync(() => missingPermissionContext.SaveChangesAsync(), 547);
    }

    [SqlServerSchemaFact]
    public async Task AuditLog_check_constraint_rejects_unknown_actor_type()
    {
        await ResetToInitialMigrationAsync();

        await using var context = CreateContext();
        context.AuditLogs.Add(new AuditLog
        {
            ActorType = "External",
            Action = "Schema.Test",
            EntityName = "Schema",
            EntityId = "1",
            Description = "Invalid actor type test",
            CreatedAt = DateTimeOffset.UtcNow
        });

        await AssertSqlErrorAsync(() => context.SaveChangesAsync(), 547);
    }

    private static ApplicationDbContext CreateContext()
    {
        SqlServerTestDatabaseGuard.EnsureDestructiveOperationsAllowed(
            SqlServerTestDatabaseGuard.ExpectedConnectionString,
            Environment.GetEnvironmentVariable("VETERINARY_SQL_ALLOW_DESTRUCTIVE_TESTS"));

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(SqlServerTestDatabaseGuard.ExpectedConnectionString)
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task EnsureTestDatabaseExistsAsync()
    {
        SqlServerTestDatabaseGuard.EnsureDestructiveOperationsAllowed(
            SqlServerTestDatabaseGuard.ExpectedConnectionString,
            Environment.GetEnvironmentVariable("VETERINARY_SQL_ALLOW_DESTRUCTIVE_TESTS"));

        var masterConnection = new SqlConnectionStringBuilder(
            SqlServerTestDatabaseGuard.ExpectedConnectionString)
        {
            InitialCatalog = "master"
        };

        await using var connection = new SqlConnection(masterConnection.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF DB_ID(N'VeterinaryHospitalManagement_Test') IS NULL
            BEGIN
                CREATE DATABASE [VeterinaryHospitalManagement_Test];
            END;
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ResetToInitialMigrationAsync()
    {
        await EnsureTestDatabaseExistsAsync();
        await using var context = CreateContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(Migration.InitialDatabase);
        await migrator.MigrateAsync(FindInitialMigration(context));
    }

    private static string FindInitialMigration(ApplicationDbContext context) =>
        Assert.Single(
            context.Database.GetMigrations(),
            migration => migration.EndsWith("_InitialIdentityAndPermissions", StringComparison.Ordinal));

    private static ApplicationUser CreateUser(string id, string? normalizedEmail) => new()
    {
        Id = id,
        UserName = id,
        NormalizedUserName = id.ToUpperInvariant(),
        Email = normalizedEmail?.ToLowerInvariant(),
        NormalizedEmail = normalizedEmail,
        FullName = $"Schema user {id}",
        IsActive = true,
        CreatedAt = new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero),
        SecurityStamp = $"security-{id}",
        ConcurrencyStamp = $"concurrency-{id}"
    };

    private static IdentityRole CreateRole(string id, string name) => new()
    {
        Id = id,
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = $"concurrency-{id}"
    };

    private static async Task AssertSqlErrorAsync(Func<Task> operation, params int[] expectedNumbers)
    {
        var exception = await Assert.ThrowsAsync<DbUpdateException>(operation);
        var sqlException = Assert.IsType<SqlException>(exception.InnerException);
        Assert.Contains(sqlException.Number, expectedNumbers);
    }
}

internal sealed class SqlServerSchemaFactAttribute : FactAttribute
{
    public SqlServerSchemaFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("VETERINARY_SQL_INTEGRATION_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Set VETERINARY_SQL_INTEGRATION_TESTS=1 to run SQL Server schema tests.";
            return;
        }

        if (!string.Equals(
                Environment.GetEnvironmentVariable("VETERINARY_SQL_ALLOW_DESTRUCTIVE_TESTS"),
                SqlServerTestDatabaseGuard.DestructiveOptInValue,
                StringComparison.Ordinal))
        {
            Skip = "Set VETERINARY_SQL_ALLOW_DESTRUCTIVE_TESTS=YES_I_UNDERSTAND for test-database reset.";
        }
    }
}
