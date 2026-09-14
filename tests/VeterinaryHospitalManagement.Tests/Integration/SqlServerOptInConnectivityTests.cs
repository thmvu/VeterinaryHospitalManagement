using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Data;

namespace VeterinaryHospitalManagement.Tests.Integration;

public class SqlServerOptInConnectivityTests
{
    [SqlServerFact]
    public async Task ExactApplicationConnectionContractCanConnectToExistingTestDatabase()
    {
        var connectionString = SqlServerTestDatabaseGuard.ExpectedConnectionString;
        SqlServerTestDatabaseGuard.EnsureSafeTestConnection(connectionString);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        await using var context = new ApplicationDbContext(options);

        Assert.True(await context.Database.CanConnectAsync());
    }
}

internal sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("VETERINARY_SQL_INTEGRATION_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Set VETERINARY_SQL_INTEGRATION_TESTS=1 to run against the existing local test database.";
        }
    }
}
