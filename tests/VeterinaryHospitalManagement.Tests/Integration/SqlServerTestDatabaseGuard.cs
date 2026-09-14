using Microsoft.Data.SqlClient;

namespace VeterinaryHospitalManagement.Tests.Integration;

internal static class SqlServerTestDatabaseGuard
{
    public const string ExpectedConnectionString =
        "Server=localhost;Database=VeterinaryHospitalManagement_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=False";

    public const string DestructiveOptInValue = "YES_I_UNDERSTAND";

    public static void EnsureSafeTestConnection(string connectionString)
    {
        var actual = new SqlConnectionStringBuilder(connectionString);
        var expected = new SqlConnectionStringBuilder(ExpectedConnectionString);

        var matchesContract =
            string.Equals(actual.DataSource, expected.DataSource, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(actual.InitialCatalog, expected.InitialCatalog, StringComparison.Ordinal) &&
            actual.IntegratedSecurity == expected.IntegratedSecurity &&
            actual.Encrypt == expected.Encrypt &&
            actual.TrustServerCertificate == expected.TrustServerCertificate &&
            actual.MultipleActiveResultSets == expected.MultipleActiveResultSets;

        if (!matchesContract)
        {
            throw new InvalidOperationException(
                "SQL integration tests require the exact localhost VeterinaryHospitalManagement_Test connection contract.");
        }
    }

    public static void EnsureDestructiveOperationsAllowed(string connectionString, string? destructiveOptIn)
    {
        EnsureSafeTestConnection(connectionString);

        if (!string.Equals(destructiveOptIn, DestructiveOptInValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Destructive test database operations require explicit VETERINARY_SQL_ALLOW_DESTRUCTIVE_TESTS opt-in.");
        }
    }
}
