using System.Data;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore;

namespace VeterinaryHospitalManagement.Web.Data.Seed;

public sealed class SeedRunner(
    ApplicationDbContext dbContext,
    IdentitySeed identitySeed,
    PermissionSeed permissionSeed)
{
    private const string SeedLockResource = "VeterinaryHospitalManagement.IdentitySeed";

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            await AcquireSeedLockAsync(cancellationToken);

            var roles = await identitySeed.EnsureRolesAsync();
            await permissionSeed.EnsurePermissionsAsync(roles, cancellationToken);
            await identitySeed.EnsureBootstrapAdminAsync();

            await transaction.CommitAsync(cancellationToken);
        });
    }

    private async Task AcquireSeedLockAsync(CancellationToken cancellationToken)
    {
        var transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction()
            ?? throw new InvalidOperationException("A transaction is required to run the Identity seed.");
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sp_getapplock
                @Resource = N'VeterinaryHospitalManagement.IdentitySeed',
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = 60000;
            SELECT @result;
            """;
        var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (result < 0)
        {
            throw new InvalidOperationException("Could not acquire the Identity seed lock.");
        }
    }
}
