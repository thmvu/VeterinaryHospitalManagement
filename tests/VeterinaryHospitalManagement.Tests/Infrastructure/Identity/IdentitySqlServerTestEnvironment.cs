using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VeterinaryHospitalManagement.Tests.Integration;
using VeterinaryHospitalManagement.Web.Data;

namespace VeterinaryHospitalManagement.Tests.Infrastructure.Identity;

internal static class IdentitySqlServerTestEnvironment
{
    public const string BootstrapAdminEmail = "integration.admin@example.test";
    public const string BootstrapAdminPassword = "Integration.Admin1!";
    public const string BootstrapAdminFullName = "Integration Administrator";

    public static string ConnectionString => SqlServerTestDatabaseGuard.ExpectedConnectionString;

    public static bool IsDestructiveIntegrationEnabled =>
        string.Equals(Environment.GetEnvironmentVariable("VETERINARY_SQL_INTEGRATION_TESTS"), "1", StringComparison.Ordinal) &&
        string.Equals(
            Environment.GetEnvironmentVariable("VETERINARY_SQL_ALLOW_DESTRUCTIVE_TESTS"),
            SqlServerTestDatabaseGuard.DestructiveOptInValue,
            StringComparison.Ordinal);

    public static ApplicationDbContext CreateContext()
    {
        SqlServerTestDatabaseGuard.EnsureDestructiveOperationsAllowed(
            ConnectionString,
            Environment.GetEnvironmentVariable("VETERINARY_SQL_ALLOW_DESTRUCTIVE_TESTS"));

        return new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(ConnectionString)
                .Options);
    }

    public static async Task RecreateAndMigrateAsync()
    {
        SqlServerTestDatabaseGuard.EnsureDestructiveOperationsAllowed(
            ConnectionString,
            Environment.GetEnvironmentVariable("VETERINARY_SQL_ALLOW_DESTRUCTIVE_TESTS"));

        var master = new SqlConnectionStringBuilder(ConnectionString)
        {
            InitialCatalog = "master"
        };

        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF DB_ID(N'VeterinaryHospitalManagement_Test') IS NOT NULL
            BEGIN
                ALTER DATABASE [VeterinaryHospitalManagement_Test] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [VeterinaryHospitalManagement_Test];
            END;
            CREATE DATABASE [VeterinaryHospitalManagement_Test];
            """;
        await command.ExecuteNonQueryAsync();

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public static async Task MigrateDownAndUpAsync()
    {
        await using var context = CreateContext();
        var migration = InitialMigration(context);
        var migrator = context.GetService<IMigrator>();

        await migrator.MigrateAsync(Migration.InitialDatabase);
        Assert.Empty(await context.Database.GetAppliedMigrationsAsync());

        await migrator.MigrateAsync(migration);
        Assert.Contains(migration, await context.Database.GetAppliedMigrationsAsync());
    }

    public static string InitialMigration(ApplicationDbContext context) => Assert.Single(
        context.Database.GetMigrations(),
        migration => migration.EndsWith("_InitialIdentityAndPermissions", StringComparison.Ordinal));
}

internal sealed class IdentitySqlServerFactAttribute : FactAttribute
{
    public IdentitySqlServerFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("VETERINARY_SQL_INTEGRATION_TESTS"), "1", StringComparison.Ordinal))
        {
            Skip = "Set VETERINARY_SQL_INTEGRATION_TESTS=1 to run SQL Server Identity integration tests.";
            return;
        }

        if (!string.Equals(
                Environment.GetEnvironmentVariable("VETERINARY_SQL_ALLOW_DESTRUCTIVE_TESTS"),
                SqlServerTestDatabaseGuard.DestructiveOptInValue,
                StringComparison.Ordinal))
        {
            Skip = "Set VETERINARY_SQL_ALLOW_DESTRUCTIVE_TESTS=YES_I_UNDERSTAND to recreate the exact SQL Server test database.";
        }
    }
}
