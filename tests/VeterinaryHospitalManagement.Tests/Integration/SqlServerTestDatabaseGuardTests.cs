namespace VeterinaryHospitalManagement.Tests.Integration;

public class SqlServerTestDatabaseGuardTests
{
    [Fact]
    public void AcceptsExactLocalTestDatabaseContract()
    {
        SqlServerTestDatabaseGuard.EnsureSafeTestConnection(SqlServerTestDatabaseGuard.ExpectedConnectionString);
    }

    [Theory]
    [InlineData("Server=localhost;Database=VeterinaryHospitalManagementDb;Integrated Security=True;TrustServerCertificate=True")]
    [InlineData("Server=localhost;Database=master;Integrated Security=True;TrustServerCertificate=True")]
    [InlineData("Server=localhost;Database=Any_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=False")]
    [InlineData("Server=remote-server;Database=VeterinaryHospitalManagement_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=False")]
    [InlineData("Server=localhost;Database=VeterinaryHospitalManagement_Test;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=False")]
    public void RejectsConnectionOutsideExactTestContract(string connectionString)
    {
        Assert.Throws<InvalidOperationException>(() =>
            SqlServerTestDatabaseGuard.EnsureSafeTestConnection(connectionString));
    }

    [Fact]
    public void RejectsDestructiveOperationsWithoutExplicitOptIn()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SqlServerTestDatabaseGuard.EnsureDestructiveOperationsAllowed(
                SqlServerTestDatabaseGuard.ExpectedConnectionString,
                destructiveOptIn: null));
    }

    [Fact]
    public void AcceptsDestructiveOperationsOnlyWithExactOptIn()
    {
        SqlServerTestDatabaseGuard.EnsureDestructiveOperationsAllowed(
            SqlServerTestDatabaseGuard.ExpectedConnectionString,
            SqlServerTestDatabaseGuard.DestructiveOptInValue);
    }
}
