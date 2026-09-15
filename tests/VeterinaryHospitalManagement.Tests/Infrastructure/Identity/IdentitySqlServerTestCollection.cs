using Microsoft.Data.SqlClient;
using VeterinaryHospitalManagement.Tests.Integration;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace VeterinaryHospitalManagement.Tests.Infrastructure.Identity;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IdentitySqlServerTestCollection : ICollectionFixture<IdentitySqlServerTestDatabaseFixture>
{
    public const string Name = "Identity SQL Server test database";
}

public sealed class IdentitySqlServerTestDatabaseFixture : IAsyncLifetime
{
    private SqlConnection? coordinationConnection;

    public async Task InitializeAsync()
    {
        if (!IdentitySqlServerTestEnvironment.IsDestructiveIntegrationEnabled)
        {
            return;
        }

        SqlServerTestDatabaseGuard.EnsureDestructiveOperationsAllowed(
            IdentitySqlServerTestEnvironment.ConnectionString,
            Environment.GetEnvironmentVariable("VETERINARY_SQL_ALLOW_DESTRUCTIVE_TESTS"));

        var masterConnectionString = new SqlConnectionStringBuilder(
            IdentitySqlServerTestEnvironment.ConnectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString;

        coordinationConnection = new SqlConnection(masterConnectionString);
        await coordinationConnection.OpenAsync();

        await using var command = coordinationConnection.CreateCommand();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sp_getapplock
                @Resource = N'VeterinaryHospitalManagement.TestDatabaseFixture',
                @LockMode = N'Exclusive',
                @LockOwner = N'Session',
                @LockTimeout = 60000;
            SELECT @result;
            """;
        var result = Convert.ToInt32(await command.ExecuteScalarAsync());
        if (result < 0)
        {
            await coordinationConnection.DisposeAsync();
            coordinationConnection = null;
            throw new InvalidOperationException(
                "Could not acquire the cross-process lock for VeterinaryHospitalManagement_Test.");
        }
    }

    public async Task DisposeAsync()
    {
        if (coordinationConnection is not null)
        {
            await coordinationConnection.DisposeAsync();
            coordinationConnection = null;
        }
    }
}
